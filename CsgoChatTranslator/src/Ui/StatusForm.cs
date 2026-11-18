namespace CsgoChatTranslator;

/// <summary>Shows whether each part is working and handles the one-time launch option setup.</summary>
public sealed class StatusForm : Form
{
    private readonly Settings _settings;
    private readonly Label _logValue;
    private readonly Label _linkValue;
    private readonly Label _translatorValue;
    private readonly Button _copyButton;

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

        layout.Controls.Add(Heading("In game"));
        layout.Controls.Add(Paragraph(
            $"Press {settings.Hotkey}, type in English, and press Enter. Tab switches between all chat and team chat. " +
            "Messages that start with ! or / are sent as typed."));

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

        var (translatorText, translatorColor) = engine.TranslatorWorking switch
        {
            true => ("Working", Theme.Accent),
            false => ("The service is not answering", Theme.Error),
            null => ("Starts with the first message", Theme.Muted),
        };
        Set(_translatorValue, hotkeyRegistered ? translatorText : $"{translatorText}. {_settings.Hotkey} is taken by another app.",
            hotkeyRegistered ? translatorColor : Theme.Error);
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
