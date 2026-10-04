// "Ask every time": a category set to it (on the Categories tab, under the browsers) opens no browser
// of its own. Each link it gets shows this small window instead, next to the mouse: every category
// with a browser, one row each - pick one and the link opens there. Handy while many links come that
// no rule covers yet.
//
// It is shown by the short-lived copy of LinkPilot that hands the link on (Program.Open), so it works
// without the dock. Enter, a double-click or a row's number 1-9 opens; Esc or closing it opens
// nothing. "Remember for github.com" also adds an address rule, so that site goes there from now on.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

class AskForm : Form
{
    // the sorts, as config.txt says them (Config.AskSort) and as the window does
    static readonly string[] Sorts = { "name", "most", "recent" };
    static readonly string[] SortNames = { "Name", "Most used", "Recently used" };

    readonly string site;      // "github.com" - or "" for a link that is not a web address
    readonly List<Row> rows = new List<Row>();
    readonly ListBox list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed,
                                          ItemHeight = 40, BorderStyle = BorderStyle.None };
    readonly ComboBox sort = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Dock = DockStyle.Right };
    readonly CheckBox remember = new CheckBox { AutoSize = false, AutoEllipsis = true };
    readonly Font bold = new Font("Segoe UI", 9F, FontStyle.Bold), small = new Font("Segoe UI", 8F);
    Category picked;

    // one category to choose: its icon, and how many links went to it and when the last did (the link log)
    class Row : IDisposable
    {
        public Category Cat; public Bitmap Picture; public int Links; public DateTime Last;
        public override string ToString() { return Cat.Name + " - " + Cat.Shows; }   // for screen readers
        public void Dispose() { if (Picture != null) Picture.Dispose(); }
    }

    public int RowCount { get { return rows.Count; } }   // read by --selftest

    // Shows the window and waits: the category picked, or null when nothing should open. The sort, and
    // a ticked Remember, are saved to config.txt - read again first, as it may have changed meanwhile.
    // source: the app the link came from, as Router.Decide takes it.
    public static Category Pick(string url, string source)
    {
        using (var f = new AskForm(url))
        {
            f.ShowDialog();
            string how = Sorts[Math.Max(0, f.sort.SelectedIndex)];
            bool rule = f.picked != null && f.site.Length > 0 && f.remember.Checked;
            if ((how != Config.AskSort || rule) && Config.Load())
            {
                Config.AskSort = how;
                // at the end of the list - or, if the site already has a rule, in its place. And never after
                // the rule that sent this link here (Signal -> Ask every time): that one would still win.
                if (rule)
                {
                    var r = new Rule { Label = f.site, Match = f.site, Category = f.picked.Name };
                    RulesPage.Place(r, null, older => true);
                    var first = Router.Decide(url, source);
                    if (first != null && first != r)
                    {
                        Config.Rules.Remove(r);
                        Config.Rules.Insert(Config.Rules.IndexOf(first), r);
                    }
                }
                Config.Save();
            }
            return f.picked;
        }
    }

    public AskForm(string url)
    {
        Text = "LinkPilot - open this link in…";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false; MaximizeBox = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;              // it waits for an answer - it must not open behind the app you clicked in
        BackColor = SystemColors.Window;

        Uri u;
        site = Uri.TryCreate(url ?? "", UriKind.Absolute, out u) && (u.Scheme == "http" || u.Scheme == "https") ? RulesPage.SiteOf(url) : "";

        // every category a link can open in, in the order of the list, with what the link log says of it
        var log = LinkLog.Read();
        foreach (var c in Config.Categories.Where(x => !x.IsAsk && x.Works()))
        {
            var mine = log.Where(e => string.Equals(e.OpenedIn, c.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            var row = new Row { Cat = c, Links = mine.Count, Last = mine.Count > 0 ? mine.Max(e => e.When) : DateTime.MinValue };
            try { using (var icon = Tray.IconFor(c)) row.Picture = icon.ToBitmap(); } catch { }
            rows.Add(row);
        }

        // the top: the link - its site in bold, the whole of it small underneath
        var top = new Panel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(14, 10, 14, 4) };
        var host = new Label { Dock = DockStyle.Top, Height = 24, AutoEllipsis = true, UseMnemonic = false,
                               Font = new Font("Segoe UI", 11F, FontStyle.Bold), Text = site.Length > 0 ? site : "This link" };
        var full = new Label { Dock = DockStyle.Top, Height = 18, AutoEllipsis = true, UseMnemonic = false,
                               Font = small, ForeColor = SystemColors.GrayText, Text = url ?? "" };
        new ToolTip().SetToolTip(full, url ?? "");
        top.Controls.Add(full);
        top.Controls.Add(host);      // added last, so it is docked first: at the very top

        // "Open it in:", and the sort on the right
        var sortRow = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(14, 3, 14, 4) };
        sort.Items.AddRange(SortNames);
        int at = Array.IndexOf(Sorts, Config.AskSort);
        sort.SelectedIndex = at >= 0 ? at : 1;
        sort.SelectedIndexChanged += delegate { Fill(); };
        sortRow.Controls.Add(new Label { Text = "Open it in:", Dock = DockStyle.Left, AutoSize = true, Font = bold, Padding = new Padding(0, 4, 0, 0) });
        sortRow.Controls.Add(new Label { Text = "Sort by", Dock = DockStyle.Right, AutoSize = true, ForeColor = SystemColors.GrayText,
                                         Padding = new Padding(0, 4, 4, 0) });
        sortRow.Controls.Add(sort);  // added last, so it is docked first: at the right edge

        // the list: one row per category, up to eight before it scrolls
        var middle = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 6) };
        list.DrawItem += DrawRow;
        list.MouseDoubleClick += (s, e) => Choose(list.IndexFromPoint(e.Location));
        middle.Controls.Add(list);

        // the bottom: Remember, what the keys do, and the buttons
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = Ui.Bar };
        remember.Text = "Remember for " + site + (Config.RulesOn ? "" : "  (rules are off)");
        remember.SetBounds(14, 8, 260, 22);
        remember.Visible = site.Length > 0;
        new ToolTip().SetToolTip(remember, "Adds a rule: links to " + site + " then open in the one you pick, without asking.");
        var keys = new Label { Text = "Enter or 1-9: open   ·   Esc: don't open", AutoSize = true, Font = small,
                               ForeColor = SystemColors.GrayText, Left = 16, Top = site.Length > 0 ? 36 : 24 };
        var open = Ui.Primary("Open", true);
        open.AutoSize = false;
        open.SetBounds(0, 17, 76, Ui.ButtonHeight);
        open.Click += delegate { Choose(list.SelectedIndex); };
        var no = new Button { Text = "Don't open", Width = 88, Height = 28, Top = 17 };
        no.Click += delegate { Close(); };
        bottom.Controls.AddRange(new Control[] { remember, keys, open, no });

        Controls.Add(middle);
        Controls.Add(sortRow);
        Controls.Add(top);
        Controls.Add(bottom);

        ClientSize = new Size(480, top.Height + sortRow.Height + Math.Max(1, Math.Min(8, rows.Count)) * list.ItemHeight + 8 + bottom.Height);
        no.Left = ClientSize.Width - no.Width - 12;
        open.Left = no.Left - open.Width - 6;

        // next to the mouse - where you just clicked the link - and wholly on that screen
        var mouse = Cursor.Position;
        var area = Screen.FromPoint(mouse).WorkingArea;
        Location = new Point(Math.Max(area.Left, Math.Min(mouse.X - 60, area.Right - Width)),
                             Math.Max(area.Top, Math.Min(mouse.Y - 30, area.Bottom - Height)));

        Fill();
        Ui.HandCursors(this);
        Shown += delegate { Activate(); list.Focus(); };
        Disposed += delegate { foreach (var r in rows) r.Dispose(); bold.Dispose(); small.Dispose(); };
    }

    // The rows in the chosen order. Equal ones keep the order of the category list.
    void Fill()
    {
        var keep = list.SelectedItem;
        string how = Sorts[Math.Max(0, sort.SelectedIndex)];
        IEnumerable<Row> order = how == "name" ? rows.OrderBy(x => x.Cat.Name, StringComparer.CurrentCultureIgnoreCase)
                               : how == "recent" ? rows.OrderByDescending(x => x.Last)
                               : rows.OrderByDescending(x => x.Links);
        list.BeginUpdate();
        list.Items.Clear();
        foreach (var r in order) list.Items.Add(r);
        list.EndUpdate();
        if (keep != null) list.SelectedItem = keep;
        if (list.SelectedIndex < 0 && list.Items.Count > 0) list.SelectedIndex = 0;
    }

    void Choose(int index)
    {
        if (index < 0 || index >= list.Items.Count) return;
        picked = ((Row)list.Items[index]).Cat;
        DialogResult = DialogResult.OK;
    }

    // Esc: nothing opens. Enter: the selected row. 1-9: that row, whatever is selected.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!sort.DroppedDown)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == Keys.Enter && !(ActiveControl is Button)) { Choose(list.SelectedIndex); return true; }
            int n = keyData >= Keys.D1 && keyData <= Keys.D9 ? keyData - Keys.D1
                  : keyData >= Keys.NumPad1 && keyData <= Keys.NumPad9 ? keyData - Keys.NumPad1 : -1;
            if (n >= 0) { Choose(n); return true; }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // one row: its number, its icon, its name and what it opens in, and on the right how much it is used
    void DrawRow(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= list.Items.Count) return;
        var row = (Row)list.Items[e.Index];
        var g = e.Graphics;
        var r = e.Bounds;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(selected ? Ui.Picked : list.BackColor)) g.FillRectangle(bg, r);
        if (e.Index < 9)
            TextRenderer.DrawText(g, (e.Index + 1).ToString(), bold, new Rectangle(r.Left, r.Top, 26, r.Height), SystemColors.GrayText,
                                  TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (row.Picture != null) g.DrawImage(row.Picture, r.Left + 28, r.Top + (r.Height - 24) / 2, 24, 24);
        int x = r.Left + 62;
        string used = row.Links == 0 ? "not used yet"
                    : (row.Links == 1 ? "1 link" : row.Links + " links") + "  ·  " + When(row.Last);
        int usedWidth = TextRenderer.MeasureText(g, used, small, Size.Empty, TextFormatFlags.NoPrefix).Width;
        TextRenderer.DrawText(g, row.Cat.Name, bold, new Rectangle(x, r.Top + 3, r.Right - x - usedWidth - 14, 18), SystemColors.WindowText,
                              TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, row.Cat.Shows, Font, new Rectangle(x, r.Top + 20, r.Right - x - 8, 17), SystemColors.GrayText,
                              TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, used, small, new Point(r.Right - usedWidth - 6, r.Top + 5), SystemColors.GrayText, TextFormatFlags.NoPrefix);
        if (e.Index < list.Items.Count - 1)
            using (var line = new Pen(Ui.Line)) g.DrawLine(line, r.Left + 8, r.Bottom - 1, r.Right - 8, r.Bottom - 1);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
    }

    // "14:05" today, "yesterday", else "3 Oct" - as the address dialog's list of sites says it
    static string When(DateTime t)
    {
        return t.Date == DateTime.Today ? t.ToString("HH:mm") : t.Date == DateTime.Today.AddDays(-1) ? "yesterday" : t.ToString("d MMM");
    }
}
