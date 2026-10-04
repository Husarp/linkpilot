// LinkPilotSetup-X.Y.Z.exe - installs, updates and uninstalls LinkPilot, in one window.
//
//   LinkPilotSetup.exe                      install, or update what is installed (it says which)
//   LinkPilotSetup.exe --update --dir "<folder>"
//                                           started by the app's own updater: only the progress, then
//                                           LinkPilot starts again and this window closes by itself
//   Uninstall LinkPilot.exe --uninstall     its copy in the program folder - Apps & features runs it
//   LinkPilotSetup.exe --selftest           no window: checks what is inside, writes setup-selftest.txt
//
// For your account only, no administrator rights (setup.manifest says so - without it Windows asks
// for them for anything called "Setup"). The program files are inside this exe; the work itself is
// in SetupSteps.cs, on its own thread - only this window's thread touches the window.
//
// Built from installer\*.cs and the app's Ui.cs (its colours and buttons) by build.cmd and on GitHub.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("LinkPilot Setup")]
[assembly: System.Reflection.AssemblyProduct("LinkPilot")]
[assembly: System.Reflection.AssemblyCompany("LinkPilot")]

static class Setup
{
    const string OneCopy = @"Local\LinkPilot.Setup", ShowSignal = @"Local\LinkPilot.Setup.Show";

    public static string Self { get { return Application.ExecutablePath; } }

    [STAThread]
    static int Main(string[] args)
    {
        bool update = false, uninstall = false, selftest = false;
        string dir = null;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();
            if (a == "--update") update = true;
            else if (a == "--uninstall") uninstall = true;
            else if (a == "--selftest") selftest = true;
            else if (a == "--dir" && i + 1 < args.Length) dir = args[++i];
        }
        Mode mode = uninstall ? Mode.Uninstall : update ? Mode.Update : Mode.Install;
        if (selftest) return SelfTest();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        bool first;
        using (var one = new Mutex(true, OneCopy, out first))
        {
            if (!first)
            {
                try { using (var show = EventWaitHandle.OpenExisting(ShowSignal)) show.Set(); } catch { }
                return 0;
            }
            var place = Place.Find(dir, mode);
            // nothing to update (and no folder given): an install like any other
            if (mode == Mode.Update && place.Installed == null && dir == null) mode = Mode.Install;
            var form = new SetupForm(mode, place, Payload.Version);
            var signal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignal);
            new Thread(() => { while (signal.WaitOne()) try { form.BeginInvoke((MethodInvoker)form.Bring); } catch { } }) { IsBackground = true }.Start();
            Application.Run(form);
            GC.KeepAlive(one);
        }
        return 0;
    }

    // For the build: every program file inside and not empty, the version they are, and every page
    // of the window built (not shown). Exit code 1 if anything is wrong.
    static int SelfTest()
    {
        var sb = new StringBuilder();
        bool ok = true;
        foreach (string name in Payload.Files)
        {
            var data = Payload.Read(name);
            bool good = data != null && data.Length > 0;
            ok &= good;
            sb.AppendLine("payload: " + name + " " + (good ? data.Length + " bytes" : "MISSING"));
        }
        string version = null;
        try { version = Payload.Version; }
        catch (Exception e) { sb.AppendLine("version unreadable: " + e.Message); }
        ok &= version != null;
        sb.AppendLine("version: " + version);
        try
        {
            using (var f = new SetupForm(Mode.Install, Place.Find(null, Mode.Install), version ?? "0.0.0"))
                sb.AppendLine("pages built: " + f.BuildEveryPage());
        }
        catch (Exception e) { ok = false; sb.AppendLine("window FAILED: " + e); }
        try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(Self), "setup-selftest.txt"), sb.ToString()); }
        catch { ok = false; }
        return ok ? 0 : 1;
    }
}

class SetupForm : Form
{
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l);

    readonly Mode mode;
    readonly Place place;
    readonly string version;
    readonly float scale;          // 1 at 100 % display scaling, 1.5 at 150 %
    readonly Label title, subtitle;
    readonly Panel body;
    readonly FlowLayoutPanel buttons;
    FlowLayoutPanel column;

    // the progress page, which also shows a failure
    Label head, stepText, fileText, percentText, problem, note, foot;
    ProgressBar bar;
    Control fileRow;
    LinkLabel details;
    TextBox log;
    readonly StringBuilder logText = new StringBuilder();
    CheckBox runNow, deleteData;

    readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer { Interval = 30 };
    readonly System.Windows.Forms.Timer waitDefault = new System.Windows.Forms.Timer { Interval = 1000 };
    Work work;
    bool busy, finished, restarted;   // restarted: the old version runs again after a failure
    double shown, target, stepEnd;   // the bar: shown, where the work is, where this step ends (0..1)

    public SetupForm(Mode mode, Place place, string version)
    {
        this.mode = mode;
        this.place = place;
        this.version = version;
        using (var g = CreateGraphics()) scale = g.DpiX / 96F;
        AutoScaleMode = AutoScaleMode.None;   // sized by hand, below, for the display's scaling
        Font = new Font("Segoe UI", 9F);
        Text = mode == Mode.Uninstall ? "Uninstall LinkPilot" : "LinkPilot " + version + " Setup";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = SystemColors.Window;
        ClientSize = new Size(S(560), S(450));
        try { Icon = Icon.ExtractAssociatedIcon(Setup.Self); } catch { }

        // the top: the app's icon, the name, a grey line - like the app's own window
        var header = new Panel { Dock = DockStyle.Top, Height = S(66), BackColor = SystemColors.Window };
        var logo = new PictureBox { Location = new Point(S(22), S(17)), Size = new Size(S(32), S(32)), SizeMode = PictureBoxSizeMode.Zoom };
        try { using (var big = new Icon(Icon, S(32), S(32))) logo.Image = big.ToBitmap(); } catch { }
        title = new Label { AutoSize = true, Location = new Point(S(64), S(12)), Font = new Font("Segoe UI", 12F, FontStyle.Bold), UseMnemonic = false };
        subtitle = new Label { AutoSize = true, Location = new Point(S(66), S(38)), ForeColor = SystemColors.GrayText, UseMnemonic = false,
                               Text = "Sends each link to the browser and profile you chose" };
        header.Controls.Add(logo);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Ui.Line });

        // the bottom: the buttons on the right, the blue one that goes on next to Cancel
        var footer = new Panel { Dock = DockStyle.Bottom, Height = S(54), BackColor = Ui.Bar };
        buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
                                        Padding = new Padding(S(12), S(11), S(12), 0), BackColor = Ui.Bar };
        footer.Controls.Add(buttons);
        footer.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Ui.Line });

        body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = SystemColors.Window };
        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);

        clock.Tick += delegate { Drain(); };
        waitDefault.Tick += delegate
        {
            if (Defaults.LinkPilotIsDefault) return;
            waitDefault.Stop();
            Go();
        };
        // it must not die with its window half-way: closing is refused while it works
        FormClosing += (s, e) => { if (busy && e.CloseReason == CloseReason.UserClosing) e.Cancel = true; };
        FormClosed += delegate { clock.Stop(); waitDefault.Stop(); AfterClose(); };
        Title();
        if (mode == Mode.Uninstall) UninstallWelcome();
        else if (mode == Mode.Update) Go();
        else Welcome();
    }

    int S(int px) { return (int)Math.Round(px * scale); }

    // the width text wraps at
    int TextWidth { get { return ClientSize.Width - S(56) - SystemInformation.VerticalScrollBarWidth; } }

    public void Bring()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    void Title()
    {
        title.Text = mode == Mode.Uninstall ? "Uninstall LinkPilot" : "LinkPilot " + version + " Setup";
    }

    // ---- building blocks ----------------------------------------------------------------------------

    // A new page: the body emptied, a heading at its top, no buttons yet.
    void Page(string heading)
    {
        foreach (var c in body.Controls.Cast<Control>().ToList()) c.Dispose();
        foreach (var c in buttons.Controls.Cast<Control>().ToList()) c.Dispose();
        body.Controls.Clear();
        buttons.Controls.Clear();
        log = null;
        column = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
                                       Location = new Point(S(28), S(20)), Padding = new Padding(0, 0, 0, S(12)) };
        body.Controls.Add(column);
        head = new Label { Text = heading, AutoSize = true, MaximumSize = new Size(TextWidth, 0), UseMnemonic = false,
                           Font = new Font("Segoe UI", 11F, FontStyle.Bold), Margin = new Padding(0, 0, 0, S(12)) };
        column.Controls.Add(head);
    }

    Label Line(string text, Color colour)
    {
        var l = new Label { Text = text, AutoSize = true, MaximumSize = new Size(TextWidth, 0), ForeColor = colour, UseMnemonic = false,
                            Margin = new Padding(0, 0, 0, S(8)) };
        column.Controls.Add(l);
        return l;
    }

    // what will happen, one point each
    void Points(params string[] points)
    {
        var table = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 0, 0, S(8)) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(18)));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach (string p in points)
        {
            table.Controls.Add(new Label { Text = "•", AutoSize = true, ForeColor = Ui.Accent, Margin = new Padding(0, 0, 0, S(7)) });
            table.Controls.Add(new Label { Text = p, AutoSize = true, MaximumSize = new Size(TextWidth - S(24), 0), ForeColor = Ui.Muted,
                                           UseMnemonic = false, Margin = new Padding(0, 0, 0, S(7)) });
        }
        column.Controls.Add(table);
    }

    // Buttons go in from the right: Cancel / Close first, then the blue one.
    Button Main(string text, Action click)
    {
        var b = Ui.Primary(text, true);
        b.MinimumSize = new Size(S(92), S(28));
        b.Margin = new Padding(S(8), 0, 0, 0);
        b.Click += delegate { click(); };
        buttons.Controls.Add(b);
        AcceptButton = b;
        b.Cursor = Cursors.Hand;
        return b;
    }

    Button Other(string text, Action click)
    {
        var b = Ui.Button(text, delegate { click(); });
        b.MinimumSize = new Size(S(84), S(28));
        b.Margin = new Padding(S(8), 0, 0, 0);
        b.Cursor = Cursors.Hand;
        buttons.Controls.Add(b);
        return b;
    }

    // ---- install and update -------------------------------------------------------------------------

    void Welcome()
    {
        string installed = place.Installed;
        if (place.Dev)
        {
            Page("LinkPilot is registered from a development folder");
            Line("It is a git clone: " + place.Dir + ". Update it with git pull and build.cmd.", Ui.Muted);
            Main("Close", Close);
            return;
        }
        Button go;
        if (installed == null)
        {
            Page("Install LinkPilot " + version);
            Other("Cancel", Close);
            Points("Installs LinkPilot in " + place.Dir + " - for your account only, no administrator rights.",
                   "Adds it to Windows' list of browsers. Your default browser does not change - LinkPilot then shows you the one step Windows leaves to you.",
                   "Adds LinkPilot to the Start menu and the Desktop, and starts it with Windows next to the clock.");
            go = Main("Install", AskIfRunning);
        }
        else if (Place.IsNewer(version, installed))
        {
            Page("Update LinkPilot " + installed + " → " + version);
            Other("Cancel", Close);
            Points("LinkPilot is closed for a moment and started again.",
                   "The new version replaces the program in " + place.Dir + ".",
                   "Your categories, rules, settings and link log are kept.");
            go = Main("Update", AskIfRunning);
        }
        else if (installed == version)
        {
            Page("LinkPilot " + version + " is already installed");
            Other("Cancel", Close);
            Line("Installing it again is safe, and repairs a broken install. Your settings are kept.", Ui.Muted);
            go = Main("Reinstall", AskIfRunning);
        }
        else
        {
            Page("You have LinkPilot " + installed + ", which is newer");
            Other("Cancel", Close);
            Line("Install " + version + " over it? Your settings are kept.", Ui.Muted);
            go = Main("Install", AskIfRunning);
        }
        go.Focus();
    }

    // "LinkPilot is running" - asked, never closed without asking in an install you started yourself.
    // Looked for off the window's thread.
    void AskIfRunning()
    {
        foreach (Control b in buttons.Controls) b.Enabled = false;
        new Thread(() =>
        {
            bool running = App.Running(place);
            try { BeginInvoke((MethodInvoker)(() => { if (running) RunningPage(); else Go(); })); } catch { }
        }) { IsBackground = true }.Start();
    }

    void RunningPage()
    {
        bool updating = place.Installed != null && Place.IsNewer(version, place.Installed);
        Page("LinkPilot is running");
        var box = new Label { AutoSize = true, MaximumSize = new Size(TextWidth, 0), BackColor = Ui.WarnBack, ForeColor = Ui.Warn,
                              Padding = new Padding(S(12), S(10), S(12), S(10)), Margin = new Padding(0, 0, 0, S(8)), UseMnemonic = false,
                              Text = "It will be closed to " + (updating ? "update" : "install") + " it, then started again." };
        column.Controls.Add(box);
        Other("Cancel", Close);
        Main("OK", Go).Focus();
    }

    // The progress page, and the work started.
    void Go()
    {
        string doing = mode == Mode.Uninstall ? "Uninstalling" : mode == Mode.Update || (place.Installed != null && Place.IsNewer(version, place.Installed)) ? "Updating" : "Installing";
        Page(doing + " LinkPilot");
        stepText = Line("Starting…", SystemColors.ControlText);
        stepText.Margin = new Padding(0, 0, 0, S(6));
        problem = Line("", Color.FromArgb(164, 38, 44));
        problem.Visible = false;
        bar = new ProgressBar { Width = TextWidth, Height = S(18), Maximum = 1000, Style = ProgressBarStyle.Continuous, Margin = new Padding(0, 0, 0, S(4)) };
        column.Controls.Add(bar);
        var row = new TableLayoutPanel { ColumnCount = 2, Width = TextWidth, Height = S(20), Margin = new Padding(0, 0, 0, S(6)) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fileText = new Label { AutoSize = false, AutoEllipsis = true, Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText, UseMnemonic = false, Margin = new Padding(0) };
        percentText = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0), Text = "0%" };
        row.Controls.Add(fileText, 0, 0);
        row.Controls.Add(percentText, 1, 0);
        column.Controls.Add(row);
        fileRow = row;
        note = Line("", Ui.Muted);
        note.Visible = false;
        details = new LinkLabel { Text = "Show details ▸", AutoSize = true, Margin = new Padding(0, 0, 0, S(4)) };
        details.LinkClicked += delegate { ShowDetails(!log.Visible); };
        column.Controls.Add(details);
        log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = TextWidth, Height = S(92),
                            Font = new Font("Consolas", 9F), BackColor = SystemColors.Window, Visible = false, WordWrap = true,
                            Margin = new Padding(0, 0, 0, S(6)) };
        log.Text = logText.ToString();
        column.Controls.Add(log);
        foot = Line(mode == Mode.Update ? "This takes a few seconds. LinkPilot starts again by itself when it's done." : "This takes a few seconds.",
                    SystemColors.GrayText);
        Ui.HandCursors(column);

        work = new Work(mode, place, version) { DeleteData = deleteData != null && deleteData.Checked, Again = restarted };
        busy = true;
        shown = target = stepEnd = 0;
        var thread = new Thread(work.Run) { IsBackground = false };   // the install finishes even if the window goes
        thread.SetApartmentState(ApartmentState.STA);                  // the Recycle Bin is a shell call
        thread.Start();
        clock.Start();
    }

    void ShowDetails(bool open)
    {
        log.Visible = open;
        details.Text = open ? "Hide details ▾" : "Show details ▸";
        if (open) { log.SelectionStart = log.TextLength; log.ScrollToCaret(); }
    }

    void AddLog(string line)
    {
        logText.AppendLine(line);
        if (log != null && !log.IsDisposed) log.AppendText(line + Environment.NewLine);
    }

    // Every 30 ms: what the work posted, and the bar eased towards it - creeping on towards the end
    // of the current step while a step says nothing (registering takes a few seconds), never frozen.
    void Drain()
    {
        object[] m;
        while (work != null && work.Posted.TryDequeue(out m))
        {
            string kind = (string)m[0];
            if (kind == "step") stepText.Text = (string)m[1];
            else if (kind == "file") fileText.Text = (string)m[1];
            else if (kind == "log") AddLog((string)m[1]);
            else if (kind == "progress") { var at = (double[])m[1]; target = Math.Max(target, at[0]); stepEnd = at[1]; }
            else if (kind == "done") { Done(); return; }
            else if (kind == "failed") { Failed((string)m[1]); return; }
        }
        if (shown < target) shown = Math.Min(target, shown + Math.Max(0.002, (target - shown) * 0.2));
        else
        {
            double creepTo = target + (stepEnd - target) * 0.9;
            if (shown < creepTo) shown += (creepTo - shown) * 0.01;
        }
        ShowBar();
    }

    void ShowBar()
    {
        bar.Value = Math.Max(0, Math.Min(1000, (int)(shown * 1000)));
        percentText.Text = (int)(shown * 100) + "%";
    }

    void Done()
    {
        clock.Stop();
        busy = false;
        shown = target = 1;
        ShowBar();
        if (mode == Mode.Uninstall) { UninstallDone(); return; }
        if (mode == Mode.Update)
        {
            finished = true;
            head.Text = "LinkPilot " + version + " is installed";
            stepText.Text = "LinkPilot is starting again. This window closes by itself.";
            fileText.Text = "";
            foot.Visible = false;
            var later = new System.Windows.Forms.Timer { Interval = 1500 };
            later.Tick += delegate { later.Stop(); Close(); };
            later.Start();
            return;
        }
        Page("LinkPilot " + version + " is installed");
        Line(place.Installed == null ? "LinkPilot opens and walks you through the last step: choosing it as your default browser."
                                     : "Your settings are kept.", Ui.Muted);
        runNow = new CheckBox { Text = "Run LinkPilot now", Checked = true, AutoSize = true, Margin = new Padding(0, S(6), 0, 0), Cursor = Cursors.Hand };
        column.Controls.Add(runNow);
        Main("Finish", () =>
        {
            finished = true;
            try
            {
                if (runNow.Checked) App.Start(place.Exe, "");
                else if (work.ClosedApp) App.Start(place.Exe, "--tray");   // it was running: it runs again, next to the clock
            }
            catch { }
            Close();
        }).Focus();
    }

    // The error stays on screen, with the details open, Try again and Close. The old version is put
    // back by the work itself and started again right away, in the dock - LinkPilot is never left off.
    void Failed(string why)
    {
        clock.Stop();
        busy = false;
        try { SendMessage(bar.Handle, 0x410, (IntPtr)2, IntPtr.Zero); } catch { }   // PBM_SETSTATE, PBST_ERROR: the bar turns red
        head.Text = mode == Mode.Uninstall ? "The uninstall didn't finish" : mode == Mode.Update || head.Text.StartsWith("Updating") ? "The update didn't finish" : "The install didn't finish";
        stepText.Text = "Something went wrong:";
        stepText.Font = new Font(Font, FontStyle.Bold);
        problem.Text = why;
        problem.Visible = true;
        fileRow.Visible = false;
        foot.Visible = false;
        string before = place.Installed;
        string result = work.PutBackResult;
        restarted = false;
        if (mode != Mode.Uninstall && (mode == Mode.Update || work.ClosedApp) && File.Exists(place.Exe) && (result == "restored" || result == "nothing"))
            try { App.Start(place.Exe, "--tray"); restarted = true; } catch { }
        note.Text = mode == Mode.Uninstall ? "LinkPilot may be partly removed - the details say how far it got. Try again to finish it."
                  : result == "restored" ? "The previous version (" + before + ") is back in place" + (restarted ? " and running again." : ".")
                  : result == "partial" ? "Not everything could be put back - the previous files are in " + Path.Combine(place.Dir, ".update-backup") + "."
                  : before != null && result == "nothing" ? "Nothing was changed."
                  : "Nothing was installed.";
        note.Visible = true;
        ShowDetails(true);
        Other("Close", Close);
        Main("Try again", Again).Focus();
    }

    void Again()
    {
        try { SendMessage(bar.Handle, 0x410, (IntPtr)1, IntPtr.Zero); } catch { }   // PBST_NORMAL
        Go();
    }

    // ---- uninstall ----------------------------------------------------------------------------------

    void UninstallWelcome()
    {
        bool registered = false;
        try { using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Place.Key)) registered = k != null; } catch { }
        if (!registered && !File.Exists(place.Exe))
        {
            Page("LinkPilot isn't installed");
            Line("There is nothing to remove from this PC.", Ui.Muted);
            Main("Close", Close);
            return;
        }
        Page("Remove LinkPilot from this PC");
        Other("Cancel", Close);
        Points("Links go back to the browser you choose as your default.",
               place.OwnFolder ? "Its Windows registration, shortcuts and program files are removed."
                               : "Its Windows registration and shortcuts are removed. The folder " + place.Dir + " is left as it is.");
        if (place.OwnFolder)
        {
            deleteData = new CheckBox { Text = "Also delete my settings and link log", Checked = false, AutoSize = true,
                                        Margin = new Padding(0, S(6), 0, 0), Cursor = Cursors.Hand };
            column.Controls.Add(deleteData);
            Line("Kept, they are there again if you install LinkPilot later. Deleted, they go to the Recycle Bin.", SystemColors.GrayText)
                .Margin = new Padding(S(18), S(2), 0, 0);
        }
        Main("Uninstall", () => { if (Defaults.LinkPilotIsDefault) DefaultPage(); else Go(); }).Focus();
    }

    // LinkPilot is the default browser: another one has to be first, or Windows is left pointing at
    // nothing. Only the person at the PC may choose it - this waits, then carries on by itself.
    void DefaultPage()
    {
        string[] previous = Defaults.Previous(place.Dir);
        Page(previous != null ? "First, " + previous[1] + " becomes your default browser again" : "First, choose your default browser");
        Line(previous != null ? "Windows lets only you make this choice. Press Open Settings, then Set default at the top. This window carries on by itself."
                              : "Windows lets only you make this choice. Press Open Settings, choose a browser and press Set default. This window carries on by itself.",
             Ui.Muted);
        string settings = previous != null && previous[0] != null ? "ms-settings:defaultapps?registeredAppUser=" + previous[0] : "ms-settings:defaultapps";
        Other("Cancel", Close);
        Main("Open Settings", () => { try { Process.Start(settings); } catch { } }).Focus();
        waitDefault.Start();
    }

    void UninstallDone()
    {
        finished = true;
        Page("LinkPilot is uninstalled");
        bool dataLeft = File.Exists(Path.Combine(place.Dir, "config.txt")) || File.Exists(Path.Combine(place.Dir, "link-log.txt"));
        if (!place.OwnFolder) Line("The folder " + place.Dir + " is left as it is - delete it whenever you like.", Ui.Muted);
        else if (dataLeft) Line("Your settings and link log are still in " + place.Dir + " - installing LinkPilot again picks them up.", Ui.Muted);
        else if (work.DeleteData) Line("Your settings and link log are in the Recycle Bin.", Ui.Muted);
        Main("Close", Close).Focus();
    }

    // ---- after the window ---------------------------------------------------------------------------

    void AfterClose()
    {
        try
        {
            // uninstalled: this copy of the Setup goes too, once it has exited - and its folder if that
            // is now empty (rmdir without /s: settings kept on purpose keep the folder)
            if (finished && mode == Mode.Uninstall && Place.Same(Path.GetDirectoryName(Setup.Self), place.Dir))
            {
                string system = Environment.SystemDirectory;
                string command = "/d /c \"\"" + Path.Combine(system, "PING.EXE") + "\" 127.0.0.1 -n 3 >nul & del /f /q \"" + Setup.Self + "\"" +
                                 (place.OwnFolder ? " & rmdir \"" + place.Dir + "\"" : "") + "\"";
                Process.Start(new ProcessStartInfo(Path.Combine(system, "cmd.exe"), command)
                              { CreateNoWindow = true, UseShellExecute = false, WorkingDirectory = system });
            }
        }
        catch { }
    }

    // --selftest: every page built once, nothing shown, nothing started. Returns their names.
    public string BuildEveryPage()
    {
        var names = new List<string>();
        Welcome(); names.Add(head.Text);
        RunningPage(); names.Add(head.Text);
        UninstallWelcome(); names.Add(head.Text);
        Page("Installing LinkPilot"); names.Add(head.Text);
        return string.Join(" / ", names);
    }
}
