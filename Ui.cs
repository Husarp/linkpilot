// Small pieces shared by the windows: the colours and sizes, buttons, the header of every tab, the one
// (i) per tab that explains everything on it, and headings that can carry it. The explanations live
// there instead of in long texts across the windows.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;

static class Ui
{
    // The colours, in one place.
    public static readonly Color Accent = Color.FromArgb(0, 103, 192);
    public static readonly Color AccentDark = Color.FromArgb(0, 90, 158);    // headings on a soft blue box
    public static readonly Color Bar = Color.FromArgb(240, 240, 240);        // the strip along a window's bottom
    public static readonly Color Picked = Color.FromArgb(204, 228, 247);     // a selected line
    public static readonly Color Soft = Color.FromArgb(243, 247, 252);       // a box set apart: an example, a note
    public static readonly Color HelpBack = Color.FromArgb(236, 243, 254);   // the (i)'s panel
    public static readonly Color Ok = Color.FromArgb(16, 124, 65), OkBack = Color.FromArgb(232, 246, 237);
    public static readonly Color Warn = Color.FromArgb(90, 60, 0), WarnBack = Color.FromArgb(255, 244, 206);
    public static readonly Color Line = Color.FromArgb(235, 235, 235);       // thin lines between things
    public static readonly Color Muted = Color.FromArgb(70, 70, 70);         // text under a choice

    // The sizes: the space inside every tab, between groups, and the height of buttons.
    public const int Edge = 12, Gap = 8, ButtonHeight = 28, BigButtonHeight = 34;

    // The padding of every tab page.
    public static Padding PagePadding { get { return new Padding(Edge, 8, Edge, 8); } }

    // A normal button: the same height everywhere, a little wider than its text.
    public static System.Windows.Forms.Button Button(string text, EventHandler click)
    {
        var b = new System.Windows.Forms.Button { Text = text, AutoSize = true, Size = new Size(80, ButtonHeight), MinimumSize = new Size(80, ButtonHeight),
                             Padding = new Padding(8, 0, 8, 0) };
        if (click != null) b.Click += click;
        return b;
    }

    // The blue button that goes on: big in the setup, small (a normal button's height) in a dialog.
    public static System.Windows.Forms.Button Primary(string text, bool small = false)
    {
        var b = new System.Windows.Forms.Button { Text = text, AutoSize = true, Height = small ? ButtonHeight : BigButtonHeight,
                             MinimumSize = new Size(small ? 80 : 0, small ? ButtonHeight : BigButtonHeight),
                             Padding = small ? new Padding(10, 0, 10, 0) : new Padding(14, 0, 14, 0),
                             Font = new Font("Segoe UI", small ? 9F : 9.75F, FontStyle.Bold), BackColor = Accent, ForeColor = Color.White,
                             FlatStyle = FlatStyle.Flat };
        b.FlatAppearance.BorderSize = 0;
        // turned off, it is grey - a flat button keeps its blue otherwise, and looks as if it would work
        b.EnabledChanged += delegate
        {
            b.BackColor = b.Enabled ? Accent : SystemColors.Control;
            b.ForeColor = b.Enabled ? Color.White : SystemColors.GrayText;
        };
        return b;
    }

    // The top of every tab: its name with the (i) beside it, one plain line under them, and a thin
    // line along the bottom.
    public static Panel PageHeader(string title, string line, TabHelp help)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 54 };
        var top = Heading(title, help, 11F);
        top.Location = new Point(0, 0);
        p.Controls.Add(top);
        p.Controls.Add(new Label { Text = line, AutoSize = true, ForeColor = SystemColors.GrayText, UseMnemonic = false, Location = new Point(3, 29) });
        p.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Line });
        return p;
    }

    // What a list shows while it is empty: a bold line, a plain one, and a way on (any of them may be
    // null). Laid over the list, its content kept in the middle.
    public static Panel Empty(string headline, string line, params Control[] ways)
    {
        var p = new Panel { BackColor = SystemColors.Window };
        var column = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
                                           AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = SystemColors.Window };
        if (headline != null)
            column.Controls.Add(new Label { Text = headline, AutoSize = true, Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
                                            Margin = new Padding(3, 0, 3, 4) });
        var says = line == null ? null : new Label { Text = line, AutoSize = true, MaximumSize = new Size(360, 0), ForeColor = SystemColors.GrayText,
                                                     UseMnemonic = false, Margin = new Padding(3, 0, 3, 10) };
        if (says != null) column.Controls.Add(says);
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        foreach (var w in ways) if (w != null) row.Controls.Add(w);
        if (row.Controls.Count > 0) column.Controls.Add(row);
        p.Controls.Add(column);
        LayoutEventHandler centre = delegate
        {
            // the line wraps to fit a narrow list
            var most = new Size(Math.Max(120, Math.Min(360, p.ClientSize.Width - 24)), 0);
            if (says != null && says.MaximumSize != most) says.MaximumSize = most;
            var size = column.GetPreferredSize(Size.Empty);
            column.Location = new Point(Math.Max(0, (p.ClientSize.Width - size.Width) / 2), Math.Max(8, (p.ClientSize.Height - size.Height) / 3));
        };
        p.Layout += centre;
        return p;
    }

    // "Categories (i)" - bold text, with the tab's (i) after it if it has one
    public static FlowLayoutPanel Heading(string text, TabHelp help, float size = 9.75F)
    {
        var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false,
                                        Margin = new Padding(3, 0, 3, 4), BackColor = Color.Transparent };
        row.Controls.Add(new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", size, FontStyle.Bold),
                                     Margin = new Padding(0, 1, 0, 0) });
        if (help != null) row.Controls.Add(help);
        return row;
    }

    // a column title in a table - small and grey
    public static Label Caption(string text)
    {
        return new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = SystemColors.GrayText,
                           Margin = new Padding(3, 2, 3, 2) };
    }

    // Everything you can click shows the hand cursor: every button and tick box in this window, and
    // any added to it later - tabs are built when opened, the switch row after every change.
    public static void HandCursors(Control c)
    {
        if (c is ButtonBase) c.Cursor = Cursors.Hand;   // buttons, tick boxes, choices
        c.ControlAdded += (s, e) => HandCursors(e.Control);
        foreach (Control child in c.Controls) HandCursors(child);
    }

    // A heading docked across the top of a panel.
    public static Panel Section(string text, TabHelp help = null)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(2, 6, 0, 0) };
        p.Controls.Add(Heading(text, help));
        return p;
    }
}

// The tab's (i) - drawn like the Android app's: a blue ring with an "i". Clicking it opens a panel
// under it that explains every section of the tab: each section's name in bold, then short points,
// each starting with a bold word ("Which wins: ..."). A click on the (i) again, anywhere else, or Esc, closes it.
//
//     new TabHelp("The Rules tab", "# Rules", "Which wins: ...", "No match: ...", "# App rules", ...)
//
// Lines starting with "# " are section names; the others are points.
class TabHelp : Control
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool HideCaret(IntPtr hwnd);
    static readonly Color Back = Ui.HelpBack;
    readonly string title;
    readonly string[] lines;
    bool over;
    bool closedByClick;   // the panel was just closed by a click on this (i)

    public TabHelp(string title, params string[] lines)
    {
        this.title = title;
        this.lines = lines;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Size = new Size(22, 22);
        Margin = new Padding(6, 0, 0, 0);
        Cursor = Cursors.Hand;
        TabStop = false;
        AccessibleRole = AccessibleRole.HelpBalloon;
        AccessibleName = "How this works";
        AccessibleDescription = string.Join(" ", lines.Select(l => l.TrimStart('#', ' ')));
    }

    protected override void OnMouseEnter(EventArgs e) { over = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { over = false; closedByClick = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e)
    {
        if (closedByClick) closedByClick = false;   // this click just closed the panel: it stays closed
        else Open();
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var ring = new RectangleF(1.5F, 1.5F, Width - 4F, Height - 4F);
        Color ink = over ? Color.White : Ui.Accent;
        if (over) using (var fill = new SolidBrush(Ui.Accent)) g.FillEllipse(fill, ring);
        using (var edge = new Pen(Ui.Accent, 1.7F)) g.DrawEllipse(edge, ring);
        float cx = (Width - 1) / 2F;
        using (var dot = new SolidBrush(ink)) g.FillEllipse(dot, cx - 1.4F, Height * 0.26F, 2.8F, 2.8F);
        using (var bar = new Pen(ink, 2.2F) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(bar, cx, Height * 0.45F, cx, Height * 0.71F);
    }

    void Open()
    {
        var panel = BuildPanel();
        var host = new ToolStripControlHost(panel) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false };
        var drop = new ToolStripDropDown { Padding = Padding.Empty, DropShadowEnabled = true, AutoClose = true };
        drop.Items.Add(host);
        Fill(panel);   // only now: the drop-down hands its own font down to what it holds
        host.Size = panel.Size;
        // a click on this (i) while the panel is open closes it, as any click outside does - and the
        // same click, reaching the (i) next, must not open it again at once
        drop.Closing += (s, e) =>
        {
            closedByClick = e.CloseReason == ToolStripDropDownCloseReason.AppClicked && RectangleToScreen(ClientRectangle).Contains(Cursor.Position);
        };
        drop.Closed += delegate { BeginInvoke((MethodInvoker)drop.Dispose); };
        drop.Show(this, new Point(0, Height + 4));   // moved to stay on screen if needed
    }

    // The panel: the text laid out by a read-only rich text box, which does the wrapping and the bold.
    // The box has a font of its own, so no font handed down from outside can reach it - one would
    // reset all its formatting, bold and colours included, and leave its height measured for the old
    // text. (Separate from Open so a test can draw it without showing it.)
    internal Panel BuildPanel()
    {
        var box = new RichTextBox { BorderStyle = BorderStyle.None, ReadOnly = true, BackColor = Back, Width = 470, Height = 40,
                                    ScrollBars = RichTextBoxScrollBars.None, DetectUrls = false, TabStop = false,
                                    Cursor = Cursors.Arrow, Location = new Point(18, 14), Font = new Font("Segoe UI", 9.5F) };
        box.ContentsResized += (s, e) => box.Height = e.NewRectangle.Height + 4;
        box.GotFocus += delegate { HideCaret(box.Handle); };
        var panel = new Panel { BackColor = Back };
        panel.Controls.Add(box);
        panel.CreateControl();
        box.CreateControl();
        Fill(panel);
        return panel;
    }

    // The text, and the panel sized to it.
    internal void Fill(Panel panel)
    {
        var box = panel.Controls.OfType<RichTextBox>().First();
        box.ScrollBars = RichTextBoxScrollBars.None;
        box.Rtf = Rtf();
        int most = Screen.FromControl(this).WorkingArea.Height * 3 / 4;
        if (box.Height > most) { box.Height = most; box.ScrollBars = RichTextBoxScrollBars.Vertical; }
        panel.Size = new Size(box.Width + 36, box.Height + 28);
    }

    string Rtf()
    {
        var sb = new StringBuilder(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}{\colortbl ;\red0\green83\blue166;\red32\green33\blue36;}");
        sb.Append(@"\f0\cf2\fs28\b ").Append(Esc(title)).Append(@"\b0\par");
        foreach (string line in lines)
        {
            if (line.StartsWith("# "))
            {
                sb.Append(@"\pard\sb220\sa40\cf1\fs21\b ").Append(Esc(line.Substring(2))).Append(@"\b0\cf2\par");
                continue;
            }
            int colon = line.IndexOf(": ");
            sb.Append(@"\pard\fi-260\li260\tx260\sb70\fs19 \bullet\tab ");
            if (colon > 0 && colon <= 40)
                sb.Append(@"\b ").Append(Esc(line.Substring(0, colon + 1))).Append(@"\b0  ").Append(Esc(line.Substring(colon + 2)));   // one space ends \b0, the other is the text's
            else sb.Append(Esc(line));
            sb.Append(@"\par");
        }
        return sb.Append("}").ToString();
    }

    static string Esc(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (c == '\\' || c == '{' || c == '}') sb.Append('\\').Append(c);
            else if (c > 127) sb.Append(@"\u").Append((int)(short)c).Append('?');
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
