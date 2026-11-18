namespace CsgoChatTranslator;

public sealed class TrayApp : ApplicationContext
{
    private const int HotkeyId = 1;
    private const int PauseHotkeyId = 2;

    private readonly Settings _settings;
    private readonly Engine _engine;
    private readonly OverlayForm _overlay;
    private readonly ComposeForm _compose = new();
    private readonly StatusForm _status;
    private readonly NotifyIcon _tray;
    private readonly Icon _icon = Theme.CreateAppIcon();
    private readonly HotkeyWindow _hotkeyWindow;
    private readonly ToolStripMenuItem _moveItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly bool _hotkeyRegistered;
    private readonly bool _pauseHotkeyRegistered;

    public TrayApp(Settings settings, bool quiet, bool showSetup = false)
    {
        _settings = settings;
        _overlay = new OverlayForm(settings);
        _ = _overlay.Handle; // Needed before worker threads post to the UI.
        _status = new StatusForm(settings, _icon);
        _engine = new Engine(settings);

        _engine.EntryAdded += entry => OnUi(() => _overlay.Add(entry));
        _engine.StatusChanged += () => OnUi(RefreshStatus);
        _engine.CopyRequested += text => OnUi(() => CopyToClipboard(text));
        _compose.Submitted += _engine.Send;
        _status.ApiKeySubmitted += _engine.SetApiKey;
        _status.PauseToggled += TogglePause;

        _pauseItem = new ToolStripMenuItem($"Pause translating ({settings.PauseHotkey})", null, (_, _) => TogglePause());

        var writeItem = new ToolStripMenuItem($"Write a message ({settings.Hotkey})", null, (_, _) => _compose.Open());
        var statusItem = new ToolStripMenuItem("Status and setup", null, (_, _) => ShowStatus());
        _moveItem = new ToolStripMenuItem("Move overlay", null, (_, _) => ToggleMove());
        var originalItem = new ToolStripMenuItem("Show Russian original")
        {
            Checked = settings.ShowOriginal,
            CheckOnClick = true,
        };
        originalItem.CheckedChanged += (_, _) =>
        {
            settings.ShowOriginal = originalItem.Checked;
            settings.Save();
            _overlay.RefreshLayout();
        };

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([writeItem, _pauseItem, statusItem, _moveItem, originalItem, new ToolStripSeparator(),
            new ToolStripMenuItem("Quit", null, (_, _) => Quit())]);

        _tray = new NotifyIcon { Icon = _icon, Text = "CS:GO Chat Translator", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => ShowStatus();

        _hotkeyWindow = new HotkeyWindow(id =>
        {
            if (id == PauseHotkeyId) TogglePause();
            else _compose.Toggle();
        });
        _hotkeyRegistered = HotkeyWindow.TryParse(settings.Hotkey, out var modifiers, out var key) &&
            Native.RegisterHotKey(_hotkeyWindow.Handle, HotkeyId, modifiers | Native.MOD_NOREPEAT, key);
        _pauseHotkeyRegistered = HotkeyWindow.TryParse(settings.PauseHotkey, out var pauseModifiers, out var pauseKey) &&
            Native.RegisterHotKey(_hotkeyWindow.Handle, PauseHotkeyId, pauseModifiers | Native.MOD_NOREPEAT, pauseKey);

        _engine.Start();
        RefreshStatus();

        var ready = SteamInfo.HasLaunchOptions(SteamInfo.ReadLaunchOptions(), settings.NetconPort, settings.NetconPassword);
        if (showSetup || (!ready && !quiet)) ShowStatus();
        _overlay.Add(OverlayEntry.Notice(_hotkeyRegistered
            ? $"Chat translator is on. Press {settings.Hotkey} to write."
            : $"Chat translator is on, but {settings.Hotkey} is taken. Use the tray icon to write."));
    }

    private void OnUi(Action action)
    {
        if (_overlay.IsDisposed) return;
        try
        {
            _overlay.BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // The app is closing.
        }
    }

    private void RefreshStatus()
    {
        _status.UpdateStatus(_engine, _hotkeyRegistered);
        _pauseItem.Text = $"{(_engine.Paused ? "Resume" : "Pause")} translating ({_settings.PauseHotkey})";
        _tray.Text = _engine.Paused ? "CS:GO Chat Translator (paused)" : "CS:GO Chat Translator";
    }

    private void TogglePause() => _engine.SetPaused(!_engine.Paused);

    private void ShowStatus()
    {
        RefreshStatus();
        _status.ShowInFront();
    }

    private void ToggleMove()
    {
        var moving = !_overlay.IsMoving;
        _overlay.SetMoveMode(moving);
        _moveItem.Text = moving ? "Finish moving" : "Move overlay";
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another app is holding the clipboard. The text is still shown in the overlay.
        }
    }

    private void Quit()
    {
        if (_hotkeyRegistered) Native.UnregisterHotKey(_hotkeyWindow.Handle, HotkeyId);
        if (_pauseHotkeyRegistered) Native.UnregisterHotKey(_hotkeyWindow.Handle, PauseHotkeyId);
        _hotkeyWindow.DestroyHandle();
        _tray.Visible = false;
        _tray.Dispose();
        _engine.Dispose();
        ExitThread();
    }

    private sealed class HotkeyWindow : NativeWindow
    {
        private readonly Action<int> _pressed;

        public HotkeyWindow(Action<int> pressed)
        {
            _pressed = pressed;
            CreateHandle(new CreateParams());
        }

        public static bool TryParse(string text, out uint modifiers, out uint key)
        {
            modifiers = 0;
            key = 0;
            foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                switch (raw.ToLowerInvariant())
                {
                    case "ctrl" or "control": modifiers |= Native.MOD_CONTROL; break;
                    case "alt": modifiers |= Native.MOD_ALT; break;
                    case "shift": modifiers |= Native.MOD_SHIFT; break;
                    default:
                        if (!Enum.TryParse<Keys>(raw, ignoreCase: true, out var parsed)) return false;
                        key = (uint)parsed;
                        break;
                }
            }
            return key != 0;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY) _pressed(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }
}
