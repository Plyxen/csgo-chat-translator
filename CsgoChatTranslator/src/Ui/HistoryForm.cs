namespace CsgoChatTranslator;

/// <summary>Every chat line since the app started, as typed and as translated, newest at the bottom.</summary>
public sealed class HistoryForm : Form
{
    private const int MaxRows = 2000;

    private readonly DataGridView _grid;
    private readonly Label _empty;

    public HistoryForm(Icon icon)
    {
        Text = "Message history";
        Icon = icon;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(980, 560);
        MinimumSize = new Size(620, 300);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Theme.Background,
            GridColor = Theme.Border,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            EnableHeadersVisualStyles = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Surface,
            SelectionForeColor = Theme.Text,
            WrapMode = DataGridViewTriState.True,
            Padding = new Padding(4, 5, 4, 5),
            Alignment = DataGridViewContentAlignment.TopLeft,
        };
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Surface,
            ForeColor = Theme.Muted,
            SelectionBackColor = Theme.Surface,
            SelectionForeColor = Theme.Muted,
            Font = new Font("Segoe UI Semibold", 9.5f),
            Padding = new Padding(4, 6, 4, 6),
        };

        AddColumn("Time", 70, fill: 0);
        AddColumn("Player", 150, fill: 0);
        AddColumn("Chat", 60, fill: 0);
        AddColumn("Untranslated", 0, fill: 50);
        AddColumn("Translated", 0, fill: 50);

        _empty = new Label
        {
            Text = "No messages yet. Chat lines appear here as they are translated.",
            ForeColor = Theme.Muted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var clear = new Button
        {
            Text = "Clear history",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Margin = new Padding(0),
        };
        clear.FlatAppearance.BorderColor = Theme.Border;
        clear.Click += (_, _) =>
        {
            _grid.Rows.Clear();
            ShowEmptyState();
        };

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10, 8, 10, 8),
            BackColor = Theme.Background,
        };
        bar.Controls.Add(clear);

        Controls.Add(_grid);
        Controls.Add(_empty);
        Controls.Add(bar);
        ShowEmptyState();
    }

    /// <summary>Adds a chat line. Notices from the app itself are not part of the history.</summary>
    public void Add(OverlayEntry entry)
    {
        if (entry.Kind == EntryKind.Notice) return;

        // For players' chat the entry holds the translation plus the original; for the admin's own
        // messages it holds the Russian that was sent plus the English that was typed.
        const string failedMark = "  (not translated)";
        var untranslated = entry.Original ?? entry.Text;
        var translated = entry.Original is not null ? entry.Text
            : entry.Kind == EntryKind.Mine ? "(sent as typed)" : "(not translated)";
        if (untranslated.EndsWith(failedMark, StringComparison.Ordinal)) untranslated = untranslated[..^failedMark.Length];
        if (entry.Flagged is not null) translated += Environment.NewLine + entry.Flagged;
        if (entry.Rule is not null) translated += Environment.NewLine + "Rule: " + entry.Rule;

        var atBottom = _grid.RowCount == 0 ||
            _grid.FirstDisplayedScrollingRowIndex + _grid.DisplayedRowCount(true) >= _grid.RowCount;

        if (_grid.RowCount >= MaxRows) _grid.Rows.RemoveAt(0);
        var index = _grid.Rows.Add(
            DateTime.Now.ToString("HH:mm:ss"),
            entry.Name ?? "",
            entry.TeamOnly ? "team" : "all",
            untranslated,
            translated);

        var color = entry.Kind == EntryKind.Mine ? Theme.Accent : entry.Team switch
        {
            ChatTeam.Terrorist => Theme.Terrorist,
            ChatTeam.CounterTerrorist => Theme.CounterTerrorist,
            _ => Theme.Text,
        };
        var name = _grid.Rows[index].Cells[1];
        name.Style.ForeColor = color;
        name.Style.SelectionForeColor = color;
        _grid.Rows[index].Cells[0].Style.ForeColor = Theme.Muted;
        _grid.Rows[index].Cells[2].Style.ForeColor = Theme.Muted;

        ShowEmptyState();
        if (atBottom && Visible) ScrollToNewest();
    }

    public void ShowInFront()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        TopMost = true;
        Native.ForceForeground(Handle);
        Activate();
        TopMost = false;
        ScrollToNewest();
    }

    private void ScrollToNewest()
    {
        if (_grid.RowCount == 0) return;
        _grid.ClearSelection();
        try
        {
            _grid.FirstDisplayedScrollingRowIndex = _grid.RowCount - 1;
        }
        catch (InvalidOperationException)
        {
            // The grid has no room to show a row yet (window still being laid out).
        }
    }

    private void ShowEmptyState()
    {
        var empty = _grid.RowCount == 0;
        _empty.Visible = empty;
        _grid.Visible = !empty;
    }

    private void AddColumn(string title, int width, int fill)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = title,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Resizable = DataGridViewTriState.True,
        };
        if (fill > 0)
        {
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = fill;
            column.MinimumWidth = 140;
        }
        else
        {
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = LogicalToDeviceUnits(width);
        }
        _grid.Columns.Add(column);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.UseDarkTitleBar(Handle);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closing only hides the window, so the history is kept until the app quits.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }
}
