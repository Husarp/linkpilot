// The short guide: a few optional screens after "You're all set" (Setup.cs), each one decision with
// a good answer already filled in - press the blue button on every screen and the setup is good.
//
//   5  Your categories   - one tick per browser profile on this PC
//   6  How you switch    - keyboard shortcuts, smart queue, icons in the dock
//   7  Ask every time    - what happens to a link no rule decides (only with 2 categories or more)
//   8  Rules for your apps - the apps that opened links, each with a guessed category, none ticked
//   9  Where things live - one line per tab
//
// Next saves what the screen shows; Skip saves nothing. A screen that does not apply is passed over.
// Each screen is built when it is shown, since it follows what the one before it made. It can be run
// again from About & updates.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

partial class SwitchForm
{
    FlowLayoutPanel guideCategories, guideSwitch, guideAsk, guideRules, guideTour;
    readonly Stack<int> guideTrail = new Stack<int>();   // the guide's screens shown before this one, for Back
    int guideStep;

    // profile names nobody chose - the browser's own name says more
    static readonly string[] Generic = { "default", "default profile", "default-release", "your chrome" };

    IEnumerable<FlowLayoutPanel> GuidePages()
    {
        guideCategories = Column(); guideSwitch = Column(); guideAsk = Column(); guideRules = Column(); guideTour = Column();
        return new[] { guideCategories, guideSwitch, guideAsk, guideRules, guideTour };
    }

    // From "You're all set": the setup counts as done from here on, so closing the window just ends it.
    void StartGuide()
    {
        if (!Config.SetupDone) { Config.SetupDone = true; Save(); }
        GuideTo(NextGuideStep(4, 0));
    }

    // From About & updates: the guide again, without the setup's steps.
    public void ShowGuide()
    {
        if (setup == null) BuildSetup();
        foreach (var c in mainScreen) c.Visible = false;
        guideTrail.Clear();
        guideStep = 0;
        SetupStep(NextGuideStep(4, 0));
        setup.Visible = true;
    }

    void GuideTo(int step)
    {
        if (guideStep >= 4) guideTrail.Push(guideStep);
        SetupStep(step);
    }

    void GuideBack() { SetupStep(guideTrail.Count > 0 ? guideTrail.Pop() : 4); }

    // The first screen after this one that applies. more: categories about to be made (counted for the
    // screens that need two).
    int NextGuideStep(int from, int more)
    {
        for (int s = from + 1; s < 9; s++) if (GuideApplies(s, more)) return s;
        return 9;
    }

    bool GuideApplies(int step, int more)
    {
        int working = Config.Categories.Count(c => !c.IsAsk && c.Works()) + more;
        if (step == 5) return browsers.Count > 0;
        if (step == 6) return working >= 1;                       // something to switch to
        if (step == 7) return working >= 2;                       // Ask needs somewhere to send links
        if (step == 8) return Config.Categories.Count + more >= 2;   // with one, a rule changes nothing
        return true;
    }

    // "Optional · 2 of 4": the screens shown so far, this one, and those still to come.
    string Counted(int step, int more)
    {
        int n = guideTrail.Count(x => x >= 5) + 1;
        int of = n;
        for (int s = step + 1; s < 9; s++) if (GuideApplies(s, more)) of++;
        return "Optional  ·  " + n + " of " + of;
    }

    void BuildGuidePage(int step)
    {
        var page = setupPages[step - 1];
        page.SuspendLayout();
        foreach (var old in page.Controls.Cast<Control>().ToList()) { DropImages(old); old.Dispose(); }
        if (step == 5) GuideCategories(page);
        else if (step == 6) GuideSwitch(page);
        else if (step == 7) GuideAsk(page);
        else if (step == 8) GuideRules(page);
        else GuideTour(page);
        page.ResumeLayout();
    }

    static void DropImages(Control c)
    {
        var pic = c as PictureBox;
        if (pic != null && pic.Image != null) pic.Image.Dispose();
        foreach (Control child in c.Controls) DropImages(child);
    }

    // Back (when there is a screen to go back to) and Skip on the left, Next on the right. ready: when it
    // says no, Next stays on the screen.
    Control GuideNav(int step, string nextText, Action keep, Func<bool> ready = null)
    {
        var next = Ui.Primary(nextText);
        next.Click += delegate
        {
            if (ready != null && !ready()) return;
            keep();
            GuideTo(NextGuideStep(step, 0));
        };
        var left = new List<Control>();
        if (guideTrail.Count > 0) left.Add(Link("←  Back", GuideBack));
        left.Add(Link("Skip", () => GuideTo(NextGuideStep(step, 0))));
        return NavRow(next, left.ToArray());
    }

    static Label Grey(string text, int below)
    {
        return new Label { Text = text, AutoSize = true, MaximumSize = new Size(SetupWidth, 0), ForeColor = SystemColors.GrayText,
                           UseMnemonic = false, Margin = new Padding(0, 0, 0, below) };
    }

    static CheckBox Tick(string text, bool on)
    {
        return new CheckBox { Text = text, AutoSize = true, Checked = on, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                              Margin = new Padding(0, 6, 0, 0) };
    }

    static Label Under(string text)
    {
        return new Label { Text = text, AutoSize = true, MaximumSize = new Size(SetupWidth - 20, 0), UseMnemonic = false,
                           Font = new Font("Segoe UI", 9.75F), ForeColor = Ui.Muted, Margin = new Padding(20, 0, 0, 10) };
    }

    static Bitmap Small(Category c, int size)
    {
        try { using (var icon = Tray.IconFor(c)) using (var big = icon.ToBitmap()) return new Bitmap(big, size, size); }
        catch { return null; }
    }

    // ---- 5: your categories ---------------------------------------------------------------------

    class ProfileRow { public Browser B; public Profile P; public CheckBox Tick; public TextBox Name; public bool Used; }

    void GuideCategories(FlowLayoutPanel page)
    {
        var rows = new List<ProfileRow>();
        var taken = new HashSet<string>(Config.Categories.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
        int ticked = 0;

        var list = new TableLayoutPanel { ColumnCount = 4, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        list.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        list.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        list.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        list.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach (var b in browsers)
        {
            // a browser with profiles of its own: one row each; without: one row for the browser
            var profiles = b.Profiles.Where(p => p.Args.Length > 0).ToList();
            if (profiles.Count == 0) profiles.Add(b.Profiles.FirstOrDefault() ?? new Profile { Name = b.Name, Args = "" });
            foreach (var p in profiles)
            {
                var row = new ProfileRow { B = b, P = p };
                var users = Config.Categories.Where(c => string.Equals(c.Exe, b.Exe, StringComparison.OrdinalIgnoreCase) && c.Args == p.Args)
                                             .Select(c => c.Name).ToList();
                row.Used = users.Count > 0;
                string own = OwnName(p);
                bool generic = p.Args.Length == 0 || IsGeneric(own);
                string name = generic ? ShortName(b.Name) : own;
                string unique = name;
                for (int n = 2; taken.Contains(unique); n++) unique = name + " " + n;

                bool on = row.Used || (!generic && ticked < 3);
                if (on && !row.Used) { ticked++; taken.Add(unique); }
                row.Tick = new CheckBox { AutoSize = true, Checked = on, Enabled = !row.Used, Margin = new Padding(0, 6, 4, 0) };
                string key = TreeIcon(b.Exe, p.Args.Length > 0 ? p.Args : null) ?? TreeIcon(b.Exe, null);
                var pic = new PictureBox { Size = new Size(20, 20), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 4, 8, 0),
                                           Image = key != null ? treeIcons.Images[key] : null };
                var what = new Label { Text = p.Args.Length == 0 ? b.Name : b.Name + " — " + p.Name, AutoEllipsis = true, UseMnemonic = false,
                                       Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 2, 8, 0),
                                       ForeColor = row.Used ? SystemColors.GrayText : SystemColors.WindowText };
                var tick = row.Tick;
                what.Click += delegate { if (tick.Enabled) tick.Checked = !tick.Checked; };
                list.Controls.Add(row.Tick);
                list.Controls.Add(pic);
                list.Controls.Add(what);
                if (row.Used)
                    list.Controls.Add(new Label { Text = "←  " + string.Join(", ", users), AutoSize = false, AutoEllipsis = true, Size = new Size(200, 18),
                                                  ForeColor = SystemColors.GrayText, UseMnemonic = false, Margin = new Padding(0, 6, 0, 0) });
                else
                {
                    var cell = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
                    cell.Controls.Add(new Label { Text = "Name:", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 6, 4, 0) });
                    row.Name = new TextBox { Text = unique, Width = 150, Enabled = on, Margin = new Padding(0, 2, 0, 2) };
                    var box = row.Name;
                    row.Tick.CheckedChanged += delegate { box.Enabled = tick.Checked; };
                    cell.Controls.Add(row.Name);
                    list.Controls.Add(cell);
                }
                rows.Add(row);
            }
        }

        page.Controls.Add(Title("Your categories", Counted(5, ticked)));
        page.Controls.Add(Text_("One category per browser profile you use. Change them later on the Categories tab."));
        if (Config.Categories.Count > 0)
        {
            var chips = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(SetupWidth, 0), Margin = new Padding(0, 0, 0, 10) };
            chips.Controls.Add(new Label { Text = "Already set up:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
            foreach (var c in Config.Categories)
            {
                bool live = string.Equals(c.Name, Config.Active, StringComparison.OrdinalIgnoreCase);
                var chip = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Ui.Soft, Padding = new Padding(4, 3, 6, 3),
                                                 Margin = new Padding(0, 0, 6, 4) };
                chip.Controls.Add(new PictureBox { Size = new Size(16, 16), SizeMode = PictureBoxSizeMode.Zoom, Image = Small(c, 16),
                                                   Margin = new Padding(0, 1, 4, 0) });
                chip.Controls.Add(new Label { Text = c.Name, AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 1, 0, 0) });
                if (live) chip.Controls.Add(new Label { Text = "live", AutoSize = true, ForeColor = Ui.Accent, Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                                                        Margin = new Padding(4, 2, 0, 0) });
                chips.Controls.Add(chip);
            }
            page.Controls.Add(chips);
        }
        page.Controls.Add(list);
        page.Controls.Add(Grey("Tip: names like Work, Home or School make switching easy to read.", 4));
        page.Controls.Add(GuideNav(5, "Next  →", delegate
        {
            foreach (var row in rows.Where(r => !r.Used && r.Tick.Checked))
                AddCategory(row.Name.Text.Trim(), row.B.Exe, row.P.Args, row.P.Args.Length == 0 ? row.B.Name : row.B.Name + " — " + row.P.Name);
            Save();
            Reload();
        }, delegate
        {
            // every ticked one needs a name of its own, as on the phone
            var names = new HashSet<string>(Config.Categories.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows.Where(r => !r.Used && r.Tick.Checked))
            {
                string name = row.Name.Text.Trim();
                string problem = name.Length == 0 ? "Give each ticked one a name."
                               : !names.Add(name) ? "There is a category called “" + name + "” already - give this one another name." : null;
                if (problem == null) continue;
                MessageBox.Show(this, problem, "LinkPilot");
                row.Name.Focus();
                row.Name.SelectAll();
                return false;
            }
            return true;
        }));
    }

    // "Work" for "Work  (Profile 2)" - the name the person gave, without the folder it is kept in.
    static string OwnName(Profile p)
    {
        int at = p.Name.IndexOf("  (", StringComparison.Ordinal);
        return at > 0 ? p.Name.Substring(0, at) : p.Name;
    }

    static bool IsGeneric(string name)
    {
        string n = name.Trim().ToLowerInvariant();
        return Generic.Contains(n) || Regex.IsMatch(n, @"^(person|profile) \d+$");
    }

    // "Firefox", not "Mozilla Firefox" - as the first category is named
    static string ShortName(string browser)
    {
        foreach (string maker in new[] { "Mozilla ", "Google ", "Microsoft " })
            if (browser.StartsWith(maker, StringComparison.OrdinalIgnoreCase) && browser.Length > maker.Length) return browser.Substring(maker.Length);
        return browser;
    }

    // ---- 6: how you switch ----------------------------------------------------------------------

    void GuideSwitch(FlowLayoutPanel page)
    {
        page.Controls.Add(Title("How you switch", Counted(6, 0)));
        page.Controls.Add(Text_("Switch categories without opening this window."));

        var keysOn = Tick("Turn on keyboard shortcuts", true);
        page.Controls.Add(keysOn);
        var keys = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Margin = new Padding(20, 2, 0, 2) };
        foreach (var c in Config.Categories) KeyLine(keys, c.Name, c.HotKey, c.KeyOn);
        KeyLine(keys, "Next category", Config.NextKey, Config.NextOn);
        page.Controls.Add(keys);
        page.Controls.Add(Under("Change any of them later on the Shortcuts tab."));

        var smart = Tick("Smart queue", Config.SmartQueue);
        page.Controls.Add(smart);
        page.Controls.Add(Under("Next goes back to the category used before - one press flips back, like Alt+Tab."));

        var working = Config.Categories.Where(c => c.Works()).ToList();
        int real = working.Count(c => !c.IsAsk);
        var dock = Tick("In the dock", real >= 2 && real <= 4);
        page.Controls.Add(dock);
        page.Controls.Add(Under("Give each category its own icon next to the clock - one click switches."));

        page.Controls.Add(GuideNav(6, "Next  →", delegate
        {
            Config.ShortcutsOn = keysOn.Checked;
            if (keysOn.Checked)
            {
                // a key another program holds stays filled in, but is not turned on
                foreach (var c in Config.Categories) if (c.KeyOn && KeyProblem(c.HotKey) == Taken) c.KeyOn = false;
                if (Config.NextOn && KeyProblem(Config.NextKey) == Taken) Config.NextOn = false;
                if (Config.PrevOn && KeyProblem(Config.PrevKey) == Taken) Config.PrevOn = false;
                if (Config.RulesKeyOn && KeyProblem(Config.RulesKey) == Taken) Config.RulesKeyOn = false;
            }
            Config.SmartQueue = smart.Checked;
            if (dock.Checked) foreach (var c in working) c.InDock = true;
            Save();
            Reload();
        }));
    }

    const string Taken = "taken by another program";

    // What is in the way of a shortcut, as the Shortcuts tab says it - or null when it is ready.
    string KeyProblem(string keys)
    {
        if (keys.Length == 0) return null;
        try
        {
            if (Shortcut.TypesCharacter(keys) != null) return "types a character";
            if (ShortcutFree != null && !ShortcutFree(keys)) return Taken;
        }
        catch { }
        return null;
    }

    // on: unticked on the Shortcuts tab, a key shows as off - the guide does not turn it back on
    void KeyLine(TableLayoutPanel table, string name, string keys, bool on)
    {
        string problem = KeyProblem(keys);
        table.Controls.Add(new Label { Text = name, AutoSize = true, UseMnemonic = false, Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                                       Margin = new Padding(0, 2, 14, 2) });
        table.Controls.Add(new Label { Text = keys.Length > 0 ? keys : "(none)", AutoSize = true, Margin = new Padding(0, 2, 14, 2) });
        if (keys.Length > 0 && !on)
            table.Controls.Add(new Label { Text = "off - turn it on on the Shortcuts tab", AutoSize = true, ForeColor = SystemColors.GrayText,
                                           Margin = new Padding(0, 2, 0, 2) });
        else
            table.Controls.Add(new Label { Text = keys.Length == 0 ? "" : problem != null ? "⚠ " + problem : "✓ ready", AutoSize = true,
                                           ForeColor = problem != null ? Ui.Warn : Ui.Ok, Margin = new Padding(0, 2, 0, 2) });
    }

    // ---- 7: ask every time ----------------------------------------------------------------------

    void GuideAsk(FlowLayoutPanel page)
    {
        var current = Config.Current();
        page.Controls.Add(Title("When no rule decides", Counted(7, 0)));
        page.Controls.Add(Text_("A link no rule sends somewhere goes to one place:"));

        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 6) };
        var choices = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0) };
        var live = new RadioButton { Text = "Open in the live category", AutoSize = true, Checked = current == null || !current.IsAsk,
                                     Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Margin = new Padding(0, 6, 0, 0) };
        var ask = new RadioButton { Text = "Ask every time", AutoSize = true, Checked = !live.Checked,
                                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Margin = new Padding(0, 6, 0, 0) };
        var startLive = new CheckBox { Text = "Start with it live", AutoSize = true, Checked = true, Enabled = ask.Checked, Margin = new Padding(20, 0, 0, 8) };
        ask.CheckedChanged += delegate { startLive.Enabled = ask.Checked; };
        choices.Controls.Add(live);
        choices.Controls.Add(Narrow(Under("You switch it yourself: from the bottom row, the dock or a shortcut.")));
        choices.Controls.Add(ask);
        choices.Controls.Add(Narrow(Under("A small window next to the mouse asks which category. It can remember the site for next time.")));
        choices.Controls.Add(startLive);
        row.Controls.Add(choices);
        row.Controls.Add(AskPicture());
        page.Controls.Add(row);

        page.Controls.Add(GuideNav(7, "Next  →", delegate
        {
            if (!ask.Checked)
            {
                // Ask every time was live: a real category takes over
                var now = Config.Current();
                var first = Config.Categories.FirstOrDefault(x => !x.IsAsk && x.Works());
                if (now == null || !now.IsAsk || first == null) return;
                Config.Active = first.Name;
                Save();
                Reload();
                return;
            }
            // the same as picking Ask every time in the list of browsers - or the one already made
            var c = Config.Categories.FirstOrDefault(x => x.IsAsk);
            if (c == null)
            {
                string name = "Ask";
                for (int n = 2; Config.Categories.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)); n++) name = "Ask " + n;
                c = AddCategory(name, Category.AskExe, "", "Ask every time");
            }
            if (startLive.Checked) Config.Active = c.Name;
            Save();
            Reload();
        }));
    }

    static Label Narrow(Label l) { l.MaximumSize = new Size(330, 0); return l; }

    // What the Ask every time window looks like - drawn, not the real one, which would pop up.
    Control AskPicture()
    {
        var cats = Config.Categories.Where(c => !c.IsAsk && c.Works()).Take(3).ToList();
        var pictures = cats.Select(c => Small(c, 20)).ToList();
        var p = new Panel { Size = new Size(270, 58 + cats.Count * 34 + 30), Margin = new Padding(24, 8, 0, 0), BackColor = SystemColors.Window };
        p.Paint += (s, e) =>
        {
            var g = e.Graphics;
            using (var edge = new Pen(Color.FromArgb(190, 190, 190))) g.DrawRectangle(edge, 0, 0, p.Width - 1, p.Height - 1);
            using (var big = new Font("Segoe UI", 10F, FontStyle.Bold))
            using (var bold = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (var small = new Font("Segoe UI", 8F))
            {
                TextRenderer.DrawText(g, "github.com", big, new Point(10, 8), SystemColors.WindowText);
                TextRenderer.DrawText(g, "Open it in:", bold, new Point(10, 34), SystemColors.WindowText);
                for (int i = 0; i < cats.Count; i++)
                {
                    int y = 56 + i * 34;
                    if (i == 0) using (var pick = new SolidBrush(Ui.Picked)) g.FillRectangle(pick, 4, y - 2, p.Width - 8, 32);
                    TextRenderer.DrawText(g, (i + 1).ToString(), bold, new Point(10, y + 7), SystemColors.GrayText);
                    if (pictures[i] != null) g.DrawImage(pictures[i], 28, y + 4, 20, 20);
                    TextRenderer.DrawText(g, cats[i].Name, bold, new Rectangle(56, y, p.Width - 64, 16), SystemColors.WindowText, TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(g, cats[i].Shows, small, new Rectangle(56, y + 15, p.Width - 64, 14), SystemColors.GrayText, TextFormatFlags.EndEllipsis);
                }
                using (var bar = new SolidBrush(Ui.Bar)) g.FillRectangle(bar, 1, p.Height - 27, p.Width - 2, 26);
                TextRenderer.DrawText(g, "Remember for github.com", small, new Point(10, p.Height - 21), SystemColors.WindowText);
            }
        };
        p.Disposed += delegate { foreach (var b in pictures) if (b != null) b.Dispose(); };
        return p;
    }

    // ---- 8: rules for your apps -----------------------------------------------------------------

    class AppRow { public AppCatalog.App App; public CheckBox Tick; public ComboBox To; }

    void GuideRules(FlowLayoutPanel page)
    {
        page.Controls.Add(Title("Rules for your apps", Counted(8, 0)));
        page.Controls.Add(Text_("Links from these apps can always go to one category."));

        var rows = new List<AppRow>();
        var apps = SuggestedApps();
        var list = new TableLayoutPanel { ColumnCount = 4, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        list.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        list.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        list.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        list.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        foreach (var a in apps)
        {
            var row = new AppRow { App = a };
            row.Tick = new CheckBox { AutoSize = true, Margin = new Padding(0, 7, 4, 0) };
            var pic = new PictureBox { Size = new Size(20, 20), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 5, 8, 0) };
            var name = new Label { Text = a.Name, AutoEllipsis = true, UseMnemonic = false, Dock = DockStyle.Fill,
                                   TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 2, 8, 0) };
            var tick = row.Tick;
            name.Click += delegate { tick.Checked = !tick.Checked; };
            var cell = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            cell.Controls.Add(new Label { Text = "→", AutoSize = true, Margin = new Padding(0, 7, 4, 0) });
            row.To = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Margin = new Padding(0, 3, 0, 3) };
            row.To.Items.AddRange(Config.Categories.Select(c => (object)c.Name).ToArray());
            row.To.SelectedItem = GuessCategory(a);
            if (row.To.SelectedItem == null && row.To.Items.Count > 0) row.To.SelectedIndex = 0;
            cell.Controls.Add(row.To);
            list.Controls.Add(row.Tick);
            list.Controls.Add(pic);
            list.Controls.Add(name);
            list.Controls.Add(cell);
            rows.Add(row);
            // the app's icon: finding its program can take a moment, so away from the screen
            string exes = a.Exes;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Bitmap found = null;
                foreach (string exe in exes.Split(';'))
                {
                    string path = AppIcons.PathOf(exe.Trim());
                    if (path == null) continue;
                    try { using (var icon = Icon.ExtractAssociatedIcon(path)) found = icon.ToBitmap(); break; } catch { }
                }
                if (found != null) ui.Post(delegate { if (pic.IsDisposed) found.Dispose(); else pic.Image = found; }, null);
            });
        }
        if (apps.Count == 0)
            page.Controls.Add(Grey("No apps found yet - after a few links, Add apps… on the Rules tab lists the ones that opened them.", 6));
        else
        {
            page.Controls.Add(list);
            page.Controls.Add(Grey("Nothing is ticked - tick the ones you want.", 4));
        }
        var added = Grey("", 4);
        added.ForeColor = Ui.Ok;
        added.Visible = false;
        var more = Link("More apps…", delegate { MoreApps(added); });
        more.Margin = new Padding(0, 2, 0, 4);
        page.Controls.Add(more);
        page.Controls.Add(added);

        page.Controls.Add(GuideNav(8, "Next  →", delegate
        {
            foreach (var row in rows.Where(r => r.Tick.Checked && r.To.SelectedItem != null))
                RulesPage.Place(new Rule { ByApp = true, Label = row.App.Name, Match = row.App.Exes, Category = (string)row.To.SelectedItem },
                                null, older => true);
            Save();
            Reload();
        }));
    }

    // The app list (Add apps…), as on the Rules tab. What it adds is saved at once.
    void MoreApps(Label said)
    {
        using (var picker = new AppPicker())
        {
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            var names = new List<string>();
            foreach (var a in picker.Chosen)
            {
                var r = new Rule { ByApp = true, Label = a.Name, Match = a.Exes, Category = picker.Target };
                bool placed = RulesPage.Place(r, null, older => MessageBox.Show(this,
                    older.Label + " already has a rule: its links go to " + older.Category + ".\nReplace it?", "LinkPilot",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
                if (placed) names.Add(a.Name);
            }
            Save();
            Reload();
            if (names.Count > 0) { said.Text = "✓ Added: " + string.Join(", ", names) + "  →  " + picker.Target; said.Visible = true; }
        }
    }

    // Up to six apps that open links here, without a rule yet: those that did lately, those with the
    // most links in the log, then known apps running now. Browsers and LinkPilot are left out.
    List<AppCatalog.App> SuggestedApps()
    {
        var found = new List<AppCatalog.App>();
        var skip = new HashSet<string>(browsers.Select(b => Path.GetFileName(b.Exe)), StringComparer.OrdinalIgnoreCase) { "BrowserSwitch.exe" };
        foreach (var r in Config.Rules.Where(r => r.ByApp)) foreach (string exe in r.Match.Split(';')) skip.Add(exe.Trim());
        Action<string> offer = exe =>
        {
            if (found.Count >= 6 || string.IsNullOrEmpty(exe) || skip.Contains(exe)) return;
            var a = AppCatalog.ForExe(exe) ?? new AppCatalog.App { Name = Path.GetFileNameWithoutExtension(exe), Group = "", Exes = exe };
            if (a.Exes.Split(';').Any(x => skip.Contains(x.Trim()))) return;
            foreach (string x in a.Exes.Split(';')) skip.Add(x.Trim());
            found.Add(a);
        };
        foreach (string exe in Router.Recent()) offer(exe);
        foreach (var g in LinkLog.Read().Where(e => e.From.Length > 0 && LinkLog.WentToBrowser(e))
                                        .GroupBy(e => e.From, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()))
            offer(g.Key);
        if (found.Count < 6)
        {
            // known apps only: no helper programs nobody has heard of. File Explorer and Settings are
            // always running, so they say nothing.
            var running = new List<string>();
            try
            {
                foreach (var p in Process.GetProcesses()) using (p) try { running.Add(p.ProcessName + ".exe"); } catch { }
            }
            catch { }
            foreach (var a in AppCatalog.All)
                if (a.Exes.Split(';').Any(x => running.Contains(x, StringComparer.OrdinalIgnoreCase)) &&
                    !a.Exes.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) && !a.Exes.Equals("SystemSettings.exe", StringComparison.OrdinalIgnoreCase))
                    offer(a.Exes.Split(';')[0]);
        }
        return found;
    }

    // Where an app's links probably belong, by its kind and the categories' names - else the live one.
    static string GuessCategory(AppCatalog.App a)
    {
        bool workChat = new[] { "Microsoft Teams", "Slack", "Zoom", "Mattermost", "Rocket.Chat" }.Contains(a.Name);   // chat apps mostly used for work
        string[] like = workChat || a.Group == "Work & office" || a.Group == "Email" ? new[] { "work", "office", "job" }
                      : a.Group == "Notes & study" ? new[] { "school", "study" }
                      : a.Group == "Gaming" || a.Group == "Music & video" || a.Group == "Chat & social" ? new[] { "home", "personal" }
                      : new string[0];
        var c = Config.Categories.FirstOrDefault(x => !x.IsAsk && like.Any(w => x.Name.ToLowerInvariant().Contains(w)));
        // else the live one - unless that asks every time: a rule is for not asking
        if (c == null) c = Config.Current();
        if (c == null || c.IsAsk) c = Config.Categories.FirstOrDefault(x => !x.IsAsk && x.Works()) ?? c;
        return c != null ? c.Name : null;
    }

    // ---- 9: where things live -------------------------------------------------------------------

    void GuideTour(FlowLayoutPanel page)
    {
        page.Controls.Add(Title("Where things live", "That's it. Here is where everything is."));
        var table = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, BackColor = Ui.Soft, Padding = new Padding(12, 8, 12, 4),
                                           Margin = new Padding(0, 0, 0, 10), MinimumSize = new Size(SetupWidth, 0) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        TourLine(table, "Categories", "Your categories, and which browser and profile each opens.");
        TourLine(table, "Rules", "Links from an app, to a site, or with a word, sent to their own category.");
        TourLine(table, "Shortcuts", "The keys that switch categories from any program.");
        TourLine(table, "Link cleaning", "What is taken out of links - and a box to try one.");
        TourLine(table, "Link log", "Every link, where it came from and where it went.");
        TourLine(table, "About & updates", "Updates, and this guide again.");
        table.Controls.Add(new Label { Text = "Next to the clock", AutoSize = true, Font = new Font("Segoe UI", 9.75F, FontStyle.Italic),
                                       Margin = new Padding(0, 0, 8, 8) });
        table.Controls.Add(new Label { Text = "The LinkPilot icon: click to open, right-click for the menu.", AutoSize = true,
                                       Font = new Font("Segoe UI", 9.75F), Margin = new Padding(0, 0, 0, 8) });
        page.Controls.Add(table);
        var finish = Ui.Primary("Finish");
        finish.Click += delegate { LeaveSetup(); };
        page.Controls.Add(guideTrail.Count > 0 ? NavRow(finish, Link("←  Back", GuideBack)) : NavRow(finish));
    }

    // One tab: its name - a link that ends the guide and opens it - and what is on it.
    void TourLine(TableLayoutPanel table, string tab, string what)
    {
        var name = new LinkLabel { Text = tab, AutoSize = true, UseMnemonic = false, Font = new Font("Segoe UI", 9.75F), Margin = new Padding(0, 0, 8, 8) };
        name.LinkClicked += delegate { LeaveSetup(); ShowTabNamed(tab); };
        table.Controls.Add(name);
        table.Controls.Add(new Label { Text = what, AutoSize = true, Font = new Font("Segoe UI", 9.75F), Margin = new Padding(0, 0, 0, 8) });
    }
}
