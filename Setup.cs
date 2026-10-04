// The setup screen: what the window shows on the very first start, and again whenever LinkPilot
// is not the default browser.
//
// Three steps. The first says how LinkPilot works and why it can be trusted - what it needs,
// that it works offline, what the internet is used for. The second asks for the choices: link
// cleaning (of copied links too), the link log, and asking GitHub for updates. The third is the one thing Windows leaves to
// the person at the computer: making it the default browser - or, if it already is, says so. Then a
// big "You're all set", which also makes a first category from the browser used so far if there is
// none, and Finish - or, if you like, a short optional guide through the most useful settings (Guide.cs).
//
// Buttons as in any Windows setup: Back and Skip on the left, the blue button that goes on on the
// right.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

partial class SwitchForm
{
    Panel setup;
    FlowLayoutPanel setupHow, setupChoices, setupDefault, setupDone;
    readonly List<FlowLayoutPanel> setupPages = new List<FlowLayoutPanel>();   // step 1 is the first, the guide's from step 5
    Control setupTodo, setupAlready;      // step 3: the steps to take, or "already done"
    Button setupGo;                       // step 3's blue button: Open Windows Settings, or Next
    Label setupStatus, setupNext, exampleAfter;
    CheckBox chooseClean, chooseCopied, chooseLog, chooseUpdates;
    bool madeFirstCategory;
    readonly Timer setupWatch = new Timer { Interval = 1500 };
    readonly List<Control> mainScreen = new List<Control>();   // hidden while the setup screen shows

    const int SetupWidth = 640;
    static readonly Color Done = Ui.Ok;

    // the example under Clean links on step 2 - the same link the Link cleaning tab tries
    static readonly Font ExampleFont = new Font("Segoe UI", 8.25F), ExampleBold = new Font("Segoe UI", 9F, FontStyle.Bold);
    const string ExampleLink = "https://www.google.com/url?q=https://www.youtube.com/watch%3Fv%3DdQw4w9WgXcQ%26si%3DxYz123&sa=D&utm_source=chat";

    // Shows the setup screen over everything else, on its first step. Called when the window opens
    // on a first start or while LinkPilot is not the default browser; also from About & updates.
    public void ShowSetup()
    {
        if (setup == null) BuildSetup();
        foreach (var c in mainScreen) c.Visible = false;
        chooseClean.Checked = Config.CleanOn && Config.UnwrapOn;
        chooseCopied.Checked = Config.CopyCleanOn;
        chooseLog.Checked = Config.LogOn;
        chooseUpdates.Checked = Config.CheckUpdates;
        ShowExample();
        madeFirstCategory = false;
        SetupStep(1);
        setup.Visible = true;
    }

    // One of the three steps - the third starts watching for Windows to report the change - or, as
    // step 4, "You're all set"; 5 to 9 are the guide's screens.
    void SetupStep(int step)
    {
        PauseKeys(step == 6);    // the guide's keys show whether they are free - not held by the dock itself
        if (step <= 4) guideTrail.Clear();
        guideStep = step;
        if (step >= 5) BuildGuidePage(step);   // only now: it follows what the screen before it made
        for (int i = 0; i < setupPages.Count; i++) setupPages[i].Visible = i + 1 == step;
        setup.AutoScrollPosition = new Point(0, 0);
        setupStatus.ForeColor = SystemColors.GrayText;
        setupStatus.Text = "";
        if (step == 3)
        {
            bool already = IsDefaultBrowser;
            setupTodo.Visible = !already;
            setupAlready.Visible = already;
            setupGo.Text = already ? "Next  →" : "Open Windows Settings";
        }
        if (step == 4)
        {
            if (MakeFirstCategory()) madeFirstCategory = true;
            setupNext.Text = WhatNext();
        }
        if (step == 3 && !IsDefaultBrowser) setupWatch.Start(); else setupWatch.Stop();
    }

    // The choices of the second step, as they are ticked.
    void KeepChoices()
    {
        Config.CleanOn = Config.UnwrapOn = chooseClean.Checked;
        Config.CopyCleanOn = chooseCopied.Checked;
        Config.LogOn = chooseLog.Checked;
        Config.CheckUpdates = chooseUpdates.Checked;
        Save();
    }

    // Back to the main screen - on Finish, or Skip. The setup has been seen: it comes back by itself
    // only while LinkPilot is not the default browser.
    void LeaveSetup()
    {
        setupWatch.Stop();
        if (setup != null) setup.Visible = false;
        foreach (var c in mainScreen) c.Visible = true;
        if (!Config.SetupDone) { Config.SetupDone = true; Save(); }
        Reload();
        CheckDefault();
        PauseKeys(tabs.SelectedTab == shortcutsPage);
    }

    // With no category yet, the browser used until now becomes the first one - live - so links keep
    // going where they went, and the list shows where that is. True if one was made.
    bool MakeFirstCategory()
    {
        if (Config.Categories.Count > 0) return false;
        string exe = Config.FallbackExe;
        if (exe.Length == 0 || !File.Exists(exe))
        {
            var first = Machine.Browsers().FirstOrDefault();
            if (first == null) return false;
            exe = first.Exe;
        }
        var browser = Machine.Browsers().FirstOrDefault(b => string.Equals(b.Exe, exe, StringComparison.OrdinalIgnoreCase));
        string full = browser != null ? browser.Name : Path.GetFileNameWithoutExtension(exe);
        string name = full;
        foreach (string maker in new[] { "Mozilla ", "Google ", "Microsoft " })   // "Firefox", not "Mozilla Firefox"
            if (name.StartsWith(maker, StringComparison.OrdinalIgnoreCase) && name.Length > maker.Length) name = name.Substring(maker.Length);
        string key = Config.SuggestKey(name, 1);
        Config.Categories.Add(new Category { Name = name, Exe = exe, Args = "", Shows = full, HotKey = key, DefaultKey = key });
        Config.Active = name;
        Save();
        return true;
    }

    void BuildSetup()
    {
        setup = new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.Window, AutoScroll = true, Visible = false };
        setupHow = Column();
        setupChoices = Column();
        setupDefault = Column();
        setupDone = Column();
        setupPages.AddRange(new[] { setupHow, setupChoices, setupDefault, setupDone });
        setupPages.AddRange(GuidePages());
        foreach (var page in setupPages) setup.Controls.Add(page);
        setup.Resize += delegate
        {
            int left = Math.Max(16, (setup.ClientSize.Width - SetupWidth) / 2);
            foreach (var page in setupPages) page.Left = left;
        };

        // ---- step 1: how it works ----
        setupHow.Controls.Add(Title("Welcome to LinkPilot", "Step 1 of 3 - how it works"));
        setupHow.Controls.Add(Heading("One step is needed - here is why"));
        setupHow.Controls.Add(Text_(
            "LinkPilot decides which browser - and which profile of it - each link opens in. For that, every link " +
            "has to pass through it first, and Windows sends links only to your default browser. So LinkPilot has " +
            "to be your default browser; it then hands each link straight on to the browser you chose. This is the " +
            "only way it can work.\n" +
            "Windows lets only you make this choice - no program and no command may make it for you. That protects " +
            "you from programs that would take over your browser. Step 3 shows you where."));

        setupHow.Controls.Add(Heading("Why you can trust it"));
        var trust = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, BackColor = Ui.Soft,
                                           Padding = new Padding(12, 8, 12, 2), Margin = new Padding(0, 0, 0, 10),
                                           Width = SetupWidth, MaximumSize = new Size(SetupWidth, 0) };
        trust.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        trust.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Trust(trust, "Fully offline", "Everything works without the internet. The only time LinkPilot goes online is " +
              "to ask GitHub for a newer version, and to download it when you press Update - you can switch the check " +
              "off in the next step.");
        Trust(trust, "Sends nothing", "No account, no ads, no tracking, nothing collected. Your links, settings and the " +
              "link log stay in its own folder on this PC.");
        Trust(trust, "Made for personal use", "A small project made for private use, and shared freely - nothing is sold, " +
              "nothing is in it for anyone else.");
        Trust(trust, "Open", "Every file is public on GitHub, to read or to build yourself - what runs is what you can see.");
        Trust(trust, "Easy to undo", "Your other browsers stay as they are. Make one of them the default again at any " +
              "time, or uninstall LinkPilot.");
        setupHow.Controls.Add(trust);
        var next = Ui.Primary("Next  →");
        next.Click += delegate { SetupStep(2); };
        setupHow.Controls.Add(NavRow(next, Link("Skip setup", LeaveSetup)));

        // ---- step 2: three choices ----
        setupChoices.Controls.Add(Title("Your choices", "Step 2 of 3 - all of these can be changed later, on their tabs"));
        chooseClean = Choice(setupChoices, "Clean links", "Takes tracking out of links - utm_source, fbclid, YouTube's si... - and " +
                             "skips redirects such as google.com/url?q=..., so the page opens directly. Only parts known to be " +
                             "tracking are removed, so links keep working.");
        setupChoices.Controls.Add(CleanExample());
        chooseClean.CheckedChanged += delegate { ShowExample(); };
        chooseCopied = Choice(setupChoices, "Clean copied links too", "When you copy a link on its own - YouTube's Copy link, " +
                              "a link from a chat - it is cleaned the same way right away, so you paste the clean link. Text " +
                              "with a link inside, and anything a password manager copies, is left alone. Nothing is kept.");
        chooseLog = Choice(setupChoices, "Keep a log of links", "Which app each link came from, where it opened, and what was " +
                           "changed - kept on this PC only, the newest " + LinkLog.Keep + " links.");
        chooseUpdates = Choice(setupChoices, "Check for updates automatically", "When LinkPilot starts and when you open its " +
                               "window, ask GitHub whether a newer version exists - the only time it goes online. Nothing is " +
                               "downloaded until you press Update.");
        var next2 = Ui.Primary("Next  →");
        next2.Click += delegate { KeepChoices(); SetupStep(3); };
        setupChoices.Controls.Add(NavRow(next2, Link("←  Back", () => SetupStep(1))));

        // ---- step 3: making it the default browser - or saying it already is ----
        setupDefault.Controls.Add(Title("Make LinkPilot your default browser", "Step 3 of 3 - the one step Windows leaves to you"));
        var todo = Column();
        todo.Top = 0; todo.Margin = new Padding(0);
        todo.Controls.Add(Text_(
            "1.  Press Open Windows Settings below - it opens on LinkPilot's own page.\n" +
            "     Give it a few seconds: Settings first looks LinkPilot up, and only then shows the Set default\n" +
            "     button at the top. Wait for it - nothing is wrong.\n" +
            "2.  Windows 11: press Set default at the top of that page.\n" +
            "     Windows 10: under Web browser, click the browser shown and choose LinkPilot.\n" +
            "3.  Come back here - this screen notices by itself and takes you on."));
        todo.Controls.Add(Text_(
            "Why not automatically? Windows keeps the default browser as a choice only you can make: a program - or a " +
            "command typed in PowerShell - that tries to set it is simply ignored. That is what stops other programs " +
            "from taking over your browser, and why this one click is yours."));
        setupTodo = todo;
        setupDefault.Controls.Add(todo);
        setupAlready = Banner("✓", "Already done", "LinkPilot is already your default browser - there is nothing to do here.");
        setupDefault.Controls.Add(setupAlready);
        setupGo = Ui.Primary("Open Windows Settings");
        setupGo.Click += delegate
        {
            if (IsDefaultBrowser) { SetupStep(4); return; }
            OpenDefaultAppsSettings();
            setupStatus.ForeColor = SystemColors.GrayText;
            setupStatus.Text = "Settings is opening - it can take a few seconds until Set default appears at the top. " +
                               "Waiting for you to press it…";
        };
        setupDefault.Controls.Add(NavRow(setupGo, Link("←  Back", () => SetupStep(2)), Link("Skip for now", LeaveSetup)));
        setupStatus = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(2, 10, 0, 12) };
        setupDefault.Controls.Add(setupStatus);

        // ---- you're all set ----
        setupDone.Controls.Add(Banner("✓", "You're all set!", "LinkPilot is your default browser."));
        setupDone.Controls.Add(Text_("From now on every link passes through LinkPilot, and goes on to the browser you choose."));
        setupNext = Text_("");
        setupDone.Controls.Add(setupNext);
        // the way into the guide - plainly optional
        var more = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Ui.Soft,
                                         Padding = new Padding(12, 10, 12, 8), Margin = new Padding(0, 0, 0, 6), MinimumSize = new Size(SetupWidth, 0) };
        more.Controls.Add(new Label { Text = "A few quick things - about a minute, all optional", AutoSize = true, ForeColor = Ui.AccentDark,
                                      Font = new Font("Segoe UI", 9.75F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 2) });
        more.Controls.Add(new Label { Text = "Your categories  ·  How you switch  ·  Rules for your apps", AutoSize = true,
                                      Font = new Font("Segoe UI", 9.75F), Margin = new Padding(0) });
        setupDone.Controls.Add(more);
        var guide = Ui.Primary("Set them up  →");
        guide.Click += delegate { StartGuide(); };
        setupDone.Controls.Add(NavRow(guide, Link("Finish - I'll look around myself", LeaveSetup)));

        setupWatch.Tick += delegate
        {
            if (!IsDefaultBrowser) return;
            setupWatch.Stop();
            SetupStep(4);
        };
        FormClosed += delegate { setupWatch.Stop(); };

        Controls.Add(setup);
        setup.BringToFront();
        setup.VisibleChanged += delegate { ShowBanner(); };   // the update banner is not shown over the setup
    }

    // The "all set" page's line: where links open now - or, with no category, how to make one.
    string WhatNext()
    {
        var live = Config.Current();
        if (madeFirstCategory && live != null)
            return "Your first category, " + live.Name + ", is live: links open in " + live.Shows + ", just as before.";
        if (live != null)
            return "Links open in " + live.Name + " (" + live.Shows + ").";
        return "Until a category is live, links go to " + Program.OriginalBrowserName() + ", as before. Make one on the " +
               "Categories tab: press New category, pick a browser profile on the right and press Use this.";
    }

    static FlowLayoutPanel Column()
    {
        return new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
                                     AutoSizeMode = AutoSizeMode.GrowAndShrink, Top = 14, Left = 16 };
    }

    Control Title(string title, string under)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 10) };
        row.Controls.Add(new PictureBox { Image = Icon.ToBitmap(), Size = new Size(40, 40), SizeMode = PictureBoxSizeMode.Zoom,
                                          Margin = new Padding(0, 2, 12, 0) });
        var words = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        words.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 16F, FontStyle.Bold), Margin = new Padding(0) });
        words.Controls.Add(new Label { Text = under, AutoSize = true, ForeColor = SystemColors.GrayText, Font = new Font("Segoe UI", 10F),
                                       Margin = new Padding(2, 0, 0, 0) });
        row.Controls.Add(words);
        return row;
    }

    // A big green tick with a headline and a line under it - for "done".
    static Control Banner(string mark, string headline, string under)
    {
        var box = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Ui.OkBack,
                                        Padding = new Padding(14, 10, 20, 10), Margin = new Padding(0, 4, 0, 14),
                                        MinimumSize = new Size(SetupWidth, 0) };
        box.Controls.Add(new Label { Text = mark, AutoSize = true, Font = new Font("Segoe UI", 30F, FontStyle.Bold), ForeColor = Done,
                                     Margin = new Padding(0, 0, 14, 0) });
        var words = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        words.Controls.Add(new Label { Text = headline, AutoSize = true, Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = Done,
                                       Margin = new Padding(0) });
        words.Controls.Add(new Label { Text = under, AutoSize = true, MaximumSize = new Size(SetupWidth - 110, 0), Font = new Font("Segoe UI", 10.5F),
                                       Margin = new Padding(2, 0, 0, 0) });
        box.Controls.Add(words);
        return box;
    }

    static Label Heading(string text)
    {
        return new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), Margin = new Padding(0, 2, 0, 4) };
    }

    static Label Text_(string text)
    {
        return new Label { Text = text, AutoSize = true, MaximumSize = new Size(SetupWidth, 0), Font = new Font("Segoe UI", 9.75F), UseMnemonic = false,
                           Margin = new Padding(0, 0, 0, 10) };
    }

    static void Trust(TableLayoutPanel table, string what, string why)
    {
        table.Controls.Add(new Label { Text = "✓  " + what, AutoSize = true, Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
                                       ForeColor = Ui.AccentDark, Margin = new Padding(0, 0, 8, 6) });
        table.Controls.Add(new Label { Text = why, AutoSize = true, MaximumSize = new Size(SetupWidth - 200, 0),
                                       Font = new Font("Segoe UI", 9.75F), Margin = new Padding(0, 0, 0, 6) });
    }

    // A tick with its name in bold, and what it means underneath.
    static CheckBox Choice(Control column, string what, string means)
    {
        var tick = new CheckBox { Text = what, AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Margin = new Padding(0, 6, 0, 0) };
        column.Controls.Add(tick);
        column.Controls.Add(new Label { Text = means, AutoSize = true, MaximumSize = new Size(SetupWidth - 20, 0), UseMnemonic = false,
                                        Font = new Font("Segoe UI", 9.75F), ForeColor = Ui.Muted,
                                        Margin = new Padding(20, 0, 0, 12) });
        return tick;
    }

    // Under Clean links: a real link before and after - worked out by the cleaner itself, so it
    // always shows what it really does.
    Control CleanExample()
    {
        var box = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, BackColor = Ui.Soft, Padding = new Padding(10, 6, 10, 4),
                                         Margin = new Padding(20, 0, 0, 12), Width = SetupWidth - 20 };
        box.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        box.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SetupWidth - 20 - 52 - 20));
        box.Controls.Add(new Label { Text = "Before", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 0, 0, 4) });
        box.Controls.Add(new Label { Text = ExampleLink, AutoSize = false, AutoEllipsis = true, UseMnemonic = false, Dock = DockStyle.Fill,
                                     Height = 16, Font = ExampleFont, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 1, 0, 4) });
        box.Controls.Add(new Label { Text = "After", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 0, 0, 4) });
        exampleAfter = new Label { AutoSize = false, AutoEllipsis = true, UseMnemonic = false, Dock = DockStyle.Fill, Height = 18, Margin = new Padding(0, 0, 0, 4) };
        box.Controls.Add(exampleAfter);
        return box;
    }

    void ShowExample()
    {
        if (!chooseClean.Checked)
        {
            // the link itself would not fit beside it - it is the one above
            exampleAfter.Text = "The same link, left as it is";
            exampleAfter.ResetFont();
            exampleAfter.ForeColor = SystemColors.WindowText;
            return;
        }
        // as it will be with cleaning on - whatever the settings are until Next
        bool clean = Config.CleanOn, unwrap = Config.UnwrapOn;
        Config.CleanOn = Config.UnwrapOn = true;
        string changes, after = Cleaner.Apply(ExampleLink, out changes);
        Config.CleanOn = clean; Config.UnwrapOn = unwrap;
        exampleAfter.Text = after;
        exampleAfter.Font = ExampleBold;
        exampleAfter.ForeColor = Ui.Ok;
    }

    static LinkLabel Link(string text, Action click)
    {
        var l = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, 10, 22, 0) };
        l.LinkClicked += delegate { click(); };
        return l;
    }

    // The bottom row of a step: Back / Skip on the left, the blue button on the right.
    static Control NavRow(Button primary, params Control[] left)
    {
        var row = new Panel { Width = SetupWidth, Height = 40, Margin = new Padding(0, 8, 0, 0) };
        var links = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        links.Controls.AddRange(left);
        primary.Dock = DockStyle.Right;
        row.Controls.Add(links);
        row.Controls.Add(primary);
        return row;
    }

    // Windows 11 can open straight at one app's page, which saves hunting through the list. The name
    // has to be the value name under RegisteredApplications - "BrowserSwitch", no space. If that form
    // of the link is not understood, the plain Default apps page still opens.
    static void OpenDefaultAppsSettings()
    {
        try { System.Diagnostics.Process.Start("ms-settings:defaultapps?registeredAppUser=BrowserSwitch"); }
        catch { try { System.Diagnostics.Process.Start("ms-settings:defaultapps"); } catch { } }
    }
}
