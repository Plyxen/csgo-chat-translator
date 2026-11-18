namespace CsgoChatTranslator;

/// <summary>Shows whether each part is working and handles the one-time launch option setup.</summary>
public sealed class StatusForm : Form
{
    private readonly Settings _settings;
    private readonly Label _logValue;
    private readonly Label _linkValue;
    private readonly Label _translatorValue;
    private readonly Button _copyButton;
    private readonly TextBox _keyBox;
    private readonly Label _keyStatus;
    private DateTime _noKeyNoticeUntil;
    private readonly Button _pauseButton;

    /// <summary>The user clicked the pause / resume button.</summary>
    public event Action? PauseToggled;

    /// <summary>The pasted API key, or null when the user removed it.</summary>
    public event Action<string?>? ApiKeySubmitted;

    public StatusForm(Settings settings, Icon icon)
    {
        _settings = settings;
        Text = "CS:GO Chat Translator";
        Icon = icon;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10f);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(20, 16, 20, 18),
        };

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 0, 0, 14) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
        _logValue = AddRow(grid, "Reading chat");
        _linkValue = AddRow(grid, "Sending chat");
        _translatorValue = AddRow(grid, "Translation");
        layout.Controls.Add(grid);

        _pauseButton = SmallButton("Pause translating");
        _pauseButton.Margin = new Padding(0, 0, 0, 16);
        _pauseButton.Click += (_, _) => PauseToggled?.Invoke();
        layout.Controls.Add(_pauseButton);

        layout.Controls.Add(Heading("One-time setup"));
        layout.Controls.Add(Paragraph(
            "1.  Click the button below.\n" +
            "2.  In Steam, right-click CS:GO Legacy, open Properties, and replace everything under Launch Options by pasting.\n" +
            "3.  Start the game. Your current options are kept in what you paste."));

        _copyButton = new Button
        {
            Text = "Copy launch options",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Accent,
            ForeColor = Theme.Background,
            Font = new Font("Segoe UI Semibold", 10f),
            Padding = new Padding(10, 3, 10, 3),
            Margin = new Padding(0, 4, 0, 16),
        };
        _copyButton.FlatAppearance.BorderSize = 0;
        _copyButton.Click += (_, _) => CopyLaunchOptions();
        layout.Controls.Add(_copyButton);

        layout.Controls.Add(Heading("Better translations (optional)"));
        layout.Controls.Add(Paragraph(
            "Paste an Anthropic API key to translate with Claude Haiku: it understands slang and keeps insults as rude " +
            "as written. It costs a few cents per evening. Without a key, or if Claude is unreachable, the free routes are used."));

        var keyRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 2) };
        _keyBox = new TextBox
        {
            Width = 300,
            UseSystemPasswordChar = true,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "sk-ant-…",
            Margin = new Padding(0, 3, 8, 0),
        };
        var saveKey = SmallButton("Save key");
        saveKey.Click += (_, _) =>
        {
            var key = _keyBox.Text.Trim();
            if (key.Length == 0) return;
            _keyBox.Clear();
            ApiKeySubmitted?.Invoke(key);
        };
        var removeKey = SmallButton("Remove");
        removeKey.Click += (_, _) =>
        {
            _keyBox.Clear();
            ApiKeySubmitted?.Invoke(null);
        };
        keyRow.Controls.AddRange([_keyBox, saveKey, removeKey]);
        layout.Controls.Add(keyRow);

        // For setups without a keyboard (remote desktop from a phone): one click, no typing.
        var pasteKey = SmallButton("Use key from clipboard");
        pasteKey.Margin = new Padding(0, 6, 0, 2);
        pasteKey.Click += (_, _) => UseKeyFromClipboard();
        layout.Controls.Add(pasteKey);

        _keyStatus = new Label { AutoSize = true, MaximumSize = new Size(470, 0), Margin = new Padding(0, 2, 0, 16) };
        layout.Controls.Add(_keyStatus);

        layout.Controls.Add(Heading("In game"));
        layout.Controls.Add(Paragraph(
            $"Press {settings.Hotkey}, type in English, and press Enter. Tab switches between all chat and team chat. " +
            $"Messages that start with ! or / are sent as typed. {settings.PauseHotkey} pauses and resumes translating, " +
            "for when you are on the server but not playing."));

        Controls.Add(layout);
    }

    public void UpdateStatus(Engine engine, bool hotkeyRegistered)
    {
        Set(_logValue, engine.LogFound ? "Ready" : "Waiting for console.log", engine.LogFound ? Theme.Accent : Theme.Warning);

        var (text, color) = engine.Link switch
        {
            LinkState.Connected => ("Connected to the game", Theme.Accent),
            LinkState.NotAuthorized => ("The game rejected the password. Redo the setup.", Theme.Error),
            _ => ("Waiting for the game. Translations are copied for now.", Theme.Warning),
        };
        Set(_linkValue, text, color);

        var (keyText, keyColor) = engine.Ai switch
        {
            AiState.NoKey => ("No key saved. Using the free routes.", Theme.Muted),
            AiState.Unchecked => ("Key saved. Checking it…", Theme.Muted),
            AiState.Working => ("Claude Haiku is working.", Theme.Accent),
            AiState.KeyRejected => ("The key was rejected. Check it in the Anthropic Console and save it again.", Theme.Error),
            AiState.NoCredit => ("The API account has no credit. Using the free routes until it does.", Theme.Warning),
            _ => ("Claude did not answer. Using the free routes for now.", Theme.Warning),
        };
        if (DateTime.UtcNow >= _noKeyNoticeUntil) Set(_keyStatus, keyText, keyColor);

        var route = engine.TranslatorRoute == "Claude" ? "Claude Haiku" : "free route";
        var (translatorText, translatorColor) = engine.TranslatorWorking switch
        {
            true => ($"Working ({route})", Theme.Accent),
            false => ("The service is not answering", Theme.Error),
            null => ("Starts with the first message", Theme.Muted),
        };
        _pauseButton.Text = engine.Paused ? "Resume translating" : "Pause translating";
        if (engine.Paused)
        {
            Set(_translatorValue, "Paused. Chat is not sent to any translator and nothing is billed.", Theme.Warning);
            return;
        }

        Set(_translatorValue, hotkeyRegistered ? translatorText : $"{translatorText}. {_settings.Hotkey} is taken by another app.",
            hotkeyRegistered ? translatorColor : Theme.Error);
    }

    /// <summary>Finds an API key inside whatever text was copied, such as a whole sample command.</summary>
    public static string? ExtractApiKey(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(text, @"sk-ant-[A-Za-z0-9_\-]{20,}");
        return match.Success ? match.Value : null;
    }

    private void UseKeyFromClipboard()
    {
        string? text = null;
        try
        {
            if (Clipboard.ContainsText()) text = Clipboard.GetText();
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another app is holding the clipboard.
        }

        var key = ExtractApiKey(text);
        if (key is null)
        {
            _noKeyNoticeUntil = DateTime.UtcNow.AddSeconds(6);
            Set(_keyStatus, "No key found in the clipboard. Copy the text that contains sk-ant-… and click again.", Theme.Warning);
            return;
        }

        try
        {
            Clipboard.Clear();
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
        }
        _noKeyNoticeUntil = default;
        ApiKeySubmitted?.Invoke(key);
    }

    public void ShowInFront()
    {
        Show();
        TopMost = true;
        Native.ForceForeground(Handle);
        Activate();
        TopMost = false;
    }

    public void CopyLaunchOptions()
    {
        var options = SteamInfo.BuildLaunchOptions(SteamInfo.ReadLaunchOptions(), _settings.NetconPort, _settings.NetconPassword);
        try
        {
            Clipboard.SetText(options);
            _copyButton.Text = "Copied";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            _copyButton.Text = "Clipboard busy, click again";
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.UseDarkTitleBar(Handle);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            _copyButton.Text = "Copy launch options";
        }
        base.OnFormClosing(e);
    }

    private static void Set(Label label, string text, Color color)
    {
        label.Text = text;
        label.ForeColor = color;
    }

    private static Label AddRow(TableLayoutPanel grid, string name)
    {
        var row = grid.RowCount++;
        grid.Controls.Add(new Label { Text = name, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 3, 8, 3) }, 0, row);
        var value = new Label { AutoSize = true, MaximumSize = new Size(340, 0), Margin = new Padding(0, 3, 0, 3) };
        grid.Controls.Add(value, 1, row);
        return value;
    }

    private static Button SmallButton(string text)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Margin = new Padding(0, 0, 8, 0),
        };
        button.FlatAppearance.BorderColor = Theme.Border;
        return button;
    }

    private static Label Heading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 10.5f),
        Margin = new Padding(0, 0, 0, 4),
    };

    private static Label Paragraph(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(470, 0),
        ForeColor = Theme.Muted,
        Margin = new Padding(0, 0, 0, 6),
    };
}
