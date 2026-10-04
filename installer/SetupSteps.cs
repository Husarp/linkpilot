// The work LinkPilotSetup.exe does - on its own thread, never touching the window. It only posts
// what happens ("step", "file", "log", "progress", "done", "failed") into a queue the window reads.
//
// Install / update, in steps:
//   0. closing LinkPilot (an update first waits for it to exit by itself - it started this Setup)
//   1. copying its files - each old one is first moved into .update-backup, so it can be put back
//   2. registering it with Windows and adding its shortcuts - install.ps1 -Quiet, the same script
//      the zip's Install.cmd and get.ps1 run, so there is one registration and not two
//   3. adding it to Apps & features - this Setup becomes its uninstaller
//   4. starting it again (an update; an install starts it from the Finish page)
// Anything failing puts the old files back, so the old version still works.
//
// Your settings and link log live in the program's folder, next to BrowserSwitch.exe - every copy
// of LinkPilot has always kept them there. They are never touched: only the files named in Payload
// are replaced, and nothing else in the folder is emptied or removed (except by Uninstall, with
// "Also delete my settings and link log" ticked, and then into the Recycle Bin).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

enum Mode { Install, Update, Uninstall }

// The files inside this Setup, and the version they are.
static class Payload
{
    public static readonly string[] Files = { "BrowserSwitch.exe", "install.ps1", "uninstall.ps1", "Install.cmd", "Back to normal.cmd", "README.md", "LICENSE" };
    public const string Uninstaller = "Uninstall LinkPilot.exe";

    public static byte[] Read(string name)
    {
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        {
            if (s == null) return null;
            var copy = new MemoryStream();
            s.CopyTo(copy);
            return copy.ToArray();
        }
    }

    // "4.6.0" - read from the BrowserSwitch.exe inside, so the Setup can never say another version
    // than the program it installs. (The version is written in one place: BrowserSwitch.cs.)
    static string version;
    public static string Version
    {
        get
        {
            if (version != null) return version;
            var exe = Read("BrowserSwitch.exe");
            if (exe == null) return null;
            var v = Assembly.ReflectionOnlyLoad(exe).GetName().Version;
            return version = v.Major + "." + v.Minor + "." + v.Build;
        }
    }
}

// Where LinkPilot is, or goes, and what is there now.
class Place
{
    public const string Key = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BrowserSwitch";
    public string Dir;
    public string Installed;   // the version there now, or null
    public string MoveFrom;    // the folder of the time it was called Browser Switch, moved here first
    public bool Dev;           // a git clone: updated with git, never by this

    public string Exe { get { return Path.Combine(Dir, "BrowserSwitch.exe"); } }

    public static string Programs(string name)
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", name);
    }

    // The folder: the one asked for (--dir, from the app's own updater), else where it is
    // registered (if it is still there), else %LOCALAPPDATA%\Programs\LinkPilot. The uninstaller
    // works on its own folder.
    public static Place Find(string asked, Mode mode)
    {
        string regDir = null, regVersion = null;
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(Key))
                if (k != null) { regDir = k.GetValue("InstallLocation") as string; regVersion = k.GetValue("DisplayVersion") as string; }
        }
        catch { }
        string own = Path.GetDirectoryName(Path.GetFullPath(Setup.Self));
        var p = new Place();
        if (asked != null) p.Dir = asked;
        else if (mode == Mode.Uninstall && File.Exists(Path.Combine(own, "BrowserSwitch.exe"))) p.Dir = own;
        else if (!string.IsNullOrEmpty(regDir) && File.Exists(Path.Combine(regDir, "BrowserSwitch.exe"))) p.Dir = regDir;
        else
        {
            p.Dir = Programs("LinkPilot");
            string old = Programs("Browser Switch");
            if (!Directory.Exists(p.Dir) && File.Exists(Path.Combine(old, "BrowserSwitch.exe"))) p.MoveFrom = old;
        }
        p.Dir = Path.GetFullPath(p.Dir).TrimEnd('\\');
        if (p.Dir.EndsWith(":")) p.Dir += "\\";
        p.Dev = Directory.Exists(Path.Combine(p.Dir, ".git"));
        string exe = p.MoveFrom != null ? Path.Combine(p.MoveFrom, "BrowserSwitch.exe") : p.Exe;
        if (regVersion != null && regDir != null && Same(regDir, p.Dir) && File.Exists(exe)) p.Installed = regVersion;
        else if (File.Exists(exe))
            try { var v = FileVersionInfo.GetVersionInfo(exe); p.Installed = v.FileMajorPart + "." + v.FileMinorPart + "." + v.FileBuildPart; }
            catch { }
        return p;
    }

    public static bool Same(string a, string b)
    {
        try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    // Its own folder - as uninstall.ps1 decides: one the Setup or get.ps1 made, not a git clone.
    // Any other folder (one you unzipped or built yourself) is left as it is by Uninstall.
    public bool OwnFolder
    {
        get { return !Dev && (Same(Dir, Programs("LinkPilot")) || Same(Dir, Programs("Browser Switch"))); }
    }

    public static bool IsNewer(string a, string b)
    {
        Version va, vb;
        return Version.TryParse(a ?? "", out va) && Version.TryParse(b ?? "", out vb) && va > vb;
    }
}

// Whether LinkPilot runs, and closing it.
static class App
{
    const string Dock = @"Local\BrowserSwitch.Dock", QuitSignal = @"Local\BrowserSwitch.Quit";

    public static bool DockRunning()
    {
        try { using (Mutex.OpenExisting(Dock)) return true; }
        catch { return false; }
    }

    // BrowserSwitch.exe running from one of these folders: the dock, an Ask every time window, a link
    // being handed on.
    public static List<Process> In(params string[] dirs)
    {
        var found = new List<Process>();
        foreach (var p in Process.GetProcessesByName("BrowserSwitch"))
        {
            try
            {
                string folder = Path.GetDirectoryName(p.MainModule.FileName);
                if (dirs.Any(d => d != null && Place.Same(d, folder))) { found.Add(p); continue; }
            }
            catch { }
            p.Dispose();
        }
        return found;
    }

    public static bool Running(Place place)
    {
        if (DockRunning()) return true;
        var list = In(place.Dir, place.MoveFrom);
        bool any = list.Count > 0;
        foreach (var p in list) p.Dispose();
        return any;
    }

    // An update waits for it to exit by itself first: the app started this Setup and is closing.
    // Then the dock is asked to exit (it takes its icons away cleanly); 4.5.0 and earlier have no
    // such signal, so what is still running is stopped - as get.ps1 and uninstall.ps1 always did.
    public static void Close(Place place, int waitSeconds, Action<string> log)
    {
        var until = DateTime.Now.AddSeconds(waitSeconds);
        while (Running(place) && DateTime.Now < until) Thread.Sleep(250);
        if (!Running(place)) return;
        try
        {
            using (var quit = EventWaitHandle.OpenExisting(QuitSignal)) quit.Set();
            log("Asked LinkPilot to close.");
            until = DateTime.Now.AddSeconds(5);
            while (DockRunning() && DateTime.Now < until) Thread.Sleep(200);
        }
        catch (WaitHandleCannotBeOpenedException) { }
        catch (Exception e) { log("Couldn't ask LinkPilot to close: " + e.Message); }
        var left = In(place.Dir, place.MoveFrom);
        // a dock still there runs from somewhere else - an old copy: it goes too, only one runs at a time
        if (DockRunning()) foreach (var p in Process.GetProcessesByName("BrowserSwitch")) if (!left.Any(x => x.Id == p.Id)) left.Add(p);
        foreach (var p in left)
        {
            try { p.Kill(); p.WaitForExit(5000); log("Stopped BrowserSwitch.exe (process " + p.Id + ")."); }
            catch (Exception e) { log("Couldn't stop process " + p.Id + ": " + e.Message); }
            p.Dispose();
        }
    }

    public static void Start(string exe, string args)
    {
        Process.Start(new ProcessStartInfo(exe, args) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = true });
    }
}

// Windows' default browser, and the one used before LinkPilot - as uninstall.ps1 finds them.
static class Defaults
{
    // Windows 11 24H2 and later record the choice in UserChoiceLatest and can leave the older
    // UserChoice unchanged, so the newer record wins whenever it exists.
    public static bool LinkPilotIsDefault
    {
        get
        {
            const string https = @"SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\https\";
            try
            {
                string id = ProgId(https + @"UserChoiceLatest\ProgId") ?? ProgId(https + "UserChoice");
                return id != null && id.StartsWith("BrowserSwitch", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }

    static string ProgId(string key)
    {
        using (var k = Registry.CurrentUser.OpenSubKey(key))
            return k == null ? null : k.GetValue("ProgId") as string;
    }

    // The browser used before LinkPilot: its program is "fallback=" in config.txt; its name and
    // the id Settings knows it by come from the list of registered browsers. {id or null, name}, or null.
    public static string[] Previous(string dir)
    {
        string exe = null;
        try
        {
            string config = Path.Combine(dir, "config.txt");
            if (File.Exists(config))
                foreach (string line in File.ReadAllLines(config))
                    if (line.StartsWith("fallback=", StringComparison.OrdinalIgnoreCase)) { exe = line.Substring(9).Trim(); break; }
        }
        catch { }
        if (string.IsNullOrEmpty(exe)) return null;
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            try
            {
                using (var registered = hive.OpenSubKey(@"Software\RegisteredApplications"))
                {
                    if (registered == null) continue;
                    foreach (string id in registered.GetValueNames())
                    {
                        string capabilities = registered.GetValue(id) as string ?? "";
                        if (id.StartsWith("BrowserSwitch", StringComparison.OrdinalIgnoreCase) ||
                            capabilities.IndexOf("StartMenuInternet", StringComparison.OrdinalIgnoreCase) < 0) continue;   // browsers only
                        int cut = capabilities.LastIndexOf('\\');
                        if (cut <= 0) continue;
                        string client = capabilities.Substring(0, cut);
                        using (var open = hive.OpenSubKey(client + @"\shell\open\command"))
                        using (var named = hive.OpenSubKey(client))
                        {
                            string command = open == null ? null : open.GetValue("") as string;
                            if (command == null || command.IndexOf(exe, StringComparison.OrdinalIgnoreCase) < 0) continue;
                            string label = named == null ? null : named.GetValue("") as string;
                            return new[] { id, string.IsNullOrEmpty(label) ? id : label };
                        }
                    }
                }
            }
            catch { }
        return new[] { null, Path.GetFileNameWithoutExtension(exe) };
    }
}

class Work
{
    [DllImport("shell32.dll")] static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom, pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

    // What the window reads: {kind, value}.
    public readonly ConcurrentQueue<object[]> Posted = new ConcurrentQueue<object[]>();

    readonly Mode mode;
    readonly Place place;
    readonly string version;
    public bool DeleteData;      // Uninstall: settings and link log to the Recycle Bin too
    public bool ClosedApp;       // it was running and this closed it
    public bool Again;           // Try again after the old version was started again: no waiting for it to exit by itself
    public string PutBackResult; // after a failure: "restored", "removed", "nothing" or "partial"
    readonly int[] weights;

    // what this run changed, to put back if it fails
    readonly List<string> moved = new List<string>(), written = new List<string>();
    string writing;
    bool createdDir;
    Before before;   // set while install.ps1 runs and until Apps & features is written

    // What Windows had before install.ps1 ran: which shortcuts were there, whether LinkPilot was
    // registered as a browser, config.txt, and the Apps & features entry as it was (null: none).
    class Before
    {
        public bool Desktop, Startup, StartMenu, Handler, Config;
        public Dictionary<string, object[]> Listed;
    }

    static string DesktopLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Switch browser.lnk"); } }
    static string StartupLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "LinkPilot.lnk"); } }
    static string StartMenuLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\LinkPilot.lnk"); } }

    static readonly string[] Leftovers = { "selftest.txt", "detected.txt", "dry-run.log", "get.ps1", "build.cmd", "BrowserSwitch.ico", "setup-selftest.txt" };
    // the person's own: kept unless "Also delete my settings and link log" is ticked
    static readonly string[] Data = { "config.txt", "link-log.txt", "recent-apps.txt", "errors.log" };

    public Work(Mode mode, Place place, string version)
    {
        this.mode = mode;
        this.place = place;
        this.version = version;
        weights = mode == Mode.Uninstall ? new[] { 10, 30, 20, 30, 10 } : new[] { 5, 35, 45, 5, 10 };
    }

    string Backup { get { return Path.Combine(place.Dir, ".update-backup"); } }

    void Post(string kind, object value) { Posted.Enqueue(new[] { kind, value }); }
    void Log(string line) { Post("log", line); }

    // The bar: where this step's share starts plus how far into it, and where the step ends - the
    // window lets the bar creep towards that end while a step says nothing.
    void At(int step, double fraction)
    {
        double total = weights.Sum(), before = weights.Take(step).Sum();
        Post("progress", new[] { (before + weights[step] * Math.Min(1, fraction)) / total, (before + weights[step]) / total });
    }

    void Step(int step, string text)
    {
        At(step, 0);
        Post("step", text);
        Post("file", "");
        Log(text + "…");
    }

    public void Run()
    {
        moved.Clear(); written.Clear(); writing = null; createdDir = false; PutBackResult = null; before = null;
        try
        {
            if (mode == Mode.Uninstall) Uninstall(); else Install();
            Post("done", null);
        }
        catch (Exception e)
        {
            Log("ERROR: " + e);
            if (mode != Mode.Uninstall)
            {
                if (before != null) UndoRegister();
                PutBackResult = PutBack();
                Log("Put back: " + PutBackResult);
            }
            Post("failed", e.Message);
        }
    }

    // ---- install and update -----------------------------------------------------------------------

    void Install()
    {
        Log("LinkPilot " + version + " into " + place.Dir + (place.Installed != null ? " (now " + place.Installed + ")" : ""));
        if (place.Dev) throw new Exception("This folder is a development copy (a git clone) - update it with git pull and build.cmd.");

        Step(0, "Closing LinkPilot");
        bool wasRunning = App.Running(place);
        App.Close(place, mode == Mode.Update && !Again ? 15 : 0, Log);
        if (wasRunning) ClosedApp = true;
        if (place.MoveFrom != null && !Directory.Exists(place.Dir))
        {
            Log("Moving " + place.MoveFrom + " to " + place.Dir + " (its name before)");
            Retry(() => Directory.Move(place.MoveFrom, place.Dir), 10);
            place.MoveFrom = null;
        }

        Step(1, "Copying LinkPilot");
        if (!Directory.Exists(place.Dir)) { Directory.CreateDirectory(place.Dir); createdDir = true; }
        if (Directory.Exists(Backup)) Directory.Delete(Backup, true);   // left by a run that was cut off
        UntilFree(place.Exe);
        var names = new List<string>(Payload.Files);
        bool selfIsUninstaller = Place.Same(Setup.Self, Path.Combine(place.Dir, Payload.Uninstaller));
        if (!selfIsUninstaller) names.Add(Payload.Uninstaller);
        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i], target = Path.Combine(place.Dir, name);
            Post("file", name);
            At(1, (double)i / names.Count);
            byte[] data = name == Payload.Uninstaller ? File.ReadAllBytes(Setup.Self) : Payload.Read(name);
            if (data == null) throw new Exception(name + " is missing from this Setup.");
            if (File.Exists(target))
            {
                Directory.CreateDirectory(Backup);
                string to = Path.Combine(Backup, name);
                // Windows lets a running program be renamed - a link clicked just now cannot block this
                Retry(() => File.Move(target, to), 10);
                moved.Add(name);
            }
            writing = target + ".new";
            File.WriteAllBytes(writing, data);
            File.Move(writing, target);
            writing = null;
            written.Add(name);
            Log("  " + name);
        }

        Step(2, "Registering LinkPilot with Windows and adding its shortcuts");
        // shortcuts you removed stay removed by an update; a new install or a reinstall makes them all
        bool keepRemoved = place.Installed != null && (mode == Mode.Update || Place.IsNewer(version, place.Installed));
        before = new Before
        {
            Desktop = File.Exists(DesktopLnk), Startup = File.Exists(StartupLnk), StartMenu = File.Exists(StartMenuLnk),
            Config = File.Exists(Path.Combine(place.Dir, "config.txt")), Listed = ReadKey(Place.Key)
        };
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\BrowserSwitchURL")) before.Handler = k != null;
        Register();
        if (keepRemoved)
        {
            if (!before.Desktop && File.Exists(DesktopLnk)) { File.Delete(DesktopLnk); Log("  the Desktop shortcut stays removed"); }
            if (!before.Startup && File.Exists(StartupLnk)) { File.Delete(StartupLnk); Log("  the Startup shortcut stays removed - LinkPilot does not start with Windows"); }
        }

        Step(3, "Adding LinkPilot to Apps & features");
        using (var k = Registry.CurrentUser.CreateSubKey(Place.Key))
        {
            k.SetValue("UninstallString", "\"" + Path.Combine(place.Dir, Payload.Uninstaller) + "\" --uninstall");
            k.SetValue("Publisher", "Husarp");
            long bytes = Payload.Files.Concat(new[] { Payload.Uninstaller }).Select(n => Path.Combine(place.Dir, n))
                                      .Where(File.Exists).Sum(f => new FileInfo(f).Length);
            k.SetValue("EstimatedSize", (int)(bytes / 1024), RegistryValueKind.DWord);
        }
        // done: what was replaced is not needed any more
        moved.Clear(); written.Clear(); before = null;
        try { if (Directory.Exists(Backup)) Directory.Delete(Backup, true); } catch (Exception e) { Log("  couldn't remove .update-backup: " + e.Message); }

        if (mode == Mode.Update)
        {
            Step(4, "Starting LinkPilot");
            App.Start(place.Exe, "");
        }
        At(weights.Length - 1, 1);
        Log("Done.");
    }

    // install.ps1 -Quiet: the registration as a browser, the Start menu, Desktop and Startup
    // shortcuts, Apps & features, the browser used so far into config.txt (only when there is none).
    // PowerShell by its full address - a lost PATH entry makes "powershell.exe" alone unknown.
    void Register()
    {
        string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
        string script = Path.Combine(place.Dir, "install.ps1");
        var psi = new ProcessStartInfo(powershell, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\" -Quiet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = place.Dir
        };
        string firstError = null;
        using (var p = new Process { StartInfo = psi })
        {
            p.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data) && e.Data.Trim().Length > 0) Log("  " + e.Data.TrimEnd()); };
            p.ErrorDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data) || e.Data.Trim().Length == 0) return;
                if (firstError == null) firstError = e.Data.Trim();
                Log("  " + e.Data.TrimEnd());
            };
            try { p.Start(); }
            catch (Exception e) { throw new Exception("Couldn't start Windows PowerShell to register LinkPilot (" + e.Message + ")."); }
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            if (!p.WaitForExit(120000))
            {
                try { p.Kill(); } catch { }
                throw new Exception("Registering LinkPilot with Windows took more than 2 minutes and was stopped.");
            }
            p.WaitForExit();   // the last lines of its output
            if (p.ExitCode != 0)
                throw new Exception("Registering LinkPilot with Windows failed: " + (firstError ?? "install.ps1 stopped with code " + p.ExitCode + "."));
        }
    }

    // Right before the first file moves: BrowserSwitch.exe must open for writing - nothing holds it.
    void UntilFree(string exe)
    {
        if (!File.Exists(exe)) return;
        for (int i = 0; ; i++)
        {
            try { using (new FileStream(exe, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) return; }
            catch (IOException)
            {
                if (i >= 20) throw new Exception("BrowserSwitch.exe is still in use by another program (maybe antivirus).");
                Thread.Sleep(500);
            }
        }
    }

    // A file can stay locked for a moment after its program exits.
    static void Retry(Action action, int times)
    {
        for (int i = 0; ; i++)
        {
            try { action(); return; }
            catch (IOException) { if (i >= times - 1) throw; Thread.Sleep(500); }
            catch (UnauthorizedAccessException) { if (i >= times - 1) throw; Thread.Sleep(500); }
        }
    }

    // After a failure past install.ps1 (it writes as it goes): the shortcuts it made come off again
    // and Apps & features is as it was. A new install that is taken back also leaves no browser
    // registration and no config.txt behind - nothing would be there for them to point at.
    void UndoRegister()
    {
        var b = before;
        before = null;
        bool fresh = moved.Count == 0;   // nothing was there before to put back
        try
        {
            foreach (var lnk in new[] { b.Desktop ? null : DesktopLnk, b.Startup ? null : StartupLnk, b.StartMenu ? null : StartMenuLnk })
                if (lnk != null && File.Exists(lnk)) { File.Delete(lnk); Log("  taken back: " + lnk); }
            Registry.CurrentUser.DeleteSubKeyTree(Place.Key, false);
            if (b.Listed != null)
                using (var k = Registry.CurrentUser.CreateSubKey(Place.Key))
                    foreach (var v in b.Listed) k.SetValue(v.Key, v.Value[0], (RegistryValueKind)v.Value[1]);
            Log("  taken back: Apps & features as it was");
            if (fresh && !b.Handler)
            {
                foreach (string key in new[] { @"Software\Classes\BrowserSwitchURL", @"Software\Clients\StartMenuInternet\BrowserSwitch" })
                    Registry.CurrentUser.DeleteSubKeyTree(key, false);
                using (var registered = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
                    if (registered != null) registered.DeleteValue("BrowserSwitch", false);
                try { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); } catch { }
                Log("  taken back: the registration as a browser");
            }
            string config = Path.Combine(place.Dir, "config.txt");
            if (fresh && !b.Config && File.Exists(config)) File.Delete(config);
        }
        catch (Exception e) { Log("  couldn't take everything back: " + e.Message); }
    }

    // All the values of a key under HKEY_CURRENT_USER - {value, kind} by name - or null if it isn't there.
    static Dictionary<string, object[]> ReadKey(string path)
    {
        using (var k = Registry.CurrentUser.OpenSubKey(path))
        {
            if (k == null) return null;
            var values = new Dictionary<string, object[]>();
            foreach (string name in k.GetValueNames())
                values[name] = new object[] { k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), k.GetValueKind(name) };
            return values;
        }
    }

    // After a failure: what was written goes, what was there before comes back.
    string PutBack()
    {
        bool whole = true;
        if (writing != null) try { File.Delete(writing); } catch { }
        foreach (string name in written)
            try { File.Delete(Path.Combine(place.Dir, name)); } catch { whole = false; }
        foreach (string name in moved)
            try { string target = Path.Combine(place.Dir, name); Retry(() => File.Move(Path.Combine(Backup, name), target), 6); }
            catch { whole = false; }
        if (!whole) return "partial";
        try { if (Directory.Exists(Backup)) Directory.Delete(Backup, true); } catch { }
        if (moved.Count > 0) return "restored";
        if (createdDir) try { if (!Directory.EnumerateFileSystemEntries(place.Dir).Any()) Directory.Delete(place.Dir); } catch { }
        return written.Count > 0 ? "removed" : "nothing";
    }

    // ---- uninstall ----------------------------------------------------------------------------------

    void Uninstall()
    {
        Log("Removing LinkPilot from " + place.Dir);
        Step(0, "Closing LinkPilot");
        bool wasRunning = App.Running(place);
        App.Close(place, 0, Log);
        if (wasRunning) ClosedApp = true;

        Step(1, "Removing LinkPilot from Windows");
        foreach (string key in new[] { @"Software\Classes\BrowserSwitchURL", @"Software\Clients\StartMenuInternet\BrowserSwitch", Place.Key })
        {
            Registry.CurrentUser.DeleteSubKeyTree(key, false);
            Log("  HKCU\\" + key);
        }
        using (var registered = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
            if (registered != null)
                foreach (string name in new[] { "BrowserSwitch", "Browser Switch" }) registered.DeleteValue(name, false);
        try { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); } catch { }   // associations changed
        At(1, 1);

        Step(2, "Removing its shortcuts");
        string programs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs");
        string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        foreach (string lnk in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Switch browser.lnk"),
                                       Path.Combine(programs, "LinkPilot.lnk"), Path.Combine(startup, "LinkPilot.lnk"),
                                       Path.Combine(programs, "Browser Switch.lnk"), Path.Combine(startup, "Browser Switch.lnk") })
            if (File.Exists(lnk)) { File.Delete(lnk); Log("  " + lnk); }
        At(2, 1);

        Step(3, "Removing its files");
        if (!place.OwnFolder)
        {
            string uninstaller = Path.Combine(place.Dir, Payload.Uninstaller);
            if (File.Exists(uninstaller) && !Place.Same(Setup.Self, uninstaller)) { File.Delete(uninstaller); Log("  " + Payload.Uninstaller); }
            Log("  The folder " + place.Dir + " is left as it is.");
        }
        else
        {
            var files = Payload.Files.Concat(Leftovers).Select(n => Path.Combine(place.Dir, n)).ToList();
            if (!Place.Same(Setup.Self, Path.Combine(place.Dir, Payload.Uninstaller))) files.Add(Path.Combine(place.Dir, Payload.Uninstaller));
            try { files.AddRange(Directory.GetFiles(place.Dir, "*.cs")); } catch { }   // left by get.ps1 before
            for (int i = 0; i < files.Count; i++)
            {
                At(3, (double)i / files.Count);
                if (!File.Exists(files[i])) continue;
                Post("file", Path.GetFileName(files[i]));
                Retry(() => File.Delete(files[i]), 6);
                Log("  " + Path.GetFileName(files[i]));
            }
            foreach (string dir in new[] { "file-icons", ".update-backup" })
            {
                string path = Path.Combine(place.Dir, dir);
                if (Directory.Exists(path)) { Directory.Delete(path, true); Log("  " + dir + "\\"); }
            }
            if (DeleteData)
            {
                var data = Data.Select(n => Path.Combine(place.Dir, n)).Where(File.Exists).ToList();
                try { data.AddRange(Directory.GetDirectories(place.Dir, "reset-backup-*")); } catch { }
                if (data.Count > 0)
                {
                    Post("file", "your settings and link log");
                    ToRecycleBin(data);
                    foreach (var d in data) Log("  " + Path.GetFileName(d) + " - to the Recycle Bin");
                }
            }
        }
        At(3, 1);
        Step(4, "Finishing");
        At(4, 1);
        Log("Done.");
    }

    static void ToRecycleBin(List<string> paths)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = 3,                              // FO_DELETE
            pFrom = string.Join("\0", paths) + "\0\0",
            fFlags = 0x40 | 0x10 | 0x4 | 0x4000     // FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_WANTNUKEWARNING
        };
        int result = SHFileOperation(ref op);
        if (result != 0 && !op.fAnyOperationsAborted) throw new Exception("Couldn't move your settings to the Recycle Bin (error " + result + ").");
    }
}
