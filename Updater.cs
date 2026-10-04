// Updates: the only time LinkPilot goes online.
//
// It asks GitHub one question - "which are the newest releases of Husarp/linkpilot?" - and nothing
// is sent with it except what every web request carries. It asks by itself (unless that is switched
// off): 30 seconds after the dock starts, every 6 hours while it runs, and whenever the window comes
// to the front - at most every 5 minutes. And whenever you press Check now.
//
// Updating downloads LinkPilotSetup-X.Y.Z.exe from that release, runs it and closes LinkPilot: the
// Setup puts the new version in place of this one (your settings stay) and starts it again. The
// downloaded Setup is deleted the next time the dock starts.
//
// GitHub's answer is read with a pattern, not a JSON library: old copies (4.5.0) update by building
// this code with only Windows Forms and Drawing referenced, so it must not need anything more.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

static class Updater
{
    public const string Repo = "Husarp/linkpilot";
    const string Api = "https://api.github.com/repos/" + Repo + "/releases?per_page=20";
    // the only place an update is downloaded from - it is run afterwards
    const string Downloads = "https://github.com/" + Repo + "/releases/download/";
    public const string ReleasesPage = "https://github.com/" + Repo + "/releases";

    public static string Latest;     // the newest version GitHub reported since the dock started, or null
    public static string SetupUrl;   // its LinkPilotSetup-X.Y.Z.exe
    public static string Page;       // its release page
    public static bool Checking;
    public static DateTime LastReached = DateTime.MinValue;   // the last check that got an answer from GitHub
    public static string Message;    // the answer to Check now, or null
    public static bool MessageBad;   // ...and it is a problem
    public static bool BannerClosed; // the window's banner was closed with ✕ - until the dock starts again

    public enum Step { Idle, Downloading, Starting, Failed }
    public static Step Phase = Step.Idle;
    public static long Done, Total = -1;   // while downloading; Total is -1 while the size is unknown
    public static string Why;              // when it failed

    // Raised on the window thread whenever any of the above changes.
    public static event Action Changed;
    // Set by the dock: a control on the window thread, and how LinkPilot exits (to let the Setup work).
    public static Control Owner;
    public static Action Quit;

    static int asked;   // numbers the checks, so a late answer to an abandoned one is ignored
    static System.Windows.Forms.Timer giveUp, retry;

    public static string Current
    {
        get { var v = typeof(Updater).Assembly.GetName().Version; return v.Major + "." + v.Minor + "." + v.Build; }
    }

    public static bool UpdateWaiting { get { return Latest != null && IsNewer(Latest, Current); } }

    public static bool IsNewer(string a, string b)
    {
        Version va, vb;
        return Version.TryParse(a, out va) && Version.TryParse(b, out vb) && va > vb;
    }

    // A development copy - a git clone - is updated with git, never overwritten from a release.
    public static bool IsDevelopmentCopy { get { return Directory.Exists(Path.Combine(Config.Dir, ".git")); } }

    // ---- asking GitHub --------------------------------------------------------------------------

    public class Release { public string Version, Url, Page; }

    // The newest LinkPilotSetup-X.Y.Z.exe among GitHub's answer, or null. The recent releases are
    // read, not only the latest: an Android-only release on top would hide the Windows update. The
    // version comes from the file's name (the numbers in it), compared as numbers.
    public static Release Newest(string json)
    {
        Release best = null;
        Version bestVersion = null;
        foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
        {
            string url = m.Groups[1].Value;
            if (!url.StartsWith(Downloads, StringComparison.OrdinalIgnoreCase)) continue;
            string name = url.Substring(url.LastIndexOf('/') + 1);
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) < 0) continue;
            var numbers = Regex.Matches(name, "\\d+(\\.\\d+)+");
            if (numbers.Count == 0) continue;
            string text = numbers[numbers.Count - 1].Value;
            Version v;
            if (!Version.TryParse(text, out v) || (bestVersion != null && v <= bestVersion)) continue;
            string tag = url.Substring(Downloads.Length);
            if (tag.IndexOf('/') <= 0) continue;
            tag = tag.Substring(0, tag.IndexOf('/'));
            best = new Release { Version = text, Url = url, Page = "https://github.com/" + Repo + "/releases/tag/" + tag };
            bestVersion = v;
        }
        return best;
    }

    static string AskGitHub()
    {
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2, which GitHub requires
        var request = (HttpWebRequest)WebRequest.Create(Api);
        request.UserAgent = "BrowserSwitch/" + Current;
        request.Accept = "application/vnd.github+json";
        request.Timeout = 10000;
        request.ReadWriteTimeout = 10000;
        using (var response = request.GetResponse())
        using (var reader = new StreamReader(response.GetResponseStream()))
            return reader.ReadToEnd();
    }

    // By itself: only if allowed, one at a time, and not within 5 minutes of the last check that
    // reached GitHub. Says nothing if it fails - that is almost always "no internet".
    public static void AutoCheck()
    {
        if (!Config.CheckUpdates || Checking) return;
        if (LastReached != DateTime.MinValue && DateTime.Now >= LastReached && DateTime.Now - LastReached < TimeSpan.FromMinutes(5)) return;
        Ask(false);
    }

    // Check now: always asks, and gives up after 10 seconds even if the request itself hangs.
    public static void CheckNow()
    {
        Message = "Checking…";
        MessageBad = false;
        Ask(true);
        if (giveUp == null)
        {
            giveUp = new System.Windows.Forms.Timer { Interval = 10000 };
            giveUp.Tick += delegate
            {
                giveUp.Stop();
                if (!Checking) return;
                asked++;            // its answer, if it still comes, is ignored
                Checking = false;
                Message = "GitHub didn't answer within 10 seconds. The connection may be slow or blocked.";
                MessageBad = true;
                Tell();
            };
        }
        giveUp.Stop();
        giveUp.Start();
        Tell();
    }

    static void Ask(bool manual)
    {
        MakeOwner();
        Checking = true;
        int number = ++asked;
        new Thread(() =>
        {
            Release found = null;
            Exception problem = null;
            try { found = Newest(AskGitHub()); }
            catch (Exception e) { problem = e; }
            Post(() => Answered(number, manual, found, problem));
        }) { IsBackground = true }.Start();
    }

    static void Answered(int number, bool manual, Release found, Exception problem)
    {
        if (number != asked) return;
        Checking = false;
        if (giveUp != null) giveUp.Stop();
        // no answer at all - no internet, a firewall, a timeout: it cost GitHub nothing, so it does
        // not count toward the 5 minutes, and the automatic check tries again in 30 seconds
        var web = problem as WebException;
        bool reached = problem == null || web == null || web.Response != null;
        if (reached) LastReached = DateTime.Now;
        if (problem == null)
        {
            if (found != null) { Latest = found.Version; SetupUrl = found.Url; Page = found.Page; }
            if (manual)
            {
                MessageBad = false;
                Message = UpdateWaiting ? "LinkPilot " + Latest + " is available - you have " + Current + "."
                                        : "✓ You have the newest version (" + Current + ").";
            }
        }
        else if (manual) { Message = Describe(problem, false); MessageBad = true; }
        else if (!reached) RetrySoon();
        Tell();
    }

    static void RetrySoon()
    {
        if (retry == null)
        {
            retry = new System.Windows.Forms.Timer { Interval = 30000 };
            retry.Tick += delegate { retry.Stop(); AutoCheck(); };
        }
        retry.Stop();
        retry.Start();
    }

    // What went wrong, in words.
    public static string Describe(Exception e, bool downloading)
    {
        var web = e as WebException;
        if (web != null)
        {
            switch (web.Status)
            {
                case WebExceptionStatus.NameResolutionFailure:
                case WebExceptionStatus.ProxyNameResolutionFailure:
                case WebExceptionStatus.ConnectFailure:
                    return "Can't reach GitHub - no internet, or a firewall, VPN or antivirus is blocking LinkPilot.";
                case WebExceptionStatus.Timeout:
                    return downloading ? "GitHub stopped sending the update. The connection may be slow or blocked."
                                       : "GitHub didn't answer within 10 seconds. The connection may be slow or blocked.";
                case WebExceptionStatus.TrustFailure:
                case WebExceptionStatus.SecureChannelFailure:
                    return "Couldn't open a secure connection to GitHub. The network may be intercepting it, or the PC's date is wrong.";
            }
            var http = web.Response as HttpWebResponse;
            int code = http == null ? 0 : (int)http.StatusCode;
            if (code == 403 || code == 429) return "GitHub's limit of checks per hour was reached. Try again in a while.";
            if (code == 404) return downloading ? "The update file is no longer on GitHub." : "No release found on GitHub.";
        }
        if (downloading && e is IOException) return "Couldn't save the update - is the disk full?";
        return downloading ? "The download failed - the connection to GitHub dropped." : "Couldn't check for updates - the connection to GitHub failed.";
    }

    // ---- updating -------------------------------------------------------------------------------

    // Downloads the Setup into %TEMP% (with the progress shown), runs it and exits, so the Setup can
    // replace this copy. Pressing Update is the confirmation; nothing opens by itself if it fails.
    public static void Install()
    {
        if (Phase == Step.Downloading || Phase == Step.Starting) return;
        MakeOwner();
        if (IsDevelopmentCopy) { Fail("This copy is a development folder (a git clone) - update it with git pull and build.cmd."); return; }
        if (!UpdateWaiting || SetupUrl == null || !SetupUrl.StartsWith(Downloads, StringComparison.OrdinalIgnoreCase) ||
            !Regex.IsMatch(Latest, "^\\d+(\\.\\d+)+$"))
        {
            Fail("No update to download - press Check now.");
            return;
        }
        string url = SetupUrl, version = Latest, dir = Config.Dir;
        Phase = Step.Downloading;
        Done = 0;
        Total = -1;
        Tell();
        new Thread(() =>
        {
            string path = Path.Combine(Path.GetTempPath(), "LinkPilotSetup-" + version + ".exe");
            string part = path + ".part";
            try
            {
                Download(url, part);
                if (File.Exists(path)) File.Delete(path);
                File.Move(part, path);
            }
            catch (Exception e)
            {
                try { File.Delete(part); } catch { }
                string why = e is InvalidDataException ? e.Message : Describe(e, true);
                Post(() => Fail(why));
                return;
            }
            Post(() => Run(path, dir));
        }) { IsBackground = true }.Start();
    }

    static void Download(string url, string part)
    {
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
        var request = (HttpWebRequest)WebRequest.Create(url);   // GitHub sends it on to its download servers
        request.UserAgent = "BrowserSwitch/" + Current;
        request.AllowAutoRedirect = true;
        request.Timeout = 30000;
        request.ReadWriteTimeout = 60000;
        using (var response = request.GetResponse())
        using (var from = response.GetResponseStream())
        using (var to = File.Create(part))
        {
            long total = response.ContentLength, done = 0;
            var buffer = new byte[65536];
            int read, shown = -1;
            long shownBytes = 0;
            while ((read = from.Read(buffer, 0, buffer.Length)) > 0)
            {
                to.Write(buffer, 0, read);
                done += read;
                int percent = total > 0 ? (int)(done * 100 / total) : -1;
                // the window hears of it only when the number it shows changes
                if ((total > 0 && percent != shown) || (total <= 0 && done - shownBytes >= 256 * 1024))
                {
                    shown = percent;
                    shownBytes = done;
                    long d = done, t = total;
                    Post(() => { if (Phase == Step.Downloading) { Done = d; Total = t; Tell(); } });
                }
            }
            if (done < 100000 || (total > 0 && done != total))
                throw new InvalidDataException("The download stopped before it finished.");
        }
    }

    static void Run(string path, string dir)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path, "--update --dir \"" + dir.TrimEnd('\\') + "\"") { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Fail("Couldn't start the installer (" + e.Message + ").");
            return;
        }
        Phase = Step.Starting;
        Tell();
        // the dock exits as from its menu - after this click has finished
        if (Quit != null) Owner.BeginInvoke(Quit);
    }

    static void Fail(string why)
    {
        Phase = Step.Failed;
        Why = why;
        Tell();
    }

    // The release page of the newest version, or the list of releases.
    public static void OpenGitHub()
    {
        try { Process.Start(Page ?? ReleasesPage); } catch { }
    }

    // "Downloading… 42%", "Downloading… 3.1 MB" while the size is unknown, or "Downloading…".
    public static string Progress
    {
        get
        {
            if (Total > 0) return "Downloading… " + (int)(Done * 100 / Total) + "%";
            if (Done > 0) return "Downloading… " + (Done / 1048576.0).ToString("0.0") + " MB";
            return "Downloading…";
        }
    }

    // Setups downloaded before: deleted when the dock starts. One still open - the Setup that has
    // just started this copy may be closing - is tried once more a minute later.
    public static void RemoveOldDownloads()
    {
        if (!RemoveOld())
        {
            var later = new System.Windows.Forms.Timer { Interval = 60000 };
            later.Tick += delegate { later.Stop(); later.Dispose(); RemoveOld(); };
            later.Start();
        }
    }

    static bool RemoveOld()
    {
        bool all = true;
        try
        {
            foreach (string pattern in new[] { "LinkPilotSetup-*.exe", "LinkPilotSetup-*.exe.part" })
                foreach (string file in Directory.GetFiles(Path.GetTempPath(), pattern))
                    try { File.Delete(file); } catch { all = false; }
        }
        catch { }
        return all;
    }

    // ---- reaching the window thread -------------------------------------------------------------

    static void MakeOwner()
    {
        if (Owner != null) return;
        Owner = new Control();   // without a dock (a test), the thread that asked
        Owner.CreateControl();
    }

    static void Post(Action a)
    {
        try { Owner.BeginInvoke((MethodInvoker)(() => a())); } catch { }
    }

    static void Tell()
    {
        var c = Changed;
        if (c != null) c();
    }
}

// The strip under the window's header while a newer version waits: its number, Update and ✕ - or
// the download's progress, or what went wrong with Try again and GitHub. ✕ hides it until the dock
// next starts (signing in, or Exit and start), never only until the window opens again.
class UpdateBanner : Panel
{
    readonly Label head = new Label { AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), UseMnemonic = false };
    readonly Label more = new Label { AutoSize = false, AutoEllipsis = true, ForeColor = Ui.Muted, UseMnemonic = false, Height = 18 };
    readonly ProgressBar bar = new ProgressBar { Width = 160, Height = 10, Maximum = 100, Visible = false };
    readonly Button update, github, close;
    readonly ToolTip tip = new ToolTip();
    readonly Panel words = new Panel { Dock = DockStyle.Fill };

    public UpdateBanner()
    {
        Dock = DockStyle.Top;
        Height = 44;
        BackColor = Ui.HelpBack;
        Padding = new Padding(14, 0, 8, 0);
        Visible = false;
        update = Ui.Primary("Update", true);
        update.Click += delegate { Updater.Install(); };
        github = Ui.Button("GitHub", delegate { Updater.OpenGitHub(); });
        close = new Button { Text = "✕", Size = new Size(28, 28), FlatStyle = FlatStyle.Flat, ForeColor = Ui.Muted, Margin = new Padding(6, 0, 0, 0) };
        close.FlatAppearance.BorderSize = 0;
        close.Click += delegate { Updater.BannerClosed = true; Visible = false; };
        tip.SetToolTip(close, "Hide until LinkPilot starts again");

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, 7, 0, 0) };
        foreach (var b in new[] { github, update }) b.Margin = new Padding(6, 0, 0, 0);
        buttons.Controls.AddRange(new Control[] { github, update, close });
        words.Controls.AddRange(new Control[] { head, bar, more });
        words.Layout += delegate { Place(); };
        Controls.Add(words);
        Controls.Add(buttons);
        Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Ui.Line });
        Fill();
    }

    // head, then the bar while downloading, then the grey words - cut short with "…" if they are long
    void Place()
    {
        head.Location = new Point(0, (words.Height - head.Height) / 2);
        int x = head.Right + 6;
        if (bar.Visible) { bar.Location = new Point(x, (words.Height - bar.Height) / 2); x = bar.Right + 8; }
        more.Location = new Point(x, (words.Height - more.Height) / 2 + 1);
        more.Width = Math.Max(0, words.Width - x - 4);
    }

    public void Fill()
    {
        var phase = Updater.Phase;
        bar.Visible = phase == Updater.Step.Downloading;
        update.Visible = phase != Updater.Step.Starting;
        update.Text = phase == Updater.Step.Failed ? "Try again" : "Update";
        update.Enabled = phase != Updater.Step.Downloading;
        github.Visible = phase == Updater.Step.Failed;
        if (phase == Updater.Step.Downloading)
        {
            head.Text = "Downloading LinkPilot " + Updater.Latest + "…";
            more.Text = Updater.Progress.Replace("Downloading… ", "").Replace("Downloading…", "");
            bar.Style = Updater.Total > 0 ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
            if (Updater.Total > 0) bar.Value = (int)Math.Min(100, Updater.Done * 100 / Updater.Total);
        }
        else if (phase == Updater.Step.Starting) { head.Text = "Starting the installer…"; more.Text = "LinkPilot closes and starts again when it's done"; }
        else if (phase == Updater.Step.Failed) { head.Text = "Update failed:"; more.Text = Updater.Why; }
        else { head.Text = "LinkPilot " + Updater.Latest + " is available"; more.Text = "you have " + Updater.Current; }
        tip.SetToolTip(more, more.Text);
        Place();
    }
}

// The About & updates tab: the version, what "offline" means here, and updates.
class AboutPage : UserControl
{
    readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(640, 0), Margin = new Padding(3, 6, 3, 3), UseMnemonic = false };
    readonly Button check, get, github;
    readonly FlowLayoutPanel downloading;
    readonly ProgressBar bar = new ProgressBar { Width = 260, Height = 12, Maximum = 100, Margin = new Padding(3, 6, 8, 3) };
    readonly Label progress = new Label { AutoSize = true, Margin = new Padding(3, 4, 3, 3) };

    // showGuide: the short guide again; showSetup: the whole setup, from its first step
    public AboutPage(Action save, Action showSetup, Action showGuide)
    {
        Font = new Font("Segoe UI", 9F);
        Dock = DockStyle.Fill;
        Padding = Ui.PagePadding;
        var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var column = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Location = new Point(0, 0) };

        // what offline means
        column.Controls.Add(Heading("Works offline"));
        column.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(640, 0), Margin = new Padding(3, 2, 3, 2),
            Text = "Your links, categories, rules and the link log never leave this computer. LinkPilot connects to the internet " +
                   "only to ask GitHub (github.com) whether a newer version exists - when it starts and when you open this window, " +
                   "at most every 5 minutes, unless you switch that off below, or when you press Check now - and, when you press " +
                   "Update, to download the new version's installer. Nothing about your links or settings is sent; GitHub sees only " +
                   "what any visit to a website shows, such as your internet address." });

        // updates
        column.Controls.Add(Heading("Updates"));
        var auto = new CheckBox { Text = "Check for updates automatically", AutoSize = true, Checked = Config.CheckUpdates,
                                  Margin = new Padding(3, 6, 3, 0) };
        auto.CheckedChanged += delegate
        {
            Config.CheckUpdates = auto.Checked;
            save();
            if (auto.Checked) Updater.AutoCheck();
        };
        column.Controls.Add(auto);
        column.Controls.Add(new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(20, 0, 3, 4),
                                        Text = "When LinkPilot starts and whenever you open this window - at most every 5 minutes." });
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        row.Controls.Add(new Label { Text = "Version " + Updater.Current, AutoSize = true, Font = new Font(Font, FontStyle.Bold),
                                     Margin = new Padding(3, 7, 10, 3) });
        check = Ui.Button("Check now", delegate { Updater.CheckNow(); });
        row.Controls.Add(check);
        column.Controls.Add(row);
        column.Controls.Add(status);
        downloading = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 0), Visible = false };
        downloading.Controls.Add(bar);
        downloading.Controls.Add(progress);
        column.Controls.Add(downloading);
        var row2 = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
        github = Ui.Button("GitHub", delegate { Updater.OpenGitHub(); });
        get = Ui.Primary("Get update", true);
        get.Margin = new Padding(6, 3, 3, 3);
        get.Click += delegate { Updater.Install(); };
        row2.Controls.Add(github);
        row2.Controls.Add(get);
        column.Controls.Add(row2);
        column.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(640, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(3, 6, 3, 2),
            Text = "Updating downloads LinkPilotSetup-X.Y.Z.exe from GitHub and runs it: LinkPilot closes, the new version takes " +
                   "its place and starts again. Your settings stay. The one-command install (get.ps1) still works too, and builds " +
                   "it from the source code instead." });

        // the window
        column.Controls.Add(Heading("Window"));
        var taskbar = new CheckBox { Text = "Show a taskbar button while this window is open", AutoSize = true, Checked = Config.TaskbarButton,
                                     Margin = new Padding(3, 6, 3, 3) };
        taskbar.CheckedChanged += delegate
        {
            Config.TaskbarButton = taskbar.Checked;
            save();
            var form = FindForm();
            if (form != null) form.ShowInTaskbar = taskbar.Checked;
        };
        column.Controls.Add(taskbar);
        column.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(640, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(3, 0, 3, 2),
            Text = "Either way LinkPilot keeps running next to the clock after the window is closed - click its icon there " +
                   "(under the ^ arrow, if Windows has hidden it) to open this window again." });

        // more
        column.Controls.Add(Heading("More"));
        var guide = new LinkLabel { Text = "Take the short guide again", AutoSize = true, Margin = new Padding(3, 6, 3, 3) };
        guide.LinkClicked += delegate { showGuide(); };
        var setup = new LinkLabel { Text = "Show the setup screen again", AutoSize = true };
        setup.LinkClicked += delegate { showSetup(); };
        var code = new LinkLabel { Text = "LinkPilot on GitHub - the code, releases, and where to report a problem", AutoSize = true };
        code.LinkClicked += delegate { try { Process.Start("https://github.com/" + Updater.Repo); } catch { } };
        column.Controls.Add(guide);
        column.Controls.Add(setup);
        column.Controls.Add(code);
        body.Controls.Add(column);
        Controls.Add(body);
        Controls.Add(Ui.PageHeader("LinkPilot " + Updater.Current, "Decides which browser - and which profile - every link opens in.", null));

        // this page is built again whenever config.txt changes: it listens only while it exists
        Action changed = Fill;
        HandleCreated += delegate { Updater.Changed -= changed; Updater.Changed += changed; };
        Disposed += delegate { Updater.Changed -= changed; };
        Fill();
    }

    // a section's name, with room above it
    static Control Heading(string text)
    {
        var h = Ui.Heading(text, null);
        h.Margin = new Padding(0, 12, 3, 2);
        return h;
    }

    void Fill()
    {
        var phase = Updater.Phase;
        bool waiting = Updater.UpdateWaiting;
        check.Enabled = !Updater.Checking;
        get.Visible = waiting && phase != Updater.Step.Starting;
        get.Text = phase == Updater.Step.Failed ? "Try again" : "Get update";
        get.Enabled = phase != Updater.Step.Downloading;
        downloading.Visible = phase == Updater.Step.Downloading;
        if (phase == Updater.Step.Downloading)
        {
            bar.Style = Updater.Total > 0 ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
            if (Updater.Total > 0) bar.Value = (int)Math.Min(100, Updater.Done * 100 / Updater.Total);
            progress.Text = Updater.Progress;
        }

        if (phase == Updater.Step.Failed) Say("Update failed: " + Updater.Why, Ui.Warn);
        else if (phase == Updater.Step.Starting) Say("Starting the installer… LinkPilot closes and starts again when it's done.", SystemColors.GrayText);
        else if (Updater.Message != null && !(Updater.Message.StartsWith("✓") && waiting))
            Say(Updater.Message, Updater.MessageBad ? Ui.Warn : Updater.Checking ? SystemColors.GrayText : Ui.Ok);
        else if (waiting) Say("LinkPilot " + Updater.Latest + " is available - you have " + Updater.Current + ".", Ui.Ok);
        else if (Updater.LastReached != DateTime.MinValue) Say("Last checked " + Updater.LastReached.ToString("HH:mm") + ".", SystemColors.GrayText);
        else Say("Not checked yet.", SystemColors.GrayText);
    }

    void Say(string text, Color colour)
    {
        status.Text = text;
        status.ForeColor = colour;
    }
}
