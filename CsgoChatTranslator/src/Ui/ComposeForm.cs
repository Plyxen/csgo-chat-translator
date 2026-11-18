namespace CsgoChatTranslator;

/// <summary>The one-line box that opens on the hotkey for writing a message.</summary>
public sealed class ComposeForm : Form
{
    private readonly TextBox _box;
    private readonly Label _scope;
    private readonly Label _hint;
    private bool _teamOnly;
    private IntPtr _previousWindow;

    public ComposeForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Theme.Surface;
        AutoScaleMode = AutoScaleMode.None;
        Text = "Write a message";

        _scope = new Label
        {
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 9.5f),
            BackColor = Theme.Accent,
            ForeColor = Theme.Background,
            Cursor = Cursors.Hand,
        };
        _scope.Click += (_, _) => ToggleScope();

        _box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 12.5f),
            MaxLength = 240,
            PlaceholderText = "Type in English",
        };

        _hint = new Label
        {
            Text = "Enter send  ·  Tab team  ·  Esc close",
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Theme.Muted,
            BackColor = Theme.Surface,
        };

        Controls.AddRange([_scope, _box, _hint]);
        UpdateScope();
    }

    public event Action<string, bool>? Submitted;

    public void Toggle()
    {
        if (Visible) CloseBox(restoreFocus: true);
        else Open();
    }

    public void Open()
    {
        _previousWindow = Native.GetForegroundWindow();
        var screen = _previousWindow != IntPtr.Zero ? Screen.FromHandle(_previousWindow) : Screen.PrimaryScreen!;
        ArrangeFor(screen);
        Show();
        Native.ForceForeground(Handle);
        Activate();
        _box.Focus();
        _box.SelectionStart = _box.TextLength;
    }

    public void ArrangeFor(Screen screen)
    {
        var width = Math.Min(Scale(720), screen.Bounds.Width - Scale(40));
        var height = Scale(46);
        var margin = Scale(8);
        Bounds = new Rectangle(
            screen.Bounds.Left + (screen.Bounds.Width - width) / 2,
            screen.Bounds.Top + (int)(screen.Bounds.Height * 0.72),
            width, height);

        var scopeWidth = Scale(58);
        var hintWidth = Scale(220);
        _scope.Bounds = new Rectangle(margin, margin, scopeWidth, height - margin * 2);
        _hint.Bounds = new Rectangle(width - hintWidth - margin, margin, hintWidth, height - margin * 2);
        var boxLeft = _scope.Right + Scale(10);
        var boxHeight = _box.PreferredHeight;
        _box.Bounds = new Rectangle(boxLeft, (height - boxHeight) / 2, _hint.Left - boxLeft - margin, boxHeight);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Enter:
                Submit();
                return true;
            case Keys.Escape:
                _box.Clear();
                CloseBox(restoreFocus: true);
                return true;
            case Keys.Tab:
                ToggleScope();
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    // Clicking elsewhere closes the box but keeps what was typed.
    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (Visible) Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            CloseBox(restoreFocus: true);
        }
        base.OnFormClosing(e);
    }

    private void Submit()
    {
        var text = _box.Text.Trim();
        if (text.Length > 0)
        {
            _box.Clear();
            Submitted?.Invoke(text, _teamOnly);
        }
        CloseBox(restoreFocus: true);
    }

    private void CloseBox(bool restoreFocus)
    {
        Hide();
        if (restoreFocus && _previousWindow != IntPtr.Zero && Native.IsWindow(_previousWindow))
        {
            Native.SetForegroundWindow(_previousWindow);
        }
    }

    private void ToggleScope()
    {
        _teamOnly = !_teamOnly;
        UpdateScope();
        _box.Focus();
    }

    private void UpdateScope()
    {
        _scope.Text = _teamOnly ? "TEAM" : "ALL";
        _scope.BackColor = _teamOnly ? Theme.CounterTerrorist : Theme.Accent;
    }

    private int Scale(int value) => (int)Math.Round(value * DeviceDpi / 96f);
}
