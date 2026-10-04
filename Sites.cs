// Suggestions as you type a site or an app: the sites you opened (the link log) and well-known
// ones, the apps you use and the ones on this PC. What you typed must be where a name or one of
// its words starts - "re" finds reddit.com, not youtube.com; "code" finds Visual Studio Code.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

static class Sites
{
    // Widely used sites, the most used first: they are suggested in this order after your own.
    // The Android app has the same list in the same order (android/.../Sites.kt) - change both together.
    public static readonly string[] Popular = {
        // everyday
        "google.com", "youtube.com", "facebook.com", "wikipedia.org", "instagram.com", "x.com", "reddit.com",
        "gmail.com", "linkedin.com", "tiktok.com", "messenger.com", "web.whatsapp.com", "web.telegram.org",
        "discord.com", "pinterest.com", "threads.com", "bsky.app", "twitch.tv", "bing.com", "duckduckgo.com",
        "yahoo.com", "maps.google.com", "translate.google.com", "deepl.com", "quora.com", "medium.com",
        "substack.com", "9gag.com", "imdb.com", "microsoft.com", "apple.com", "icloud.com",
        // AI
        "chatgpt.com", "claude.ai", "gemini.google.com", "perplexity.ai", "copilot.microsoft.com", "grok.com",
        "chat.deepseek.com", "huggingface.co",
        // Polish
        "allegro.pl", "olx.pl", "ceneo.pl", "wp.pl", "onet.pl", "interia.pl", "o2.pl", "gazeta.pl",
        "pudelek.pl", "tvn24.pl", "tvp.info", "polsatnews.pl", "rmf24.pl", "money.pl", "bankier.pl", "sport.pl",
        "meczyki.pl", "wykop.pl", "filmweb.pl", "cda.pl", "player.pl", "vod.tvp.pl", "otomoto.pl", "otodom.pl",
        "pracuj.pl", "justjoin.it", "nofluffjobs.com", "pyszne.pl", "glovoapp.com", "allegrolokalnie.pl",
        "vinted.pl", "empik.com", "mediaexpert.pl", "euro.com.pl", "rtveuroagd.pl", "x-kom.pl", "morele.net",
        "zalando.pl", "decathlon.pl", "inpost.pl", "poczta-polska.pl", "intercity.pl", "jakdojade.pl",
        "mbank.pl", "pkobp.pl", "ipko.pl", "ing.pl", "santander.pl", "pekao.com.pl", "bankmillennium.pl",
        "aliorbank.pl", "bnpparibas.pl", "gov.pl", "epuap.gov.pl", "podatki.gov.pl", "pacjent.gov.pl", "zus.pl",
        "usosweb.uw.edu.pl", "synergia.librus.pl", "portal.librus.pl",
        // work & development
        "github.com", "gitlab.com", "bitbucket.org", "stackoverflow.com", "mail.google.com", "docs.google.com",
        "drive.google.com", "calendar.google.com", "meet.google.com", "outlook.live.com", "outlook.office.com",
        "office.com", "onedrive.live.com", "sharepoint.com", "teams.microsoft.com", "slack.com", "zoom.us",
        "notion.so", "figma.com", "atlassian.net", "trello.com", "asana.com", "monday.com", "clickup.com",
        "linear.app", "miro.com", "canva.com", "airtable.com", "calendly.com", "dropbox.com",
        "developer.mozilla.org", "learn.microsoft.com", "npmjs.com", "pypi.org", "hub.docker.com",
        "aws.amazon.com", "portal.azure.com", "console.cloud.google.com", "cloudflare.com", "vercel.com",
        "netlify.com", "dev.to",
        // music & video
        "netflix.com", "spotify.com", "open.spotify.com", "music.youtube.com", "primevideo.com",
        "disneyplus.com", "hbomax.com", "soundcloud.com",
        // shopping & money
        "amazon.com", "amazon.pl", "amazon.de", "aliexpress.com", "temu.com", "ebay.com", "shein.com",
        "ikea.com", "paypal.com", "revolut.com", "wise.com",
        // travel, news, learning, games
        "booking.com", "airbnb.com", "ryanair.com", "wizzair.com", "lot.com", "accuweather.com", "bbc.com",
        "cnn.com", "theguardian.com", "nytimes.com", "duolingo.com", "coursera.org", "udemy.com",
        "khanacademy.org", "chess.com", "lichess.org", "store.steampowered.com", "epicgames.com", "roblox.com"
    };

    // The sites whose links went to a browser, from the link log - with how many links each, the most first.
    public static List<KeyValuePair<string, int>> Mine()
    {
        return LinkLog.Read().Where(LinkLog.WentToBrowser).Select(e =>
        {
            Uri u;
            string host = Uri.TryCreate(e.Opened, UriKind.Absolute, out u) && (u.Scheme == "http" || u.Scheme == "https") ? u.Host.ToLowerInvariant() : "";
            if (host.StartsWith("www.")) host = host.Substring(4);
            return host.StartsWith("m.") ? host.Substring(2) : host;   // m.youtube.com is youtube.com
        }).Where(h => h.Contains('.')).GroupBy(h => h).OrderByDescending(g => g.Count())
          .Select(g => new KeyValuePair<string, int>(g.Key, g.Count())).ToList();
    }

    // The sites to suggest for what was typed: yours first, then the popular ones. Nothing typed: your
    // most used. A path or a space typed: none - that is an address of your own. Never just what was typed.
    public static List<Hints.Item> Find(string typed, List<KeyValuePair<string, int>> mine)
    {
        string q = Router.CleanAddress(typed).ToLowerInvariant();
        if (q.StartsWith("www.")) q = q.Substring(4);
        if (q.Length == 0) return mine.Take(Hints.Most).Select(x => Item(x.Key, x.Value)).ToList();
        if (q.IndexOfAny(new[] { '/', ' ', '?' }) >= 0) return new List<Hints.Item>();
        var count = mine.ToDictionary(x => x.Key, x => x.Value);
        string same = typed.Trim().ToLowerInvariant();
        return mine.Select(x => x.Key).Concat(Popular.Where(p => !count.ContainsKey(p))).Where(h => h != same)
                   .Select(h => new { Host = h, Tier = Tier(q, h, true), N = count.ContainsKey(h) ? count[h] : 0, At = Array.IndexOf(Popular, h) })
                   .Where(x => x.Tier >= 0)
                   .OrderBy(x => x.Tier).ThenByDescending(x => x.N).ThenBy(x => x.At < 0 ? int.MaxValue : x.At)
                   .Take(Hints.Most).Select(x => Item(x.Host, x.N)).ToList();
    }

    static Hints.Item Item(string host, int links) { return new Hints.Item { Text = host, Note = links > 0 ? Links(links) : "popular", Tag = host }; }

    public static string Links(int n) { return n == 1 ? "1 link" : n + " links"; }

    // 0: the name starts with what was typed; 1: one of its words does; -1: neither. Big or small
    // letters do not matter. A site's words are its parts between the dots, all but the last and the
    // plain ones (docs.google.com: docs, google; pekao.com.pl: pekao) - so "co" or "pl" does not find every site.
    // The Android app matches the same way (android/.../Sites.kt).
    static readonly string[] Plain = { "com", "co", "org", "net", "edu", "gov", "ac" };

    public static int Tier(string q, string name, bool site)
    {
        if (q.Length == 0) return -1;
        if (name.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 0;
        int stop = site ? name.LastIndexOf('.') : name.Length;
        for (int i = 1; i < stop; i++)
            if ((site ? name[i - 1] == '.' && !Plain.Contains(name.Substring(i, name.IndexOf('.', i) - i))
                      : !char.IsLetterOrDigit(name[i - 1])) && char.IsLetterOrDigit(name[i]) &&
                string.Compare(name, i, q, 0, q.Length, StringComparison.OrdinalIgnoreCase) == 0) return 1;
        return -1;
    }
}

// A list of suggestions under a text box. It is laid over the window rather than opened as a window
// of its own, so the box keeps the keyboard: type on, Down/Up to move through it, Enter or a click
// to pick, Esc to close it - the window stays open. It opens as you type (unless OnDemand), on Down,
// or on a click into the empty box. It ends above Above, if given, so it never covers the buttons.
class Hints
{
    public class Item { public string Text, Note; public object Tag; }
    public const int Most = 8;
    public Control Above;    // the list stops above this control
    public bool OnDemand;    // opens only on Down or a click, not as you type

    readonly TextBox box;
    readonly Control host;
    readonly Func<string, List<Item>> find;
    readonly Action<Item> pick;
    readonly ListBox list;
    bool quiet;   // the text is being set by a pick, not typed
    bool keyed;   // a row was chosen with Down/Up - only then does Enter pick it; the mouse only points

    public Hints(TextBox box, Control host, Func<string, List<Item>> find, Action<Item> pick)
    {
        this.box = box; this.host = host; this.find = find; this.pick = pick;
        list = new ListBox { DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 22, IntegralHeight = false, Visible = false,
                             TabStop = false, BorderStyle = BorderStyle.FixedSingle, Font = box.Font };
        host.Controls.Add(list);
        list.DrawItem += Draw;
        list.MouseMove += (s, e) => { int i = list.IndexFromPoint(e.Location); if (i >= 0 && i != list.SelectedIndex) list.SelectedIndex = i; };
        list.MouseClick += (s, e) => { int i = list.IndexFromPoint(e.Location); if (i >= 0) Pick((Item)list.Items[i]); };
        list.Leave += delegate { Later(); };
        box.TextChanged += delegate { if (!quiet && box.Focused) { if (!OnDemand) Open(); else if (list.Visible) Open(); } };
        box.MouseDown += delegate { if (!list.Visible && box.Text.Trim().Length == 0) Open(); };
        box.Leave += delegate { Later(); };
        host.MouseDown += delegate { Close(); };   // a click on the window's empty part
        // Enter and Esc would otherwise press the window's OK or Cancel before the box sees them
        box.PreviewKeyDown += (s, e) =>
        {
            if (list.Visible && (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter && keyed && list.SelectedIndex >= 0)) e.IsInputKey = true;
        };
        box.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Down)
            {
                if (!list.Visible) Open();
                else if (list.SelectedIndex < list.Items.Count - 1) { list.SelectedIndex++; keyed = true; }
            }
            else if (e.KeyCode == Keys.Up) { if (list.Visible && list.SelectedIndex >= 0) { list.SelectedIndex--; keyed = true; } }
            else if (e.KeyCode == Keys.Enter && list.Visible && keyed && list.SelectedIndex >= 0) Pick((Item)list.SelectedItem);
            else if (e.KeyCode == Keys.Escape && list.Visible) Close();
            else return;
            e.Handled = e.SuppressKeyPress = true;
        };
    }

    public void Open()
    {
        var at = host.PointToClient(box.PointToScreen(new Point(0, box.Height)));
        int end = Above == null ? host.ClientSize.Height : host.PointToClient(Above.PointToScreen(Point.Empty)).Y;
        int fits = Math.Max(1, (end - at.Y - 6) / list.ItemHeight);   // as many as fit, no scrolling
        var items = find(box.Text).Take(fits).ToList();
        if (items.Count == 0) { Close(); return; }
        list.BeginUpdate();
        list.Items.Clear();
        list.Items.AddRange(items.Cast<object>().ToArray());
        list.EndUpdate();
        list.SelectedIndex = -1;
        keyed = false;
        list.SetBounds(at.X, at.Y + 1, box.Width, items.Count * list.ItemHeight + 2);
        list.Visible = true;
        list.BringToFront();
    }

    public void Close() { list.Visible = false; }

    // closes once the keyboard has gone somewhere other than the box or the list
    void Later()
    {
        if (!host.IsHandleCreated) return;
        host.BeginInvoke((Action)delegate { if (!box.Focused && !list.Focused) Close(); });
    }

    void Pick(Item item)
    {
        quiet = true;
        Close();
        try { pick(item); } finally { quiet = false; }
        box.Focus();
    }

    void Draw(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var item = (Item)list.Items[e.Index];
        bool on = (e.State & DrawItemState.Selected) != 0;
        using (var back = new SolidBrush(on ? Ui.Picked : SystemColors.Window)) e.Graphics.FillRectangle(back, e.Bounds);
        var r = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
        int noteWidth = TextRenderer.MeasureText(item.Note ?? "", list.Font).Width;
        TextRenderer.DrawText(e.Graphics, item.Note ?? "", list.Font, r, SystemColors.GrayText,
                              TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        r.Width -= noteWidth + 8;
        TextRenderer.DrawText(e.Graphics, item.Text, list.Font, r, SystemColors.WindowText,
                              TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}
