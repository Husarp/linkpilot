// Link rules: send a link to a category by the app it came from (Signal -> Work), by its address
// (github.com -> Home) or by a keyword in its address (invoice -> Work). Rules win over the live category: the first rule that matches decides, and a
// link no rule matches goes to the live category as before.
//
// The app is known because Windows starts LinkPilot from inside the program that opened the
// link, so that program is LinkPilot's parent process. A few programs - mostly Store apps - hand
// links over through a Windows go-between, and then the go-between is all that can be seen.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Router
{
    static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

    // The rule a link falls under, or null. A rule whose category is gone or has no browser is passed
    // over, so a link is never sent nowhere. Ask every time counts as having one while any other
    // category does.
    public static Rule Decide(string url, string source)
    {
        if (!Config.RulesOn) return null;
        foreach (var r in Config.Rules)
        {
            if (!r.On) continue;
            var c = Config.Categories.FirstOrDefault(x => Same(x.Name, r.Category));
            if (c == null || !c.Works()) continue;
            if (r.ByApp ? FromApp(r, source) : r.ByWord ? HasWord(r.Match, url) : HasAddress(r.Match, url)) return r;
        }
        return null;
    }

    static bool FromApp(Rule r, string source)
    {
        return !string.IsNullOrEmpty(source) && r.Match.Split(';').Any(m => Same(m.Trim(), source));
    }

    // "github.com" matches github.com and any site under it (gist.github.com, www.github.com). Text
    // with a "/" in it is looked for anywhere in the address ("github.com/my-company").
    public static bool HasAddress(string pattern, string url)
    {
        string p = (pattern ?? "").Trim().ToLowerInvariant();
        if (p.Length == 0 || string.IsNullOrEmpty(url)) return false;
        string u = url.ToLowerInvariant();
        if (p.Contains("/")) return u.Contains(p);
        if (p.StartsWith("*.")) p = p.Substring(2);
        Uri parsed;
        if (!Uri.TryCreate(url, UriKind.Absolute, out parsed) || string.IsNullOrEmpty(parsed.Host)) return u.Contains(p);
        string host = parsed.Host.ToLowerInvariant();
        return host == p || host.EndsWith("." + p);
    }

    // A keyword rule: "invoice" matches a whole word in the site, the path, or a value after the ?
    // (example.com/invoice/12, ?q=my+invoice) - never part of a word (invoices), nor a parameter's
    // name (?invoice=1). Several words must come one after another ("pull request": .../pull-request).
    // Big or small letters do not matter. The same as on Android (Router.hasWord there).
    public static bool HasWord(string keyword, string url)
    {
        string k = Keyword(keyword);
        if (k == null || string.IsNullOrEmpty(url)) return false;
        string[] want = k.Split(' ');
        return LinkWords(url).Any(part => InARow(part, want));
    }

    // What someone typed for a keyword, as a rule matches it: its words in small letters, one space
    // between ("Pull-Request" -> "pull request"). Null if it has fewer than 3 letters and digits.
    public static string Keyword(string text)
    {
        string k = string.Join(" ", Words(text));
        return k.Replace(" ", "").Length >= 3 ? k : null;
    }

    static readonly Regex NotWord = new Regex(@"[^\p{L}\p{Nd}]+");
    static string[] Words(string s) { return NotWord.Split((s ?? "").ToLowerInvariant()).Where(w => w.Length > 0).ToArray(); }

    static bool InARow(string[] part, string[] want)
    {
        for (int i = 0; i + want.Length <= part.Length; i++)
        {
            int j = 0;
            while (j < want.Length && part[i + j] == want[j]) j++;
            if (j == want.Length) return true;
        }
        return false;
    }

    // The words of a link, part by part: the site, the path, and each value after the ? - never a
    // parameter's name, so a word cannot run from one part into the next. A value that is itself a
    // link is read the same way.
    static List<string[]> LinkWords(string url)
    {
        var parts = new List<string[]>();
        int s = url.IndexOf("://", StringComparison.Ordinal);
        if (s <= 0) return parts;
        string rest = url.Substring(s + 3);
        int hash = rest.IndexOf('#');
        if (hash >= 0) rest = rest.Substring(0, hash);
        int end = rest.IndexOfAny(new[] { '/', '?' });
        if (end < 0) end = rest.Length;
        string host = rest.Substring(0, end);
        host = host.Substring(host.LastIndexOf('@') + 1);
        if (host.IndexOf(':') >= 0) host = host.Substring(0, host.IndexOf(':'));
        string after = rest.Substring(end);
        int q = after.IndexOf('?');
        parts.Add(Words(host));
        parts.Add(Words(Unescape(q >= 0 ? after.Substring(0, q) : after)));
        if (q >= 0)
            foreach (string pair in after.Substring(q + 1).Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq < 0) continue;   // a name alone, no value
                string value = Unescape(pair.Substring(eq + 1));
                if (IsLink(value)) parts.AddRange(LinkWords(value)); else parts.Add(Words(value));
            }
        return parts;
    }

    static bool IsLink(string s)
    {
        int at = s.IndexOf("://", StringComparison.Ordinal);
        return at > 0 && s.Substring(0, at).All(c => char.IsLetterOrDigit(c) || "+.-".IndexOf(c) >= 0);
    }

    static string Unescape(string s)
    {
        if (s.IndexOf('%') < 0) return s;
        try { return Uri.UnescapeDataString(s); } catch { return s; }
    }

    // What someone typed for an address, as a rule matches it: "https://github.com/" -> "github.com".
    public static string CleanAddress(string text)
    {
        string t = (text ?? "").Trim();
        foreach (string scheme in new[] { "https://", "http://" })
            if (t.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) t = t.Substring(scheme.Length);
        return t.TrimEnd('/');
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PROCESSENTRY32
    {
        public uint dwSize, cntUsage, th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID, cntThreads, th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
    }
    [DllImport("kernel32.dll")] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")] static extern bool Process32First(IntPtr snap, ref PROCESSENTRY32 e);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")] static extern bool Process32Next(IntPtr snap, ref PROCESSENTRY32 e);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] static extern bool GetProcessTimes(IntPtr h, out long created, out long exited, out long kernel, out long user);

    // Programs some apps start only to open a link for them (NetBird: "rundll32 url.dll,FileProtocolHandler").
    // The app behind one is the one that started it - when it is still running to be asked, and no
    // rule that is on names the go-between itself (a rule made for "rundll32.exe" keeps working).
    static readonly string[] GoBetweens = { "rundll32.exe", "cmd.exe" };

    static bool GoBetween(string exe)
    {
        return GoBetweens.Any(g => Same(g, exe)) && !Config.Rules.Any(r => r.On && r.ByApp && FromApp(r, exe));
    }

    // Whether process a started before process b. A program that has closed can leave its number to a
    // new one, which started later - that one is not the app behind the go-between. No when it cannot be told.
    static bool StartedBefore(uint a, uint b)
    {
        long ta = Started(a), tb = Started(b);
        return ta != 0 && tb != 0 && ta <= tb;
    }

    static long Started(uint pid)
    {
        IntPtr h = OpenProcess(0x1000, false, pid);   // only to ask about it
        if (h == IntPtr.Zero) return 0;
        try { long created, exited, kernel, user; return GetProcessTimes(h, out created, out exited, out kernel, out user) ? created : 0; }
        finally { CloseHandle(h); }
    }

    // The program that asked Windows to open the link - this process's parent - as its file name,
    // "Signal.exe". Null if it cannot be told.
    public static string SourceApp()
    {
        IntPtr snap = CreateToolhelp32Snapshot(2, 0);   // every process
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return null;
        try
        {
            var all = new Dictionary<uint, PROCESSENTRY32>();
            var e = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32)) };
            if (Process32First(snap, ref e))
                do { all[e.th32ProcessID] = e; } while (Process32Next(snap, ref e));
            PROCESSENTRY32 me, parent, above;
            uint self = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            if (!all.TryGetValue(self, out me) || !all.TryGetValue(me.th32ParentProcessID, out parent)) return null;
            for (int i = 0; i < 3 && GoBetween(parent.szExeFile) && all.TryGetValue(parent.th32ParentProcessID, out above) &&
                            above.th32ProcessID != parent.th32ProcessID && StartedBefore(above.th32ProcessID, parent.th32ProcessID); i++)
                parent = above;
            return parent.szExeFile;
        }
        catch { }
        finally { CloseHandle(snap); }
        return null;
    }

    static string RecentFile { get { return Path.Combine(Config.Dir, "recent-apps.txt"); } }

    // Which programs have opened links, newest first - offered in the app list as "Opened links
    // lately", so an app the built-in list does not know can still be added with one click.
    public static void Remember(string source)
    {
        if (string.IsNullOrEmpty(source) || Same(source, "BrowserSwitch.exe")) return;
        try
        {
            var list = Recent();
            list.RemoveAll(x => Same(x, source));
            list.Insert(0, source);
            File.WriteAllLines(RecentFile, list.Take(40).ToArray());
        }
        catch { }
    }

    public static List<string> Recent()
    {
        try { return File.ReadAllLines(RecentFile).Select(x => x.Trim()).Where(x => x.Length > 0).ToList(); }
        catch { return new List<string>(); }
    }
}

// Where an app's program file is, from its file name alone ("Signal.exe") - for its icon on the Rules
// tab. Asked in turn: Windows' list of installed programs (App Paths), the programs running now, and
// the Start menu's shortcuts (read once). Null if none knows it; the rule then shows no icon.
static class AppIcons
{
    static Dictionary<string, string> shortcuts;   // program file name -> where it is
    static readonly object gate = new object();

    public static string PathOf(string exe)
    {
        if (exe.Length == 0) return null;
        return FromAppPaths(exe) ?? FromRunning(exe) ?? FromShortcuts(exe);
    }

    static string FromAppPaths(string exe)
    {
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
            try
            {
                using (var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe))
                {
                    if (key == null) continue;
                    string path = ((key.GetValue("") as string) ?? "").Trim().Trim('"');
                    if (File.Exists(path)) return path;
                }
            }
            catch { }
        return null;
    }

    static string FromRunning(string exe)
    {
        foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
            try { using (p) { string path = p.MainModule.FileName; if (File.Exists(path)) return path; } }
            catch { }
        return null;
    }

    static string FromShortcuts(string exe)
    {
        string found;
        return Shortcuts().TryGetValue(exe, out found) ? found : null;
    }

    // The programs the Start menu has shortcuts to, as (shortcut name, program file name) - for "On this
    // PC" in the app list, so an app the built-in list does not know need not be looked for on disk.
    // Uninstallers are left out.
    public static List<KeyValuePair<string, string>> StartMenu()
    {
        Shortcuts();
        return startMenu;
    }

    static List<KeyValuePair<string, string>> startMenu;

    static Dictionary<string, string> Shortcuts()
    {
        lock (gate)
        {
            if (shortcuts == null)
            {
                shortcuts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                startMenu = new List<KeyValuePair<string, string>>();
                try
                {
                    object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                    foreach (var folder in new[] { Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.CommonStartMenu })
                    {
                        string[] links;
                        try { links = Directory.GetFiles(Environment.GetFolderPath(folder), "*.lnk", SearchOption.AllDirectories); }
                        catch { continue; }
                        foreach (string link in links)
                            try
                            {
                                object sc = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { link });
                                string target = (string)sc.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null);
                                if (string.IsNullOrEmpty(target) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                                    shortcuts.ContainsKey(Path.GetFileName(target)) || !File.Exists(target)) continue;
                                shortcuts[Path.GetFileName(target)] = target;
                                string name = Path.GetFileNameWithoutExtension(link);
                                if (name.IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) < 0 &&
                                    Path.GetFileName(target).IndexOf("unins", StringComparison.OrdinalIgnoreCase) < 0)
                                    startMenu.Add(new KeyValuePair<string, string>(name, Path.GetFileName(target)));
                            }
                            catch { }
                    }
                }
                catch { }
            }
            return shortcuts;
        }
    }
}

// Popular apps and the program files they run as, so rules can be set up before an app has ever
// opened a link. File names are those of each app's usual Windows install; if one does not match,
// "Opened links lately" in the app list shows what really opened the link.
static class AppCatalog
{
    public class App { public string Name, Group, Exes; }

    public static readonly string[] Groups = {
        "Chat & social", "Work & office", "Email", "AI assistants", "Notes & study", "Gaming",
        "Development", "Music & video", "Creative", "Files & sync", "Utilities" };

    static App A(int group, string name, string exes) { return new App { Group = Groups[group], Name = name, Exes = exes }; }

    public static readonly List<App> All = new List<App> {
        // chat & social
        A(0, "Signal", "Signal.exe"), A(0, "Discord", "Discord.exe;DiscordPTB.exe;DiscordCanary.exe"),
        A(0, "Telegram", "Telegram.exe"), A(0, "WhatsApp", "WhatsApp.exe;WhatsApp.Root.exe"),
        A(0, "Slack", "slack.exe"), A(0, "Microsoft Teams", "ms-teams.exe;Teams.exe"), A(0, "Skype", "Skype.exe"),
        A(0, "Messenger", "Messenger.exe"), A(0, "Zoom", "Zoom.exe"), A(0, "Element", "Element.exe"),
        A(0, "Viber", "Viber.exe"), A(0, "Wire", "Wire.exe"), A(0, "Session", "Session.exe"), A(0, "Beeper", "Beeper.exe"),
        A(0, "Mattermost", "Mattermost.exe"), A(0, "Rocket.Chat", "Rocket.Chat.exe"), A(0, "Guilded", "Guilded.exe"),
        A(0, "TeamSpeak", "TeamSpeak.exe;ts3client_win64.exe"), A(0, "Mumble", "mumble.exe"), A(0, "LINE", "LINE.exe"),
        A(0, "WeChat", "WeChat.exe"), A(0, "KakaoTalk", "KakaoTalk.exe"), A(0, "Pidgin", "pidgin.exe"),
        A(0, "Ferdium", "Ferdium.exe"), A(0, "Franz", "Franz.exe"), A(0, "Rambox", "Rambox.exe"),
        // work & office
        A(1, "Microsoft Word", "WINWORD.EXE"), A(1, "Microsoft Excel", "EXCEL.EXE"), A(1, "Microsoft PowerPoint", "POWERPNT.EXE"),
        A(1, "Microsoft OneNote", "ONENOTE.EXE"), A(1, "Microsoft Access", "MSACCESS.EXE"), A(1, "Microsoft Visio", "VISIO.EXE"),
        A(1, "Microsoft Project", "WINPROJ.EXE"), A(1, "Microsoft Publisher", "MSPUB.EXE"),
        A(1, "LibreOffice", "soffice.bin;soffice.exe"), A(1, "OnlyOffice", "DesktopEditors.exe"), A(1, "WPS Office", "wps.exe;et.exe;wpp.exe"),
        A(1, "Adobe Acrobat", "Acrobat.exe;AcroRd32.exe"), A(1, "Foxit PDF Reader", "FoxitPDFReader.exe"),
        A(1, "SumatraPDF", "SumatraPDF.exe"), A(1, "Notion", "Notion.exe"), A(1, "Trello", "Trello.exe"),
        A(1, "Asana", "Asana.exe"), A(1, "ClickUp", "ClickUp.exe"), A(1, "Linear", "Linear.exe"), A(1, "Miro", "Miro.exe"),
        A(1, "Todoist", "Todoist.exe"), A(1, "TickTick", "TickTick.exe"), A(1, "Microsoft To Do", "Todo.exe"),
        A(1, "TeamViewer", "TeamViewer.exe"), A(1, "AnyDesk", "AnyDesk.exe"), A(1, "Remote Desktop", "mstsc.exe"),
        // email
        A(2, "Outlook", "OUTLOOK.EXE"), A(2, "Outlook (new)", "olk.exe"), A(2, "Thunderbird", "thunderbird.exe"),
        A(2, "Windows Mail", "HxOutlook.exe"), A(2, "Mailspring", "Mailspring.exe"), A(2, "eM Client", "MailClient.exe"),
        A(2, "Mailbird", "Mailbird.exe"), A(2, "Proton Mail", "Proton Mail.exe"), A(2, "Meru", "Meru.exe"), A(2, "Postbox", "postbox.exe"),
        // AI assistants
        A(3, "Claude", "claude.exe"), A(3, "ChatGPT", "ChatGPT.exe"), A(3, "Perplexity", "Perplexity.exe"),
        A(3, "LM Studio", "LM Studio.exe"), A(3, "Ollama", "ollama app.exe"), A(3, "Jan", "Jan.exe"),
        // notes & study
        A(4, "Obsidian", "Obsidian.exe"), A(4, "Evernote", "Evernote.exe"), A(4, "Logseq", "Logseq.exe"),
        A(4, "Joplin", "Joplin.exe"), A(4, "Anki", "anki.exe"), A(4, "Zotero", "zotero.exe"), A(4, "Calibre", "calibre.exe;ebook-viewer.exe"),
        A(4, "Kindle", "Kindle.exe"), A(4, "Standard Notes", "Standard Notes.exe"), A(4, "Simplenote", "Simplenote.exe"),
        A(4, "Typora", "Typora.exe"), A(4, "Mendeley", "Mendeley Reference Manager.exe"),
        // gaming
        A(5, "Steam", "steam.exe;steamwebhelper.exe"), A(5, "Epic Games Launcher", "EpicGamesLauncher.exe"),
        A(5, "GOG Galaxy", "GalaxyClient.exe"), A(5, "Battle.net", "Battle.net.exe"), A(5, "EA app", "EADesktop.exe"),
        A(5, "Ubisoft Connect", "UbisoftConnect.exe;upc.exe"), A(5, "Riot Client", "RiotClientServices.exe;RiotClientUx.exe"),
        A(5, "League of Legends", "LeagueClientUx.exe"), A(5, "Xbox app", "XboxPcApp.exe"), A(5, "Minecraft Launcher", "MinecraftLauncher.exe"),
        A(5, "Prism Launcher", "prismlauncher.exe"), A(5, "CurseForge", "CurseForge.exe"), A(5, "Overwolf", "Overwolf.exe"),
        A(5, "osu!", "osu!.exe"), A(5, "Wargaming Game Center", "wgc.exe"), A(5, "Playnite", "Playnite.DesktopApp.exe;Playnite.FullscreenApp.exe"),
        A(5, "Heroic Games Launcher", "Heroic.exe"), A(5, "itch", "itch.exe"), A(5, "Amazon Games", "Amazon Games UI.exe"),
        A(5, "Medal", "Medal.exe"), A(5, "Parsec", "parsecd.exe"), A(5, "Rockstar Games Launcher", "RockstarLauncher.exe"),
        // development
        A(6, "Visual Studio Code", "Code.exe"), A(6, "Visual Studio", "devenv.exe"), A(6, "Cursor", "Cursor.exe"),
        A(6, "Windsurf", "Windsurf.exe"), A(6, "Zed", "zed.exe"), A(6, "Sublime Text", "sublime_text.exe"),
        A(6, "Notepad++", "notepad++.exe"), A(6, "JetBrains IDEs",
            "idea64.exe;pycharm64.exe;rider64.exe;webstorm64.exe;clion64.exe;goland64.exe;phpstorm64.exe;datagrip64.exe;rustrover64.exe;rubymine64.exe"),
        A(6, "Android Studio", "studio64.exe"), A(6, "GitHub Desktop", "GitHubDesktop.exe"), A(6, "GitKraken", "gitkraken.exe"),
        A(6, "Fork", "Fork.exe"), A(6, "Sourcetree", "SourceTree.exe"), A(6, "Postman", "Postman.exe"), A(6, "Insomnia", "Insomnia.exe"),
        A(6, "Bruno", "Bruno.exe"), A(6, "Docker Desktop", "Docker Desktop.exe"), A(6, "Windows Terminal", "WindowsTerminal.exe"),
        A(6, "Unity Hub", "Unity Hub.exe"), A(6, "Unity", "Unity.exe"), A(6, "DBeaver", "dbeaver.exe"),
        // music & video
        A(7, "Spotify", "Spotify.exe"), A(7, "VLC", "vlc.exe"), A(7, "Apple Music", "AppleMusic.exe"), A(7, "iTunes", "iTunes.exe"),
        A(7, "TIDAL", "TIDAL.exe"), A(7, "Deezer", "Deezer.exe"), A(7, "foobar2000", "foobar2000.exe"), A(7, "MusicBee", "MusicBee.exe"),
        A(7, "Plex", "Plex.exe"), A(7, "Jellyfin Media Player", "JellyfinMediaPlayer.exe"), A(7, "Stremio", "stremio.exe"),
        A(7, "MPC-HC", "mpc-hc64.exe;mpc-hc.exe"), A(7, "PotPlayer", "PotPlayerMini64.exe"), A(7, "OBS Studio", "obs64.exe"),
        A(7, "Streamlabs", "Streamlabs OBS.exe"), A(7, "Podcasts / Apple TV", "AppleTV.exe"),
        // creative
        A(8, "Figma", "Figma.exe"), A(8, "Photoshop", "Photoshop.exe"), A(8, "Illustrator", "Illustrator.exe"),
        A(8, "Premiere Pro", "Adobe Premiere Pro.exe"), A(8, "After Effects", "AfterFX.exe"), A(8, "Lightroom", "Lightroom.exe"),
        A(8, "Blender", "blender.exe"), A(8, "GIMP", "gimp-2.10.exe;gimp-3.0.exe;gimp.exe"), A(8, "Krita", "krita.exe"),
        A(8, "Inkscape", "inkscape.exe"), A(8, "DaVinci Resolve", "Resolve.exe"), A(8, "Canva", "Canva.exe"),
        A(8, "Paint.NET", "paintdotnet.exe"), A(8, "Audacity", "Audacity.exe"), A(8, "Adobe Creative Cloud", "Creative Cloud.exe"),
        // files & sync
        A(9, "File Explorer", "explorer.exe"), A(9, "OneDrive", "OneDrive.exe"), A(9, "Dropbox", "Dropbox.exe"),
        A(9, "Google Drive", "GoogleDriveFS.exe"), A(9, "Nextcloud", "nextcloud.exe"), A(9, "Synology Drive", "cloud-drive-ui.exe"),
        A(9, "qBittorrent", "qbittorrent.exe"), A(9, "7-Zip", "7zFM.exe"), A(9, "WinRAR", "WinRAR.exe"), A(9, "Total Commander", "TOTALCMD64.EXE"),
        // utilities
        A(10, "KeePassXC", "KeePassXC.exe"), A(10, "KeePass", "KeePass.exe"), A(10, "Bitwarden", "Bitwarden.exe"),
        A(10, "1Password", "1Password.exe"), A(10, "PowerToys Run", "PowerToys.PowerLauncher.exe"), A(10, "Flow Launcher", "Flow.Launcher.exe"),
        A(10, "Everything", "Everything.exe"), A(10, "ShareX", "ShareX.exe"), A(10, "Greenshot", "Greenshot.exe"),
        A(10, "Notepad", "Notepad.exe"), A(10, "Windows Settings", "SystemSettings.exe"), A(10, "Proton VPN", "ProtonVPN.exe"),
        A(10, "NetBird", "netbird-ui.exe;netbird.exe"),
    };

    public static App ForExe(string exe)
    {
        return All.FirstOrDefault(a => a.Exes.Split(';').Any(x => string.Equals(x, exe, StringComparison.OrdinalIgnoreCase)));
    }
}

// The Rules tab of the main window: tick a rule on or off, move it up or down (the first match
// decides), edit where it sends links, add apps, addresses or keywords. Every change is saved at once.
class RulesPage : UserControl
{
    readonly Action save;
    readonly ListView list = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, HideSelection = false,
                                            Dock = DockStyle.Fill, HeaderStyle = ColumnHeaderStyle.Nonclickable, MultiSelect = false };
    readonly Panel empty;   // over the list while there are no rules: what they are for, and a way to add one
    bool filling, doubleClick;
    // The app's icon in front of an app rule, the browser's (as that profile shows it) in front of
    // where it goes. App icons are looked for away from the screen (AppIcons) and appear when found.
    readonly ImageList icons = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
    readonly HashSet<string> looked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // apps already looked for
    // App icons found so far, kept for as long as LinkPilot runs: a tab built again shows them at
    // once instead of looking for every app again. Used on the window's own thread only.
    static readonly Dictionary<string, Bitmap> appPictures = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
    readonly SynchronizationContext ui;

    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref LVITEM lParam);
    [StructLayout(LayoutKind.Sequential)] struct LVITEM
    {
        public uint mask; public int iItem, iSubItem; public uint state, stateMask; public IntPtr pszText; public int cchTextMax, iImage;
        public IntPtr lParam; public int iIndent, iGroupId; public uint cColumns; public IntPtr puColumns, piColFmt; public int iGroup;
    }

    static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

    // showCategories: opens the Categories tab - for a list that is empty because there is only one
    public RulesPage(Action save, Action showCategories = null)
    {
        this.save = save;
        ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        Font = new Font("Segoe UI", 9F);
        Dock = DockStyle.Fill;
        Padding = Ui.PagePadding;

        var use = new CheckBox { Text = "Use rules", Checked = Config.RulesOn, AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        use.CheckedChanged += delegate { Config.RulesOn = use.Checked; save(); };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(0, 8, 0, 0), WrapContents = false };
        top.Controls.Add(use);
        var help = new TabHelp("The Rules tab",
            "# Rules",
            "Which wins: a matching rule beats the live category; the first match decides (Move up / down).",
            "No match: the link goes to the live category.",
            "Off: untick one rule, or Use rules for all - also in the dock menu and on a shortcut.",
            "Edit: where a rule sends links, or its address - or double-click the rule.",
            "Same app or address twice: you choose - replace the older rule, or keep it.",
            "# App rules",
            "Known by: the program file. Store apps may hide behind Windows - use an address rule for those.",
            "Opened links lately: in Add apps…, what really opened your links.",
            "# Address rules",
            "github.com: also covers gist.github.com. With a / it is looked for anywhere in the link.",
            "# Keyword rules",
            "invoice: a whole word in the site, the path or a value after the ? - not invoices, nor a parameter's name.");

        // an app rule fills the first column, an address rule the second - one list, so the order
        // (the first match decides) stays across both kinds
        list.Columns.Add("When a link comes from", 190);
        list.Columns.Add("or its address has", 170);
        list.Columns.Add("Goes to profile", -2);           // -2: fills the rest of the width
        list.ItemChecked += (s, e) => { if (filling) return; ((Rule)e.Item.Tag).On = e.Item.Checked; save(); };
        list.ItemActivate += delegate { Edit(); };         // double-click a rule to edit it
        // ...without the double-click also ticking or unticking it, as Windows would
        list.MouseDown += (s, e) => doubleClick = e.Clicks > 1;
        list.MouseUp += delegate { doubleClick = false; };
        list.ItemCheck += (s, e) => { if (doubleClick) e.NewValue = e.CurrentValue; };
        if (Config.Categories.Count < 2)
        {
            Control go = null;
            if (showCategories != null) { go = Ui.Primary("Go to Categories", true); go.Click += delegate { showCategories(); }; }
            empty = Ui.Empty("No rules yet", "Rules need two categories - make another first.", go);
        }
        else
        {
            var apps = Ui.Primary("Add apps…", true);
            apps.Click += delegate { AddApps(); };
            var address = new LinkLabel { Text = "or add an address", AutoSize = true, Margin = new Padding(10, 7, 3, 3) };
            address.LinkClicked += delegate { AddAddress(); };
            empty = Ui.Empty("No rules yet", "Send links from one app or site to its own category - say, Teams to Work.", apps, address);
        }
        list.Controls.Add(empty);
        // under the column titles, over the rest
        list.Resize += delegate { empty.SetBounds(0, 26, list.ClientSize.Width, Math.Max(0, list.ClientSize.Height - 26)); list.Columns[2].Width = -2; };
        list.SmallImageList = icons;
        // just after: while its window is being made, the list's rows are not there yet
        list.HandleCreated += delegate { list.BeginInvoke((MethodInvoker)ProfileImages); };

        // adding first; then changing one; then its place in the order
        var side = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 146, FlowDirection = FlowDirection.TopDown, Padding = new Padding(8, 0, 0, 0) };
        side.Controls.Add(SideButton("Add apps…", delegate { AddApps(); }));
        side.Controls.Add(SideButton("Add address…", delegate { AddAddress(); }));
        side.Controls.Add(SideButton("Add keyword…", delegate { AddWord(); }));
        var edit = SideButton("Edit…", delegate { Edit(); });
        edit.Margin = new Padding(3, 15, 3, 3);
        side.Controls.Add(edit);
        side.Controls.Add(SideButton("Remove", delegate { Remove(); }));
        var up = SideButton("Move up", delegate { MoveRule(-1); });
        up.Margin = new Padding(3, 15, 3, 3);
        side.Controls.Add(up);
        side.Controls.Add(SideButton("Move down", delegate { MoveRule(1); }));

        Controls.Add(list);
        Controls.Add(side);
        Controls.Add(top);
        Controls.Add(Ui.PageHeader("Rules", "Send links from some apps, sites or words to their own category.", help));
        Fill();
    }

    static Button SideButton(string text, EventHandler click)
    {
        var b = Ui.Button(text, click);
        b.AutoSize = false;
        b.MinimumSize = Size.Empty;
        b.Size = new Size(132, Ui.ButtonHeight);
        return b;
    }

    Rule Selected() { return list.SelectedItems.Count > 0 ? (Rule)list.SelectedItems[0].Tag : null; }

    void Fill(int select = -1)
    {
        if (select < 0) select = list.SelectedIndices.Count > 0 ? list.SelectedIndices[0] : -1;
        filling = true;
        list.BeginUpdate();
        list.Items.Clear();
        foreach (var r in Config.Rules)
        {
            var item = new ListViewItem(r.ByApp ? r.Label : "") { Checked = r.On, Tag = r };
            item.SubItems.Add(r.ByApp ? "" : r.ByWord ? "the word \"" + r.Label + "\"" : r.Label);
            bool known = Config.Categories.Any(c => Same(c.Name, r.Category));
            item.SubItems.Add(known ? r.Category : r.Category + "  (no such category - skipped)");
            if (r.ByApp)
            {
                Bitmap found;
                if (!icons.Images.ContainsKey("app:" + r.Match) && appPictures.TryGetValue(r.Match, out found))
                    icons.Images.Add("app:" + r.Match, found);
                if (icons.Images.ContainsKey("app:" + r.Match)) item.ImageKey = "app:" + r.Match;
                else LookFor(r.Match);
            }
            list.Items.Add(item);
        }
        list.EndUpdate();
        ProfileImages();
        empty.Visible = Config.Rules.Count == 0;
        if (select >= 0 && select < list.Items.Count) { list.Items[select].Selected = true; list.Items[select].EnsureVisible(); }
        filling = false;
    }

    // An app's icon, looked for on another thread (finding the program can take a moment); it is put
    // on that app's rules when found. match: the rule's program files, ";" between.
    void LookFor(string match)
    {
        if (!looked.Add(match)) return;
        ThreadPool.QueueUserWorkItem(delegate
        {
            Bitmap picture = null;
            foreach (string exe in match.Split(';'))
            {
                string path = AppIcons.PathOf(exe.Trim());
                if (path == null) continue;
                try { using (var icon = Icon.ExtractAssociatedIcon(path)) picture = icon.ToBitmap(); break; } catch { }
            }
            if (picture != null) ui.Post(delegate
            {
                appPictures[match] = picture;
                if (IsDisposed) return;
                if (!icons.Images.ContainsKey("app:" + match)) icons.Images.Add("app:" + match, picture);
                // only the rules for this app - filling the whole list again for every icon found
                // kept the window busy while the tab opened
                foreach (ListViewItem item in list.Items)
                {
                    var r = (Rule)item.Tag;
                    if (r.ByApp && Same(r.Match, match)) item.ImageKey = "app:" + match;
                }
            }, null);
        });
    }

    // The browser profile's icon in the "Goes to profile" column. A ListView shows pictures in later
    // columns only when asked to (LVS_EX_SUBITEMIMAGES), and each one is set on its own.
    void ProfileImages()
    {
        if (!list.IsHandleCreated || list.IsDisposed) return;
        SendMessage(list.Handle, 0x1036, (IntPtr)0x2, (IntPtr)0x2);   // LVM_SETEXTENDEDLISTVIEWSTYLE, LVS_EX_SUBITEMIMAGES
        for (int i = 0; i < list.Items.Count; i++)
        {
            var r = list.Items[i] == null ? null : list.Items[i].Tag as Rule;
            if (r == null) continue;
            var none = new LVITEM { mask = 0x2, iItem = i, iSubItem = 1, iImage = -1 };   // LVIF_IMAGE; none for the address
            SendMessage(list.Handle, 0x104C, IntPtr.Zero, ref none);                    // LVM_SETITEMW
            var to = new LVITEM { mask = 0x2, iItem = i, iSubItem = 2, iImage = ProfileImage(r.Category) };
            SendMessage(list.Handle, 0x104C, IntPtr.Zero, ref to);
        }
    }

    int ProfileImage(string category)
    {
        string key = "to:" + category;
        if (!icons.Images.ContainsKey(key))
        {
            var c = Config.Categories.FirstOrDefault(x => Same(x.Name, category));
            if (c == null) return -1;
            try { using (var icon = Tray.BrowserIcon(c)) icons.Images.Add(key, icon.ToBitmap()); } catch { return -1; }
        }
        return icons.Images.IndexOfKey(key);
    }

    void Remove()
    {
        var r = Selected(); if (r == null) return;
        int at = Config.Rules.IndexOf(r);
        Config.Rules.Remove(r);
        save();
        Fill(Math.Min(at, Config.Rules.Count - 1));
    }

    void MoveRule(int by)
    {
        var r = Selected(); if (r == null) return;
        int at = Config.Rules.IndexOf(r), to = at + by;
        if (to < 0 || to >= Config.Rules.Count) return;
        Config.Rules.RemoveAt(at);
        Config.Rules.Insert(to, r);
        save();
        Fill(to);
    }

    void AddApps()
    {
        using (var picker = new AppPicker())
        {
            if (picker.ShowDialog(FindForm()) != DialogResult.OK) return;
            int last = -1;
            foreach (var a in picker.Chosen)
            {
                var r = new Rule { ByApp = true, Label = a.Name, Match = a.Exes, Category = picker.Target };
                if (Place(r, null)) last = Config.Rules.IndexOf(r);
            }
            save();
            Fill(last);
        }
    }

    void AddAddress()
    {
        var r = new Rule { ByApp = false };
        if (!RuleDialog(r, "Add an address", "Add") || !Place(r, null)) return;
        save();
        Fill(Config.Rules.IndexOf(r));
    }

    void AddWord()
    {
        var r = new Rule { ByWord = true };
        if (!RuleDialog(r, "Add a keyword", "Add") || !Place(r, null)) return;
        save();
        Fill(Config.Rules.IndexOf(r));
    }

    void Edit()
    {
        var r = Selected(); if (r == null) return;
        var changed = new Rule { On = r.On, ByApp = r.ByApp, ByWord = r.ByWord, Label = r.Label, Match = r.Match, Category = r.Category };
        if (!RuleDialog(changed, "Edit rule", "Save") || !Place(changed, r)) return;
        save();
        Fill(Config.Rules.IndexOf(changed));
    }

    // Puts a new rule into the list - or an edited one in place of the rule it was (was). If another
    // rule is already for the same app or address, asks which one stays: the new one then takes the
    // older one's place in the order. False: the older one stays, and nothing changes.
    bool Place(Rule r, Rule was) { return Place(r, was, older => ReplaceOlder(older, r)); }

    // The same, for any window: replace says whether the new rule takes the older one's place. The
    // Ask every time window's "Remember for" replaces without asking - ticking it is the answer.
    internal static bool Place(Rule r, Rule was, Func<Rule, bool> replace)
    {
        var older = Config.Rules.FirstOrDefault(x => x != was && x.ByApp == r.ByApp && x.ByWord == r.ByWord &&
            (r.ByApp ? x.Match.Split(';').Intersect(r.Match.Split(';'), StringComparer.OrdinalIgnoreCase).Any() : Same(x.Match, r.Match)));
        if (older != null && !replace(older)) return false;
        int at = was != null ? Config.Rules.IndexOf(was) : older != null ? Config.Rules.IndexOf(older) : Config.Rules.Count;
        if (was != null) Config.Rules.Remove(was);
        if (older != null) { if (Config.Rules.IndexOf(older) < at) at--; Config.Rules.Remove(older); }
        Config.Rules.Insert(Math.Min(at, Config.Rules.Count), r);
        return true;
    }

    // "Signal already has a rule" - Replace the older rule, or Keep the older rule (also Esc).
    bool ReplaceOlder(Rule older, Rule r)
    {
        string what = older.ByApp ? older.Label : older.ByWord ? "The word " + older.Label : "The address " + older.Label;
        using (var d = new Form { Text = "Already has a rule", Size = new Size(470, 180), FormBorderStyle = FormBorderStyle.FixedDialog,
                                  StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false,
                                  ShowInTaskbar = false, Font = Font })
        {
            var say = new Label { Left = 14, Top = 16, Width = 430, Height = 60,
                                  Text = what + " already has a rule: its links go to " + older.Category + ".\n" +
                                         (Same(older.Category, r.Category) ? "The new one sends them there too."
                                                                           : "The new one would send them to " + r.Category + ".") };
            var replace = new Button { Text = "Replace the older rule", DialogResult = DialogResult.Yes, Left = 128, Top = 90, Width = 160 };
            var keep = new Button { Text = "Keep the older rule", DialogResult = DialogResult.No, Left = 296, Top = 90, Width = 150 };
            d.Controls.AddRange(new Control[] { say, replace, keep });
            d.AcceptButton = replace; d.CancelButton = keep;
            Ui.HandCursors(d);
            return d.ShowDialog(FindForm()) == DialogResult.Yes;
        }
    }

    ListView SitesList(TextBox box)
    {
        var seen = LinkLog.Read().Where(LinkLog.WentToBrowser).Select(e =>
        {
            Uri u;
            string host = Uri.TryCreate(e.Opened, UriKind.Absolute, out u) && (u.Scheme == "http" || u.Scheme == "https") ? u.Host.ToLowerInvariant() : "";
            if (host.StartsWith("www.")) host = host.Substring(4);
            return new { Host = host, e.When, Url = e.Opened };
        }).Where(x => x.Host.Length > 0).GroupBy(x => x.Host).Select(g => new
        {
            Host = g.Key, Count = g.Count(), Last = g.Max(x => x.When),
            Rule = Config.Rules.FirstOrDefault(x => !x.ByApp && x.On && g.Any(y => Router.HasAddress(x.Match, y.Url)))
        }).OrderByDescending(x => x.Count).ThenByDescending(x => x.Last).ToList();
        if (seen.Count == 0) return null;
        var list = new ListView { View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
        list.Columns.Add("Sites you opened", 160);
        list.Columns.Add("Links", 50);
        list.Columns.Add("Last", 80);
        list.Columns.Add("Rule", -2);
        foreach (var x in seen)
        {
            var item = new ListViewItem(x.Host) { Tag = x.Count };
            item.SubItems.Add(x.Count.ToString());
            item.SubItems.Add(x.Last.Date == DateTime.Today ? x.Last.ToString("HH:mm") : x.Last.ToString("d MMM"));
            item.SubItems[2].Tag = x.Last;
            item.SubItems.Add(x.Rule == null ? "" : "→ " + x.Rule.Category);
            if (x.Rule != null) item.ForeColor = SystemColors.GrayText;   // already has one - the others are the ones to set up
            list.Items.Add(item);
        }
        list.SelectedIndexChanged += delegate { if (list.SelectedItems.Count > 0) box.Text = list.SelectedItems[0].Text; };
        // a column's title sorts by it: the site by name, the others newest / most first
        list.ColumnClick += (s, e) =>
        {
            int col = e.Column;
            list.ListViewItemSorter = new Comparer(col);
        };
        return list;
    }

    class Comparer : System.Collections.IComparer
    {
        readonly int col;
        public Comparer(int col) { this.col = col; }
        public int Compare(object a, object b)
        {
            ListViewItem x = (ListViewItem)a, y = (ListViewItem)b;
            if (col == 1) return ((int)y.Tag).CompareTo((int)x.Tag);
            if (col == 2) return ((DateTime)y.SubItems[2].Tag).CompareTo((DateTime)x.SubItems[2].Tag);
            return string.Compare(x.SubItems[col].Text, y.SubItems[col].Text, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Asks for a rule's address (address rules) or keyword (keyword rules) and where its links go, and puts the answers in r.
    // An app rule shows its app; to have another app, remove the rule and add that app.
    bool RuleDialog(Rule r, string title, string okText)
    {
        using (var d = new Form { Text = title, Size = new Size(430, 210), FormBorderStyle = FormBorderStyle.FixedDialog,
                                  StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false,
                                  ShowInTaskbar = false, Font = Font })
        {
            var ask = new Label { Text = r.ByApp ? "Links that come from:" : r.ByWord ? "Links whose address has the word:" : "Links whose address has:",
                                  Left = 14, Top = 16, AutoSize = true };
            var box = new TextBox { Left = 14, Top = 40, Width = 390, Text = r.ByWord ? r.Label : r.Match };
            var app = new Label { Left = 14, Top = 42, AutoSize = true, Font = new Font(Font, FontStyle.Bold),
                                  Text = r.Label + "   (" + r.Match.Replace(";", ", ") + ")" };
            var to = new Label { Text = "go to profile:", Left = 14, Top = 84, AutoSize = true };
            var cat = new ComboBox { Left = 100, Top = 80, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cat.Items.AddRange(Config.Categories.Select(c => (object)c.Name).ToArray());
            // the rule's own profile - or, for a new rule, the live one
            cat.SelectedItem = cat.Items.Cast<object>().FirstOrDefault(o => Same((string)o, r.Category.Length > 0 ? r.Category : Config.Active));
            if (cat.SelectedItem == null && cat.Items.Count > 0) cat.SelectedIndex = 0;
            var ok = Ui.Primary(okText, true);
            ok.DialogResult = DialogResult.OK;
            ok.AutoSize = false;
            ok.SetBounds(238, 128, 80, Ui.ButtonHeight);
            var no = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 324, Top = 128, Width = 80, Height = Ui.ButtonHeight };
            d.Controls.AddRange(new Control[] { ask, r.ByApp ? (Control)app : box, to, cat, ok, no });
            if (r.ByWord) WordParts(d, box, ok, new Control[] { to, cat, ok, no });
            else if (!r.ByApp)
            {
                d.Controls.Add(new TabHelp("Links to an address",
                    "Sites under it: github.com also covers gist.github.com and www.github.com.",
                    "With a /: github.com/my-company is looked for anywhere in the link.",
                    "As you type: sites you opened and well-known ones - click one, or Down and Enter.",
                    "Sites you opened: from the link log - click one to use it. Sort by a column's title.") { Left = 164, Top = 14 });
                var sites = SitesList(box);
                if (sites != null)
                {
                    // the opened sites go between the address and where its links go
                    d.Height += 230;
                    sites.SetBounds(14, 72, 390, 220);
                    foreach (Control c in new Control[] { to, cat, ok, no }) c.Top += 230;
                    d.Controls.Add(sites);
                }
                else
                {
                    // no sites opened yet: room under the address for a few suggestions
                    d.Height += 5 * 22;
                    foreach (Control c in new Control[] { to, cat, ok, no }) c.Top += 5 * 22;
                }
            }
            if (!r.ByApp)
            {
                // suggestions as you type: the sites you opened, then well-known ones
                var mine = Sites.Mine();
                // they stop above where the links go; for a keyword only on Down, not to hide what it would match
                new Hints(box, d, typed => Sites.Find(typed, mine), h => { box.Text = h.Text; box.SelectionStart = box.Text.Length; })
                    { Above = cat, OnDemand = r.ByWord };
            }
            d.AcceptButton = ok; d.CancelButton = no;
            Ui.HandCursors(d);
            if (d.ShowDialog(FindForm()) != DialogResult.OK || cat.SelectedItem == null) return false;
            if (r.ByWord)
            {
                string word = Router.Keyword(box.Text);
                if (word == null) return false;
                r.Label = box.Text.Trim(); r.Match = word;
            }
            else if (!r.ByApp)
            {
                string address = Router.CleanAddress(box.Text);
                if (address.Length == 0) return false;
                r.Label = r.Match = address;
            }
            r.Category = (string)cat.SelectedItem;
            return true;
        }
    }

    // For a keyword rule: a short example, and which of the links you opened (the link log) it would
    // match, by site - updated as you type, so a word that catches too much shows before it is saved.
    // below: what goes under it, moved down to make room.
    void WordParts(Form d, TextBox box, Button ok, Control[] below)
    {
        d.Controls.Add(new TabHelp("Links with a keyword",
            "A whole word: invoice matches shop.com/invoice/12 and ?q=my+invoice - not invoices.",
            "Where: the site, the path, or a value after the ? - never a parameter's name, as in ?invoice=1.",
            "Several words: pull request must come one after the other - pull-request, pull/request.",
            "At least 3 letters or digits. Big or small letters do not matter.",
            "Would match: the links in the link log it catches, by site - check it does not catch too much.",
            "Down: sites you opened and well-known ones - pick one with Enter.") { Left = 214, Top = 14 });
        var example = new Label { Left = 14, Top = 68, Width = 390, Height = 32, ForeColor = SystemColors.GrayText,
                                  Text = "invoice: shop.com/invoice/12 and ?q=my+invoice - not invoices, nor ?invoice=1." };
        var count = new Label { Left = 14, Top = 100, Width = 390, Height = 18 };
        var sites = new ListView { View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
        sites.SetBounds(14, 120, 390, 170);
        sites.Columns.Add("Would match these sites you opened", 200);
        sites.Columns.Add("Links", 45);
        sites.Columns.Add("For example", -2);
        var opened = LinkLog.Read().Where(LinkLog.WentToBrowser).Select(e => e.Opened).ToList();
        EventHandler show = delegate
        {
            string word = Router.Keyword(box.Text);
            ok.Enabled = word != null;
            sites.BeginUpdate();
            sites.Items.Clear();
            if (word == null) count.Text = box.Text.Trim().Length == 0 ? "" : "At least 3 letters or digits.";
            else if (opened.Count == 0) count.Text = "No links in the link log to try it on.";
            else
            {
                var hits = opened.Where(u => Router.HasWord(word, u)).ToList();
                count.Text = "Would match " + hits.Count + " of the " + opened.Count + " links you opened.";
                foreach (var g in hits.GroupBy(u => SiteOf(u)).OrderByDescending(x => x.Count()))
                {
                    var item = new ListViewItem(g.Key);
                    item.SubItems.Add(g.Count().ToString());
                    item.SubItems.Add(g.First());
                    sites.Items.Add(item);
                }
            }
            sites.EndUpdate();
        };
        box.TextChanged += show;
        show(null, null);
        d.Height += 222;
        foreach (Control c in below) c.Top += 222;
        d.Controls.AddRange(new Control[] { example, count, sites });
    }

    // "github.com" for https://www.github.com/x - or "" if it is not a web address. Also the Ask
    // every time window's "Remember for".
    internal static string SiteOf(string url)
    {
        Uri u;
        string host = Uri.TryCreate(url, UriKind.Absolute, out u) ? u.Host.ToLowerInvariant() : "";
        return host.StartsWith("www.") ? host.Substring(4) : host;
    }
}

// The app list: search it, or browse by group; tick as many as you like; choose where their links go.
// Besides the built-in groups: "Opened links lately" and "Opened the most links" (what really opened
// links on this PC), "Running now" and "On this PC" (the Start menu) - so an app the built-in list does
// not know can be added without looking for its program file.
class AppPicker : Form
{
    const string AllGroup = "All apps", RecentGroup = "Opened links lately", MostGroup = "Opened the most links",
                 RunningGroup = "Running now", OnPcGroup = "On this PC";
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

    public readonly List<AppCatalog.App> Chosen = new List<AppCatalog.App>();
    public string Target;

    readonly TextBox search = new TextBox { Dock = DockStyle.Fill };
    readonly ListBox groups = new ListBox { Dock = DockStyle.Left, Width = 170, IntegralHeight = false };
    readonly ListView apps = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, Dock = DockStyle.Fill,
                                            HeaderStyle = ColumnHeaderStyle.Nonclickable, HideSelection = false };
    readonly ComboBox target = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    readonly List<AppCatalog.App> ticked = new List<AppCatalog.App>();   // kept while searching and browsing
    // one App per program file, so the same program in two groups is one row to tick
    readonly Dictionary<string, AppCatalog.App> byExe = new Dictionary<string, AppCatalog.App>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<AppCatalog.App, int> links = new Dictionary<AppCatalog.App, int>();   // links opened, from the link log
    readonly List<AppCatalog.App> recent, most;
    List<AppCatalog.App> running = new List<AppCatalog.App>(), onPc = new List<AppCatalog.App>();
    bool filling, looking = true;

    public AppPicker()
    {
        Text = "Add apps";
        Font = new Font("Segoe UI", 9F);
        Size = new Size(760, 520);
        MinimumSize = new Size(600, 380);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;

        recent = Router.Recent().Select(exe => AppFor(exe, null)).Distinct().ToList();
        foreach (var g in LinkLog.Read().Where(e => e.From.Length > 0 && LinkLog.WentToBrowser(e) &&
                                                    !string.Equals(e.From, "BrowserSwitch.exe", StringComparison.OrdinalIgnoreCase))
                                        .GroupBy(e => e.From, StringComparer.OrdinalIgnoreCase))
        {
            var a = AppFor(g.Key, null);
            int had; links.TryGetValue(a, out had);
            links[a] = had + g.Count();
        }
        most = links.OrderByDescending(x => x.Value).ThenBy(x => x.Key.Name).Take(25).Select(x => x.Key).ToList();

        var searchRow = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(8, 6, 8, 4) };
        searchRow.Controls.Add(search);
        var helpHolder = new Panel { Dock = DockStyle.Right, Width = 30 };   // keeps the (i) round, not stretched
        helpHolder.Controls.Add(new TabHelp("Adding apps",
            "Finding them: search, or pick a group on the left. The search also looks through the programs on this PC.",
            "As you type: the apps you use first - click one, or Down and Enter, to tick it.",
            "Several at once: tick as many as you like - the ticks stay while you search and switch groups - then " +
                "choose where their links go and press Add.",
            "Opened links lately / the most: the programs that really opened links on this PC.",
            "Running now: open the app first, then find it here.",
            "On this PC: every program in the Start menu.",
            "Not there either: use Browse for a program….") { Left = 8, Top = 1 });
        searchRow.Controls.Add(helpHolder);
        search.HandleCreated += delegate { SendMessage(search.Handle, 0x1501, (IntPtr)1, "Search apps…"); };   // grey hint text
        search.TextChanged += delegate { Fill(); };
        new Hints(search, this, Suggest, Pick);

        groups.Items.Add(AllGroup);
        groups.Items.Add(RecentGroup + (recent.Count > 0 ? "  (" + recent.Count + ")" : ""));
        groups.Items.Add(MostGroup + (most.Count > 0 ? "  (" + most.Count + ")" : ""));
        groups.Items.Add(RunningGroup);
        groups.Items.Add(OnPcGroup);
        groups.Items.AddRange(AppCatalog.Groups.Select(g => (object)g).ToArray());
        groups.SelectedIndex = recent.Count > 0 ? 1 : 0;
        groups.SelectedIndexChanged += delegate { Fill(); };

        apps.Columns.Add("App", 220);
        apps.Columns.Add("Program file", 250);
        apps.Columns.Add("Links", 60);
        apps.ItemChecked += (s, e) =>
        {
            if (filling) return;
            var a = e.Item.Tag as AppCatalog.App;
            if (a == null) return;
            if (e.Item.Checked) { if (!ticked.Contains(a)) ticked.Add(a); } else ticked.Remove(a);
        };
        apps.ItemActivate += delegate { if (apps.FocusedItem != null && apps.FocusedItem.Tag != null) apps.FocusedItem.Checked = !apps.FocusedItem.Checked; };

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8, 7, 8, 4) };
        var browse = new Button { Text = "Browse for a program…", AutoSize = true };
        browse.Click += delegate { Browse(); };
        bottom.Controls.Add(browse);
        bottom.Controls.Add(new Label { Text = "Send their links to:", AutoSize = true, Margin = new Padding(24, 7, 3, 3) });
        target.Items.AddRange(Config.Categories.Select(c => (object)c.Name).ToArray());
        target.SelectedItem = target.Items.Cast<object>().FirstOrDefault(o => string.Equals((string)o, Config.Active, StringComparison.OrdinalIgnoreCase));
        if (target.SelectedItem == null && target.Items.Count > 0) target.SelectedIndex = 0;   // the live one, else the first
        bottom.Controls.Add(target);
        var add = Ui.Primary("Add", true);
        add.Margin = new Padding(16, 3, 3, 3);
        add.Click += delegate
        {
            if (ticked.Count == 0) { MessageBox.Show(this, "Tick at least one app first."); return; }
            if (target.SelectedItem == null) { MessageBox.Show(this, "Make a category first - links need somewhere to go."); return; }
            Chosen.AddRange(ticked);
            Target = (string)target.SelectedItem;
            DialogResult = DialogResult.OK;
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Height = Ui.ButtonHeight };
        bottom.Controls.Add(add);
        bottom.Controls.Add(cancel);
        CancelButton = cancel;

        Controls.Add(apps);
        Controls.Add(groups);
        Controls.Add(searchRow);
        Controls.Add(bottom);
        Ui.HandCursors(this);
        Fill();
        Look();
    }

    // The App for a program file: the built-in one if the list knows it, else one made once (named after
    // the Start-menu shortcut or the program's own description, else its file name).
    AppCatalog.App AppFor(string exe, string name)
    {
        AppCatalog.App a;
        if (byExe.TryGetValue(exe, out a)) return a;
        a = AppCatalog.ForExe(exe) ??
            new AppCatalog.App { Name = string.IsNullOrEmpty(name) ? Path.GetFileNameWithoutExtension(exe) : name, Group = "", Exes = exe };
        byExe[exe] = a;
        return a;
    }

    // "Running now" and "On this PC" take a moment (every running program, every Start-menu shortcut),
    // so they are looked for away from the screen and appear when found.
    void Look()
    {
        var ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        ThreadPool.QueueUserWorkItem(delegate
        {
            var run = new List<KeyValuePair<string, string>>();   // program file, its description
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            int session = Process.GetCurrentProcess().SessionId, self = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcesses())
                try
                {
                    using (p)
                    {
                        if (p.SessionId != session || p.Id == self) continue;
                        string path = p.MainModule.FileName;
                        if (path.StartsWith(windows, StringComparison.OrdinalIgnoreCase)) continue;   // Windows' own
                        string about = (p.MainModule.FileVersionInfo.FileDescription ?? "").Trim();
                        run.Add(new KeyValuePair<string, string>(Path.GetFileName(path), about));
                    }
                }
                catch { }   // a program run as administrator cannot be asked
            var pc = AppIcons.StartMenu();
            ui.Post(delegate
            {
                if (IsDisposed) return;
                running = run.Select(x => AppFor(x.Key, x.Value)).Distinct().OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
                onPc = pc.Select(x => AppFor(x.Value, x.Key)).Distinct().OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
                looking = false;
                Fill();
            }, null);
        });
    }

    // Apps for what was typed, in order: those whose name starts with it, then those with a word that
    // does (anywhere: then those with it anywhere in the name or program file). Within each, the ones
    // you use first - links opened, opened links lately, running now, on this PC - then the built-in
    // list's order.
    List<AppCatalog.App> Ranked(string q, bool anywhere)
    {
        var run = new HashSet<AppCatalog.App>(running);
        var pc = new HashSet<AppCatalog.App>(onPc);
        return AppCatalog.All.Concat(recent).Concat(most).Concat(running).Concat(onPc).Distinct()
            .Select(a => new { App = a, Tier = Tier(a, q, anywhere) }).Where(x => x.Tier >= 0)
            .OrderBy(x => x.Tier).ThenByDescending(x => Links(x.App)).ThenBy(x => Place(recent, x.App))
            .ThenBy(x => run.Contains(x.App) ? 0 : 1).ThenBy(x => pc.Contains(x.App) ? 0 : 1)
            .ThenBy(x => Place(AppCatalog.All, x.App)).ThenBy(x => x.App.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.App).ToList();
    }

    static int Tier(AppCatalog.App a, string q, bool anywhere)
    {
        int t = Sites.Tier(q, a.Name, false);
        if (t >= 0 || !anywhere) return t;
        return a.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || a.Exes.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ? 2 : -1;
    }

    int Links(AppCatalog.App a) { int n; return links.TryGetValue(a, out n) ? n : 0; }
    static int Place(List<AppCatalog.App> list, AppCatalog.App a) { int i = list.IndexOf(a); return i < 0 ? int.MaxValue : i; }

    // The suggestions under the search: the apps for what was typed - or, before anything is typed,
    // the ones you use.
    List<Hints.Item> Suggest(string typed)
    {
        string q = typed.Trim();
        var found = q.Length > 0 ? Ranked(q, false)
                                : recent.Union(most).OrderByDescending(Links).ThenBy(a => Place(recent, a)).ToList();
        var run = new HashSet<AppCatalog.App>(running);
        var pc = new HashSet<AppCatalog.App>(onPc);
        return found.Take(Hints.Most).Select(a => new Hints.Item
        {
            Text = a.Name, Tag = a,
            Note = Links(a) > 0 ? Sites.Links(Links(a)) : recent.Contains(a) ? "opened links lately" : run.Contains(a) ? "running now"
                 : pc.Contains(a) ? "on this PC" : a.Group
        }).ToList();
    }

    // A suggestion picked: it is ticked and shown in the list, its name left in the search, chosen, so
    // typing on starts a new search.
    void Pick(Hints.Item h)
    {
        var a = (AppCatalog.App)h.Tag;
        if (!ticked.Contains(a)) ticked.Add(a);
        if (search.Text == a.Name) Fill(); else search.Text = a.Name;   // fills the list again, ticked
        search.SelectAll();
        foreach (ListViewItem item in apps.Items)
            if (item.Tag == a) { item.Selected = item.Focused = true; item.EnsureVisible(); break; }
    }

    // What the list shows: while searching, every app whose name or program file has the text (the
    // best matches first); otherwise the chosen group.
    void Fill()
    {
        string q = search.Text.Trim();
        string g = groups.SelectedItem == null ? AllGroup : ((string)groups.SelectedItem).Split(new[] { "  (" }, StringSplitOptions.None)[0];
        IEnumerable<AppCatalog.App> shown =
            q.Length > 0 ? Ranked(q, true)
            : g == RecentGroup ? recent
            : g == MostGroup ? most
            : g == RunningGroup ? running
            : g == OnPcGroup ? onPc
            : g == AllGroup ? AppCatalog.All.OrderBy(a => a.Name)
            : AppCatalog.All.Where(a => a.Group == g);
        filling = true;
        apps.BeginUpdate();
        apps.Items.Clear();
        foreach (var a in shown)
        {
            var item = new ListViewItem(a.Name) { Tag = a, Checked = ticked.Contains(a) };
            item.SubItems.Add(a.Exes.Replace(";", ", "));
            int n;
            item.SubItems.Add(links.TryGetValue(a, out n) ? n.ToString() : "");
            apps.Items.Add(item);
        }
        if (looking && (q.Length > 0 || g == RunningGroup || g == OnPcGroup))
            apps.Items.Add(new ListViewItem("Looking for the programs on this PC…") { ForeColor = SystemColors.GrayText });
        apps.EndUpdate();
        filling = false;
    }

    void Browse()
    {
        using (var d = new OpenFileDialog { Title = "The program whose links should have a rule", Filter = "Programs|*.exe" })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return;
            var a = AppFor(Path.GetFileName(d.FileName), null);
            if (!recent.Contains(a)) recent.Insert(0, a);
            if (!ticked.Contains(a)) ticked.Add(a);
            search.Text = "";
            groups.SelectedIndex = 1;
            Fill();
        }
    }
}
