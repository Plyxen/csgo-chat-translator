namespace CsgoChatTranslator;

/// <summary>Click-through message list that floats above the borderless game window.</summary>
public sealed class OverlayForm : Form
{
    private const int MaxRows = 6;
    private const int BaseWidth = 580;
    private const int MaxNameLength = 20;
    private const TextFormatFlags LineFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
    private const TextFormatFlags WrapFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;

    private sealed record Row(OverlayEntry Entry, DateTime Expires);
    private sealed record Placed(OverlayEntry Entry, string Label, Point LabelAt, Rectangle TextBox, Rectangle OriginalBox, Rectangle FlagBox);

    private readonly List<Row> _rows = [];
    private readonly List<Placed> _placed = [];
    private readonly Font _nameFont = new("Segoe UI Semibold", 11f);
    private readonly Font _textFont = new("Segoe UI", 11f);
    private readonly Font _smallFont = new("Segoe UI", 9f);
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };
    private readonly Settings _settings;
    private bool _moveMode;
    private Point _dragOffset;
    private bool _dragging;

    public OverlayForm(Settings settings)
    {
        _settings = settings;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Background;
        Opacity = 0.9;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None;
        Text = "CS:GO Chat Translator overlay";
        _timer.Tick += (_, _) => RemoveExpired();
        _timer.Start();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST |
                          Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT;
            return cp;
        }
    }

    public bool IsMoving => _moveMode;

    public void Add(OverlayEntry entry)
    {
        _rows.Add(new Row(entry, DateTime.UtcNow.AddSeconds(Math.Max(3, _settings.MessageSeconds))));
        if (_rows.Count > MaxRows) _rows.RemoveAt(0);
        Reflow();
    }

    public void RefreshLayout() => Reflow();

    public void SetMoveMode(bool on)
    {
        _moveMode = on;
        var style = Native.GetWindowLongPtr(Handle, Native.GWL_EXSTYLE).ToInt64();
        style = on ? style & ~(long)Native.WS_EX_TRANSPARENT : style | Native.WS_EX_TRANSPARENT;
        Native.SetWindowLongPtr(Handle, Native.GWL_EXSTYLE, new IntPtr(style));
        if (!on)
        {
            _settings.OverlayX = Left;
            _settings.OverlayY = Top;
            _settings.Save();
        }
        Reflow();
    }

    /// <summary>Lays out and draws to a bitmap without showing a window.</summary>
    public Bitmap RenderToBitmap()
    {
        ArrangeRows(out var size);
        var bitmap = new Bitmap(size.Width, size.Height);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Theme.Background);
        Draw(g, size);
        return bitmap;
    }

    private void RemoveExpired()
    {
        var now = DateTime.UtcNow;
        if (_rows.RemoveAll(r => r.Expires <= now) > 0) Reflow();
    }

    private void Reflow()
    {
        if (_rows.Count == 0 && !_moveMode)
        {
            if (Visible) Hide();
            return;
        }

        ArrangeRows(out var size);
        var area = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        var x = _settings.OverlayX ?? area.Left + Scale(24);
        var y = _settings.OverlayY ?? area.Top + (int)(area.Height * 0.38);
        if (_moveMode && Visible) (x, y) = (Left, Top);

        SetBounds(x, y, size.Width, size.Height);
        if (!Visible) Show();
        Native.KeepOnTop(Handle);
        Invalidate();
    }

    private void ArrangeRows(out Size size)
    {
        _placed.Clear();
        var padding = Scale(11);
        var gap = Scale(8);
        var width = Scale(BaseWidth);
        var y = padding;

        IEnumerable<OverlayEntry> entries = _rows.Select(r => r.Entry);
        if (_moveMode)
        {
            entries = entries.Prepend(OverlayEntry.Notice("Drag this box to where you want it, then choose Finish moving in the tray menu."));
        }

        foreach (var entry in entries)
        {
            var label = LabelFor(entry);
            var labelWidth = label.Length == 0 ? 0 : TextRenderer.MeasureText(label, _nameFont, Size.Empty, LineFlags).Width;
            var textX = padding + (labelWidth > 0 ? labelWidth + gap : 0);
            var available = width - textX - padding;
            var font = entry.Kind == EntryKind.Notice ? _smallFont : _textFont;
            var textSize = TextRenderer.MeasureText(entry.Text, font, new Size(available, 0), WrapFlags);
            var textBox = new Rectangle(textX, y, available, textSize.Height);
            y = textBox.Bottom;

            var originalBox = Rectangle.Empty;
            if (_settings.ShowOriginal && entry.Original is { Length: > 0 } original)
            {
                var originalSize = TextRenderer.MeasureText(original, _smallFont, new Size(available, 0), WrapFlags);
                originalBox = new Rectangle(textX, y + Scale(1), available, originalSize.Height);
                y = originalBox.Bottom;
            }

            var flagBox = Rectangle.Empty;
            if (entry.Flagged is { Length: > 0 } flagged)
            {
                var flagSize = TextRenderer.MeasureText(flagged, _smallFont, new Size(available, 0), WrapFlags);
                flagBox = new Rectangle(textX, y + Scale(2), available, flagSize.Height);
                y = flagBox.Bottom;
            }

            _placed.Add(new Placed(entry, label, new Point(padding, textBox.Top), textBox, originalBox, flagBox));
            y += gap;
        }

        size = new Size(width, Math.Max(y - gap + padding, padding * 2 + Scale(20)));
    }

    protected override void OnPaint(PaintEventArgs e) => Draw(e.Graphics, ClientSize);

    private void Draw(Graphics g, Size size)
    {
        foreach (var row in _placed)
        {
            if (row.Label.Length > 0)
            {
                TextRenderer.DrawText(g, row.Label, _nameFont, row.LabelAt, ColorFor(row.Entry), Theme.Background, LineFlags);
            }

            var isNotice = row.Entry.Kind == EntryKind.Notice;
            TextRenderer.DrawText(g, row.Entry.Text, isNotice ? _smallFont : _textFont, row.TextBox,
                isNotice ? Theme.Muted : Theme.Text, Theme.Background, WrapFlags);

            if (!row.OriginalBox.IsEmpty)
            {
                TextRenderer.DrawText(g, row.Entry.Original, _smallFont, row.OriginalBox, Theme.Muted, Theme.Background, WrapFlags);
            }

            if (!row.FlagBox.IsEmpty)
            {
                TextRenderer.DrawText(g, row.Entry.Flagged, _smallFont, row.FlagBox, Theme.Warning, Theme.Background, WrapFlags);
            }
        }

        using var border = new Pen(_moveMode ? Theme.Accent : Theme.Border);
        g.DrawRectangle(border, 0, 0, size.Width - 1, size.Height - 1);
    }

    private static string LabelFor(OverlayEntry entry)
    {
        if (entry.Kind == EntryKind.Notice || entry.Name is null) return "";

        var name = entry.Name.Length > MaxNameLength ? entry.Name[..(MaxNameLength - 1)] + "…" : entry.Name;
        var prefix = entry.Dead ? "† " : "";
        var scope = entry.TeamOnly ? entry.Team switch
        {
            ChatTeam.Terrorist => " [T]",
            ChatTeam.CounterTerrorist => " [CT]",
            ChatTeam.Spectator => " [Spec]",
            _ => " [Team]",
        } : "";
        return $"{prefix}{name}{scope}:";
    }

    private static Color ColorFor(OverlayEntry entry)
    {
        if (entry.Kind == EntryKind.Mine) return Theme.Accent;
        return entry.Team switch
        {
            ChatTeam.Terrorist => Theme.Terrorist,
            ChatTeam.CounterTerrorist => Theme.CounterTerrorist,
            _ => Theme.Muted,
        };
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_moveMode || e.Button != MouseButtons.Left) return;
        _dragging = true;
        _dragOffset = e.Location;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) Location = new Point(Cursor.Position.X - _dragOffset.X, Cursor.Position.Y - _dragOffset.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
    }

    private int Scale(int value) => (int)Math.Round(value * DeviceDpi / 96f);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _nameFont.Dispose();
            _textFont.Dispose();
            _smallFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
