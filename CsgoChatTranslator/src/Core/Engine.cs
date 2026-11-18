using System.Diagnostics;
using System.Threading.Channels;

namespace CsgoChatTranslator;

public enum EntryKind { Chat, Mine, Notice }

public sealed record OverlayEntry(
    EntryKind Kind, string? Name, ChatTeam Team, bool TeamOnly, bool Dead, string Text, string? Original,
    string? Flagged = null)
{
    public static OverlayEntry Notice(string text) => new(EntryKind.Notice, null, ChatTeam.None, false, false, text, null);
}

/// <summary>Runs the two directions: Russian chat in, your typed text out. Contains no UI.</summary>
public sealed class Engine : IDisposable
{
    private const int MinimumSayGapMs = 1100;
    private static readonly TimeSpan OwnEchoWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(1.5);

    private readonly Settings _settings;
    private readonly Translator _translator = new();
    private readonly GameConsole _console;
    private readonly CancellationTokenSource _stop = new();
    private readonly Channel<ChatLine> _incoming = Channel.CreateBounded<ChatLine>(
        new BoundedChannelOptions(50) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly Channel<(string Text, bool TeamOnly)> _outgoing = Channel.CreateUnbounded<(string, bool)>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly List<(string Text, DateTime At)> _recentlySent = [];
    private readonly Stopwatch _sinceLastSay = new();
    private readonly List<(string Key, DateTime At)> _recentIncoming = [];

    public Engine(Settings settings)
    {
        _settings = settings;
        _console = new GameConsole(settings.NetconPort, settings.NetconPassword);
        _console.StateChanged += () => StatusChanged?.Invoke();
        _translator.SetApiKey(settings.GetApiKey(), settings.AiModel);
    }

    public AiState Ai => _translator.AiState;
    public string TranslatorRoute => _translator.LastRoute;

    /// <summary>
    /// While paused, incoming chat is not read, translated or sent to any service, so nothing is billed.
    /// Writing a message with the hotkey still works, because that is a deliberate action.
    /// </summary>
    public bool Paused { get; private set; }

    public void SetPaused(bool paused)
    {
        if (Paused == paused) return;
        Paused = paused;
        EntryAdded?.Invoke(OverlayEntry.Notice(paused
            ? $"Translating is paused. Nothing is sent to the translator. {_settings.PauseHotkey} resumes."
            : "Translating resumed."));
        StatusChanged?.Invoke();
    }

    /// <summary>Stores a new Claude API key (or removes it when empty) and checks it with one tiny request.</summary>
    public void SetApiKey(string? key)
    {
        _settings.SetApiKey(key);
        _translator.SetApiKey(_settings.GetApiKey(), _settings.AiModel);
        StatusChanged?.Invoke();
        if (_translator.AiReady) _ = Task.Run(CheckAiAsync);
    }

    private async Task CheckAiAsync()
    {
        try
        {
            await _translator.TranslateAsync("привет", "ru", "en", _stop.Token, aiOnly: true);
        }
        catch (Exception e) when (e is TranslationFailedException or OperationCanceledException)
        {
            // The state on the translator already says why.
        }
        StatusChanged?.Invoke();
    }

    public event Action<OverlayEntry>? EntryAdded;
    public event Action<string>? CopyRequested;
    public event Action? StatusChanged;

    public LinkState Link => _console.State;
    public string? LogPath { get; private set; }
    public bool LogFound { get; private set; }
    public bool? TranslatorWorking { get; private set; }

    public void Start()
    {
        var ct = _stop.Token;
        _ = Task.Run(() => _console.RunAsync(ct), ct);
        _ = Task.Run(() => TailLoopAsync(ct), ct);
        _ = Task.Run(() => IncomingLoopAsync(ct), ct);
        _ = Task.Run(() => OutgoingLoopAsync(ct), ct);
    }

    public void Send(string text, bool teamOnly) => _outgoing.Writer.TryWrite((text, teamOnly));

    private async Task TailLoopAsync(CancellationToken ct)
    {
        LogTailer? tailer = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (tailer is null)
                {
                    var path = _settings.ConsoleLogPath ?? SteamInfo.FindConsoleLog();
                    if (path is null)
                    {
                        await Task.Delay(5000, ct);
                        continue;
                    }
                    tailer = new LogTailer(path);
                    LogPath = path;
                }

                foreach (var line in tailer.Poll()) HandleLogLine(line);

                if (LogFound != tailer.FileExists)
                {
                    LogFound = tailer.FileExists;
                    StatusChanged?.Invoke();
                }
                await Task.Delay(200, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void HandleLogLine(string line)
    {
        // Lines that arrive while paused are dropped, not queued, so resuming never causes a burst of requests.
        if (Paused) return;

        var chat = ChatParser.Parse(line);
        if (chat is null) return;

        // Russian in Latin letters ("cyka blyat") has no Cyrillic but still matters to an admin.
        if (!ChatParser.HasCyrillic(chat.Text) && Profanity.Describe(chat.Text) is null &&
            !(_translator.AiReady && ChatParser.LooksLikeLatinRussian(chat.Text)))
        {
            return;
        }
        if (IsOwnEcho(chat.Text)) return;

        // The same message can be printed twice when a server chat plugin is active.
        var now = DateTime.UtcNow;
        var key = chat.Name + "\n" + chat.Text;
        _recentIncoming.RemoveAll(r => now - r.At >= DuplicateWindow);
        if (_recentIncoming.Exists(r => r.Key == key)) return;
        _recentIncoming.Add((key, now));

        _incoming.Writer.TryWrite(chat);
    }

    private bool IsOwnEcho(string text)
    {
        lock (_recentlySent)
        {
            var now = DateTime.UtcNow;
            _recentlySent.RemoveAll(s => now - s.At > OwnEchoWindow);
            var index = _recentlySent.FindIndex(s => s.Text == text);
            if (index < 0) return false;
            _recentlySent.RemoveAt(index);
            return true;
        }
    }

    private async Task IncomingLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var chat in _incoming.Reader.ReadAllAsync(ct))
            {
                // Anything still queued from before the pause is dropped too.
                if (Paused) continue;

                var flagged = Profanity.Describe(chat.Text);
                if (!ChatParser.HasCyrillic(chat.Text))
                {
                    // Latin-letter Russian: only Claude can read it. Without it, show the line as typed.
                    var shown = chat.Text;
                    string? typed = null;
                    try
                    {
                        shown = await _translator.TranslateAsync(chat.Text, _settings.TheirLanguage, _settings.MyLanguage, ct, aiOnly: true);
                        typed = chat.Text;
                    }
                    catch (TranslationFailedException)
                    {
                    }
                    EntryAdded?.Invoke(new OverlayEntry(EntryKind.Chat, chat.Name, chat.Team, chat.TeamOnly, chat.Dead, shown, typed, flagged));
                    continue;
                }

                string text;
                string? original = chat.Text;
                try
                {
                    text = await _translator.TranslateAsync(chat.Text, _settings.TheirLanguage, _settings.MyLanguage, ct);
                    SetTranslatorWorking(true);
                }
                catch (TranslationFailedException)
                {
                    SetTranslatorWorking(false);
                    text = chat.Text + "  (not translated)";
                    original = null;
                }
                EntryAdded?.Invoke(new OverlayEntry(EntryKind.Chat, chat.Name, chat.Team, chat.TeamOnly, chat.Dead, text, original, flagged));
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task OutgoingLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var (rawText, teamOnly) in _outgoing.Reader.ReadAllAsync(ct))
            {
                await SendOneAsync(rawText, teamOnly, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task SendOneAsync(string rawText, bool teamOnly, CancellationToken ct)
    {
        var typed = OutgoingText.Sanitize(rawText);
        if (typed.Length == 0) return;

        var message = typed;
        var keepAsTyped = OutgoingText.IsServerCommand(typed) || ChatParser.HasCyrillic(typed);
        if (!keepAsTyped)
        {
            try
            {
                message = OutgoingText.Sanitize(
                    await _translator.TranslateAsync(typed, _settings.MyLanguage, _settings.TheirLanguage, ct, incoming: false));
                SetTranslatorWorking(true);
            }
            catch (TranslationFailedException)
            {
                SetTranslatorWorking(false);
                EntryAdded?.Invoke(OverlayEntry.Notice("Translation failed, so nothing was sent."));
                return;
            }
        }

        if (_console.State != LinkState.Connected)
        {
            CopyRequested?.Invoke(message);
            EntryAdded?.Invoke(OverlayEntry.Notice($"Copied. Paste it in game chat with Ctrl+V: {message}"));
            return;
        }

        foreach (var part in OutgoingText.Split(message))
        {
            if (_sinceLastSay.IsRunning)
            {
                var wait = MinimumSayGapMs - (int)_sinceLastSay.ElapsedMilliseconds;
                if (wait > 0) await Task.Delay(wait, ct);
            }

            lock (_recentlySent) _recentlySent.Add((part, DateTime.UtcNow));
            if (!await _console.SendAsync(OutgoingText.ToConsoleCommand(part, teamOnly), ct))
            {
                CopyRequested?.Invoke(message);
                EntryAdded?.Invoke(OverlayEntry.Notice("Lost the game connection. The message was copied instead."));
                return;
            }
            _sinceLastSay.Restart();
        }

        EntryAdded?.Invoke(new OverlayEntry(EntryKind.Mine, "You", ChatTeam.None, teamOnly, false, message, keepAsTyped ? null : typed));
    }

    private string _lastRouteShown = "";

    private void SetTranslatorWorking(bool working)
    {
        if (TranslatorWorking == working && _lastRouteShown == _translator.LastRoute) return;
        TranslatorWorking = working;
        _lastRouteShown = _translator.LastRoute;
        StatusChanged?.Invoke();
    }

    public void Dispose()
    {
        _stop.Cancel();
        _translator.Dispose();
        _console.Dispose();
    }
}
