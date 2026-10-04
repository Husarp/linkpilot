# LinkPilot

Decides which browser — and which **profile** of it — every link on this computer opens in, and
takes the tracking out of links on the way. You make named categories like Work, Home or School,
point each at a browser profile, and switch between them with one click.

*LinkPilot was called **Browser Switch** until version 4.0.0.* Installing it with the one command
moves a copy in `Programs\Browser Switch` to `Programs\LinkPilot`, settings and all, and replaces
its shortcuts. A few inside names keep the old one — the program file `BrowserSwitch.exe`, the
registry entries `BrowserSwitchURL` and `BrowserSwitch` — because they are what Windows remembers
as your default browser; renaming them would make you choose it again.

There is also **LinkPilot for Android** — the same idea for a phone, with Quick Settings tiles
and home-screen widgets to switch. It is in the [`android`](android) folder; see its
[README](android/README.md).

## Install

Needs Windows 10 or 11. Three ways — all give you the same program.

### With the installer (easiest)

On the [Releases page](https://github.com/Husarp/linkpilot/releases), download
**`LinkPilotSetup-<version>.exe`** and run it. It installs LinkPilot for your account only (no
administrator rights) and opens it; LinkPilot then walks you through the one step Windows leaves to
you: choosing it as your default browser. The installer is not signed, so Windows may say *"Windows
protected your PC"* — *More info → Run anyway*.

- **Update:** LinkPilot checks GitHub for a newer version by itself (on to begin with; *About &
  updates* tab → untick *Check for updates automatically* to stop it) and offers **Update** at the top
  of its window. That downloads the new installer, closes LinkPilot, puts the new version in place and
  starts it again. Your settings are kept. More under [Updates](#updates). Running a newer
  `LinkPilotSetup-<version>.exe` yourself does the same: it says *Update*, and asks before closing a
  running LinkPilot.
- **Uninstall:** Settings → Apps → Installed apps → LinkPilot → Uninstall. If LinkPilot is your default
  browser, it first opens Settings on the page of the browser you used before, to make that the
  default again with one click. Your settings and link log are kept unless you tick *Also delete my
  settings and link log* (they go to the Recycle Bin).

### With one command

Open **PowerShell** (press the Windows key, type `powershell`, press Enter), paste this and press
Enter:

```powershell
irm https://raw.githubusercontent.com/Husarp/linkpilot/main/get.ps1 | iex
```

That's all. It downloads the **source code** of the latest release, builds the program on your PC
with the C# compiler that is part of Windows — so no ready-made `.exe` is downloaded, and what runs
is exactly the code you can read here — installs it for your account only (no administrator rights),
and opens it. [Read the script first](get.ps1) if you like — it is short.

- **Update:** run the same command again, or press *Update* in the app. Your settings are kept.
- **Uninstall:** as with the installer.

### From the files

1. On the [Releases page](https://github.com/Husarp/linkpilot/releases), download
   **`LinkPilot-<version>.zip`** — the ready-made program with everything needed to install and
   remove it. (The release also has the installer, `BrowserSwitch.exe` on its own, the Android app,
   and the source code.)
2. Unzip it into a folder you will keep — Windows runs the program from there.
3. Double-click **`Install.cmd`**. LinkPilot opens and walks you through the rest.

The program is not signed, so Windows may say *"Windows protected your PC"* the first time — *More
info → Run anyway*. More detail under [Setting it up](#setting-it-up).

### What a release has

| File | What it is |
|---|---|
| `LinkPilotSetup-<version>.exe` | the installer — install, update and uninstall in one window. The app's own updater downloads this one |
| `LinkPilot-<version>.zip` | the ready-made program with `Install.cmd`, `install.ps1`, `uninstall.ps1`, `Back to normal.cmd`, this README and the licence |
| `BrowserSwitch.exe` | the program on its own |
| `LinkPilot-Android-<version>.apk` | the Android app (see [Install on Android](#install-on-android)) |
| Source code (zip, tar.gz) | added by GitHub; what the one command builds from |

### What is the difference?

All three end with the same `BrowserSwitch.exe` on your PC. The difference is **where it is made**
and where it goes:

| | Installer | One command | From the files |
|---|---|---|---|
| The program | built by the author, downloaded ready-made | built **on your PC** from the source code, by the compiler that is part of Windows | built by the author, downloaded ready-made |
| What you trust | that the download matches the code | the code, which you can read here | that the download matches the code |
| "Windows protected your PC" | may appear once, as for any unsigned program | no — nothing ready-made was downloaded | may appear once |
| Where it goes | `%LOCALAPPDATA%\Programs\LinkPilot`, for your account | the same | any folder you choose |
| Updating | *Update* in the app | the same command again, or *Update* in the app | a new zip, or *Update* in the app |

## Install on Android

Needs Android 8.0 or newer. The app is not in Google Play; it comes from the same release as the
Windows program.

1. On your phone, open the [Releases page](https://github.com/Husarp/linkpilot/releases) and
   download **`LinkPilot-Android-<version>.apk`** (next to `BrowserSwitch.exe`).
2. Open the downloaded file. The first time, Android asks to allow installing apps from the app you
   opened it in (your browser or Files) — allow it, go back and tap **Install**. Google Play Protect
   may warn about an app from an unknown developer — *More details → Install anyway*.
3. Open **LinkPilot**. It walks you through the rest, like on Windows: your choices, then making it
   the default browser (Android's own question — no app can do this without you).

- **Update:** the app checks GitHub by itself when it opens or you come back to it (at most every 5
  minutes; switch it off, or tap **CHECK NOW**, in Home › Updates). When there is a newer version, tap
  **UPDATE**: it downloads inside the app, Android asks once to let LinkPilot install apps, then asks
  you to confirm. Your settings are kept. More under [Updates](android/README.md#updates).
- **Uninstall:** as any app — long-press LinkPilot › App info › Uninstall. Android then asks which
  browser should be the default again.

What it does, and what differs from Windows: [LinkPilot for Android](android/README.md).

## What it does

- **Categories** — Work, Home, School… each pointing at a browser and profile; one click switches.
- **The dock** — lives next to the clock, with an icon per category if you like, and a short note
  after every switch.
- **Keyboard shortcuts** — one per category, next / previous, and rules on / off. With the **smart
  queue**, Next goes back to the category used before — one press flips back, like Alt+Tab.
- **Link rules** — links from chosen apps (Signal → Work), to chosen sites (github.com → Home) or with
  a chosen word in them (invoice → Work) go to their own category, whatever is live. A ready-made list
  of ~175 popular apps to pick from, plus the programs on your PC.
- **Ask every time** — a category that opens no browser of its own: each link it gets shows a small
  window next to the mouse asking which category to open it in.
- **Link cleaning** — 314 tracking parts (`utm_source`, `fbclid`, YouTube's `si`, the shops' own…)
  taken out of links, and 34 redirects (`google.com/url?q=…`, Outlook Safe Links, affiliate links)
  skipped, so links go straight to the page. If you like, a link you **copy** is cleaned too, so you
  paste the clean one.
- **Link log** — every link: which app it came from, where it opened, and what was changed.
- **A short guide** after the setup — your categories, how you switch, rules for your apps — each
  screen with a good answer filled in already. It can be taken again any time.
- **Web page files** (`.htm`, `.html`) show the browser they will open in.
- **Offline and private** — it never sends your links anywhere. It only goes online to check for a
  newer version (you can switch that off) and to download it when you press *Update*.

## Privacy and security

**It works offline.** LinkPilot never sends your links, categories, rules or the link log
anywhere, and has no account, ads, tracking or analytics. The one time it connects to the internet
is to ask GitHub whether a newer version exists — when it starts and when you open its window, at
most every 5 minutes (switch that off in the setup or on the *About & updates* tab), or when you press
*Check now* — and, when you press *Update*, to download the new version's installer. GitHub then sees
only what any visit to a website shows, such as your internet address. Nothing about your links is sent.

**What it keeps, and where** — all of it in plain text, in its own folder, on your PC only:

| File | What it holds |
|---|---|
| `config.txt` | your categories, rules, shortcuts and settings |
| `link-log.txt` | the link log: the newest 1000 links you opened from other programs (and copied links it cleaned, if that is on) — and so a record of sites you visited. Switch it off, or clear it, on the *Link log* tab |
| `recent-apps.txt` | the programs that opened links lately, for app rules |
| `file-icons\` | the icon web page files show |

**What you copy** is looked at only if you turn on *Clean copied links too* — just to see whether it
is one link on its own — and never kept; what a password manager copies is not looked at at all.

**What it touches in Windows:** only your own account's part of the registry (`HKEY_CURRENT_USER`)
— the registration that makes it a browser Windows can offer, and a Start menu, Desktop and Startup
shortcut. It needs no administrator rights, changes nothing for other accounts, and installs no
service or driver. Uninstalling removes all of it.

**What it cannot do:** make itself your default browser. Windows allows only you to choose that —
no program and no command can — which is also what protects you from programs taking over your
browser. LinkPilot shows you where to click.

**What you can check:** every line of code is here. The one-command install builds the program on
your PC from this code, so nothing ready-made is downloaded. The ready-made `.exe` in the releases is
built from the same code, but it is not signed, so Windows may warn about it the first time.

**Made for personal use** and shared freely under the MIT licence — see [LICENSE](LICENSE).

## Why it is built this way

Windows will not let a script change your default browser. The setting carries a signature tied to
your account, and anything written directly is thrown away. That protection stops programs hijacking
your browser, and it is worth keeping.

So this does not fight it. **LinkPilot becomes the default browser** and passes each link
straight on to the real one, based on which category is live.

Nothing was downloaded to build it. `BrowserSwitch.exe` is compiled from the `.cs` files and
`BrowserSwitch.ico` by the C# compiler already inside Windows (`build.cmd` rebuilds it).

## The dock

LinkPilot lives in the **notification area** next to the clock — "the dock" — and starts there
when you sign in. While its window is open it also has a taskbar button (untick *Show a taskbar
button* on *About & updates* to keep it in the dock only). Closing the window does not stop it — the
first time, a note says so and where to find it.

- **Click its icon** (blue square, two arrows) — the window opens, at once: the dock builds it
  unseen a few seconds after it starts, and closing the window only hides it (it is brought up to
  date each time it opens). The icon stays.
- **Right-click it** — your categories, with a tick on the live one: click one to switch. Also
  *Open LinkPilot* and *Exit*.
- **Pin a category** — in the window, select it and tick **Show in dock**. It gets its own icon; one
  click on that icon switches to it, with no window. **Dock icon…** chooses what it looks like: the
  browser's own icon — for Brave, Chrome and Edge the one **with the profile's picture on it**, the
  same the taskbar shows, which the browser keeps in the profile's folder; that icon **recoloured** — a small editor with *Colour*, *Strength* and
  *Brightness* sliders and a live preview, for two profiles of the same browser (a green Brave for
  Work, an orange one for Home); a colour with the category's first letter; or an image file.
- **Hover over a pinned icon** and it says which category it is: **Work**, or **Switch to Work** —
  choose which in the right-click menu, *Hover text on dock icons*.
- **Every switch shows a short note** at the bottom of the screen for about two seconds —
  "Links now open in: Work", and under it the browser and profile with the category's dock icon
  in front (recoloured, if you recoloured it). It never takes the focus and clicks go straight
  through it. It appears whichever way you switched: an icon, a shortcut, the window, or
  `Back to normal.cmd`.

Windows puts new icons in the hidden-icons area first — the **^** arrow next to the clock. To keep
one always visible, drag it from there onto the taskbar, or switch it on in Settings →
Personalization → Taskbar → *Other system tray icons*.

## Web page files

Saved web pages — `.htm` and `.html` files — show **the browser they will open in**: a white page
with that browser's icon on its corner, the same icon as in the dock (with the profile's picture,
or recoloured, if yours is). It follows every switch, and the rules too, so the icon
always tells the truth about where a double-click goes.

Right-click the dock icon → *Icon of .htm / .html files* to choose the look: **A web page with the
browser's icon**, **The browser's icon** alone, or **LinkPilot's own icon**. Windows usually
redraws at once; if an open folder still shows the old icon, press F5 in it.

## Keyboard shortcuts

**Off until you turn them on:** in the window, the **Shortcuts** tab, then tick **Turn on keyboard
shortcuts**. They then work anywhere, as long as the dock is running.

- **One per category**, and **Next** / **Previous**, which step through every category that has a
  browser, in order, round and round. Every switch shows the usual note — and if the category was
  already live, the note says so, so a key press never goes unanswered.
- **Suggested keys are filled in already.** Ctrl+Alt+1, 2, 3… where free, otherwise the same with
  Shift, otherwise Ctrl+Alt + a letter of the category's name; for next/previous Ctrl+Alt+Space /
  Shift+Space, otherwise PageDown/PageUp — each checked with Windows first, so only free keys are
  suggested. Other programs often hold every Ctrl+Alt+number already; then Work gets
  **Ctrl+Alt+W**, Home **Ctrl+Alt+H**, and so on.
- **To change one:** click its box and press **any key** — alone or with Ctrl, Alt, Shift or Win.
  Special keys work too: media keys (play/pause, volume, next track), **F13–F24**, mail/calculator/
  browser launch keys, Print Screen, Pause. **Clear** empties a line. While you record, Enter, Esc,
  Tab and Backspace are recorded like any other key. While the tab is open the dock lets go of its
  keys, so pressing one records it instead of switching.
- **Active** — the tick in front of each name. Untick it to switch that one shortcut off: its keys
  are kept but do nothing, and other programs can use them again. The big *Turn on keyboard
  shortcuts* tick still switches all of them at once.
- **Reset** appears next to a shortcut once it differs from the one suggested, and puts that back.
- **In next / previous** — a tick per category. Next and Previous only stop at the ticked ones
  (all of them to begin with); a category's own shortcut works either way. If the live category is
  not ticked, they still move on from where it sits in the list.
- **Smart queue** (on to begin with) — Next and Previous go by recent use, like Alt+Tab: Next goes
  back to the category used before, so one press flips back, and Previous to the one used longest
  ago. Presses close together (within about three seconds) walk on through the rest instead of
  flipping between the same two. Untick it and they go in list order.
- **Not every key can be used.** Logitech's **Easy-Switch keys** (1, 2, 3 — they choose which paired
  device the keyboard talks to) are handled inside the keyboard itself and never reach the PC; that
  is also why Logi Options+ cannot reassign them. The top-row keys and the ones at the top right
  usually can be given F13–F24 in Logi Options+.
- **A keyboard button that shows nothing** when pressed is handled by the keyboard's own software
  (for Logitech: Logi Options+). Give it a keystroke there — F13 to F24 are ideal, since no keyboard
  has them and nothing else uses them — then record that.
- **Each line says whether it will work:** *ready*, *taken by another program*, *used twice here*, or
  *types something* — a plain **K** types k, so while it is a shortcut you could not type k anywhere.
  That is allowed; it is your choice. On Polish and many other keyboards **Ctrl+Alt is AltGr** —
  Ctrl+Alt+A types ą — so those are never *suggested*, and are flagged the same way if you pick one.

## Link rules

Some links should always go to the same place, whatever is live: links from Signal to Work, GitHub
links to Home. In the window, the **Rules** tab.

- **Three kinds.** *comes from an app* — the program the link was clicked in; *address has* — a site
  like `github.com`, which also covers the sites under it (`gist.github.com`), or any text with a `/`
  in it (`github.com/my-company`), looked for anywhere in the link; *address has the word* — a
  keyword (**Add keyword…**), see below.
- **Keyword rules.** `invoice` matches a whole word in the site, the path or a value after the `?` —
  `shop.com/invoice/12`, `?q=my+invoice` — but never part of a word (`invoices`) nor a parameter's
  name (`?invoice=1`). Several words must come one after another: `pull request` matches
  `…/pull-request`. At least 3 letters or digits; big or small letters do not matter. While you type,
  the dialog shows which of the sites you opened (from the link log) it would match, so a word that
  catches too much shows before it is saved.
- **Suggestions as you type.** Typing an address shows up to 8 sites: the ones you opened (from the
  link log, the most used first), then about 180 well-known ones (global and Polish — the same list
  as on Android). Only sites that start with what you typed, or one of whose parts does: `re` finds
  reddit.com, never youtube.com; `goo` also finds docs.google.com. Before you type: your own sites.
  Click one, or **Down** and **Enter**; **Esc** closes the list. In a keyword rule they show on
  **Down**, so the list of what it would match stays in view. *Add apps…* does the same for apps: the
  ones you use first (links opened, opened links lately, running now), then the rest — `code` finds
  Visual Studio Code; picking one ticks it.
- **Sites you opened.** *Add address…* lists the sites in your link log — how many links, when the
  last one was, and which already have a rule (greyed). Click one to use it; click a column's title to
  sort by it.
- **Rules win over the live category.** They are checked from the top, and the first that matches
  decides — **Move up / Move down** set the order. A link no rule matches goes to the live category
  as always. A link sent by a rule shows no note.
- **A tick per rule** keeps it but stops it. A rule whose category has been deleted is skipped.
- **Edit…** (or double-click a rule) changes the profile its links go to — and, for an address
  rule, the address.
- **One rule per app, address or keyword.** Adding (or editing into) one that already has a rule asks
  which stays: *Replace the older rule* — the new one takes its place in the order — or *Keep the
  older rule*.
- **The list** shows an app rule under *When a link comes from*, and an address or keyword rule
  under *or its address has* (a keyword as *the word "invoice"*) — one list, so the order counts across
  all of them. An app rule shows its app's icon (when the program can be found), and *Goes to
  profile* the browser's icon as that profile shows it. With fewer than two categories the list says
  to make another first — a rule needs somewhere else to send links.
- **Rules on / off — one switch for all of them**, reachable three ways: the **Use rules** tick at the
  top of the Rules tab, *Use rules* in the dock's right-click menu, and its own **keyboard shortcut**
  (on the *Shortcuts* tab, suggested Ctrl+Alt+R, or the nearest free one). Off, every link simply opens in
  the live category — switch category and all links follow. The note says which it is now.
- **Add apps…** opens a ready-made list of about 175 popular apps in groups — chat & social, work &
  office, email, AI assistants, notes & study, gaming, development, music & video, creative, files &
  sync, utilities — with a search box. Tick any number, choose their category, **Add**. Above the
  built-in groups, four come from your own PC:
  - **Opened links lately** — the programs that really opened links here;
  - **Opened the most links** — the same from the link log, the busiest first, with how many links
    each opened;
  - **Running now** — every program open at the moment: open the app first, then find it here;
  - **On this PC** — every program in the Start menu.

  The search looks through all of them, so an app the built-in list does not know can be found by
  name without looking for its program file. **Browse for a program…** takes any other.
- **How the app is known:** when a program opens a link, Windows starts LinkPilot from inside
  it, so LinkPilot asks Windows who started it. Some apps start a Windows helper just to open the
  link — NetBird uses `rundll32`, others `cmd` — and then LinkPilot looks past it to the app that
  started it, so an app rule for that app works. (A rule made for `rundll32.exe` itself still wins.)
  A few apps — mostly from the Microsoft Store — hand links over through a Windows go-between that
  hides them; their rules cannot see them, so use an address rule for those. *Opened links lately*
  shows what really opened your links.

## Ask every time

A category can open no browser of its own and **ask** instead. On the Categories tab, select the
category, pick **Ask every time** at the bottom of the browsers on the right, and press *Use this*
(the short guide can make one for you, called *Ask*). Handy while many links come that no rule covers
yet.

- Each link it gets shows a small window **next to the mouse**: the link's site, and every category
  with a browser, one row each with its icon and how many links went to it and when the last did.
  Pick one and the link opens there.
- **Enter**, a double-click or a row's number **1–9** opens; **Esc** or *Don't open* opens nothing —
  the link log keeps it as *(not opened)*, so *Open again* there can still send it.
- **Sort by** *Name*, *Most used* or *Recently used*; the choice is kept.
- **Remember for github.com** also adds an address rule, so that site goes straight to the one you
  picked from then on.
- It works as the live category, and as the category of a rule (Signal → Ask every time). It needs
  at least one other category with a browser.

## Link cleaning

Many links carry extra parts that only say where the click came from — `utm_source=newsletter`,
`fbclid=…`, YouTube's `si=…` — and some go through a middleman first, so it learns where you went:
`google.com/url?q=<the real link>`, Outlook's Safe Links, Facebook's `l.php`. The **Link cleaning** tab
takes both out before the link is handed on:

- **Skip redirects** — 34 middlemen whose links carry the real one inside: Google search results,
  Gmail and Docs, Google Ads, the Google app's shared links, Outlook and Teams Safe Links, Facebook,
  Messenger, Instagram, YouTube, Steam, LinkedIn, DuckDuckGo, VK, Slack, Reddit (and its emails),
  Tumblr, eBay, Adjust app links — and 13 **affiliate networks**, the shopping links that pay
  whoever sent them (Rakuten, Awin, Tradedoubler, Admitad, CJ, ShareASale, Skimlinks, VigLink, Digidip,
  Webgains, idealo, Partner-ads, FlexOffers). The real link opens directly.
- **Remove tracking** — 314 known tracking parts: `utm_*`, `fbclid`, `gclid` and the other ad
  click IDs, newsletter and email tracking (Mailchimp, HubSpot, Klaviyo, Marketo, Salesforce…),
  analytics such as Matomo and Adobe; on their own sites the share and tracking parts of Google
  search, YouTube, Spotify, TikTok, Instagram, Facebook, X, LinkedIn, Reddit and Wikipedia; and the
  **online shops'** tracking, share and affiliate parts — Amazon, eBay, AliExpress (search results
  included), Allegro, Temu, Shein, Etsy, Walmart, OLX, Ceneo (mostly from the
  [ClearURLs](https://clearurls.xyz), AdGuard and Brave lists). The product itself stays in the link,
  and so does what you searched for on Google. Only parts known to be tracking are removed, and
  everything else in the link stays exactly as it was, so links keep working.
- Both are on to begin with; every part and middleman has its own tick, and **Add a part…** adds
  your own. **Try a link** shows what any link becomes.
- **Clean copied links too** (off to begin with) — when you copy a link — YouTube's *Copy link*, a
  link from a chat — the same cleaning is done to it right away, so pasting gives the clean link. A
  short note says so, and it goes in the link log as *(copied)*. Only a copied link on its own is
  changed: text with a link inside, several lines, and anything a password manager copies (they mark
  it private) are left exactly as they are. What you copy is only looked at, never kept or sent.
  Windows' clipboard history (Win+V) still shows the link as it was copied as well — Windows keeps
  it before any program can clean it.

When a link you open had something taken out, a short note says so — *Link cleaned*, and what went
— while the page opens. It happens before the rules look at the link, so a rule for `github.com` also
catches a Google link to GitHub. Two things it cannot do: a link clicked *inside* a browser never
reaches LinkPilot — Google's own result links included — so it is not cleaned; and short links
(`bit.ly`, `t.co`) are not followed, because only their server knows where they lead and LinkPilot
does not look links up online.

## Link log

The **Link log** tab (also in the dock's right-click menu) lists every link LinkPilot handed
on: when, which app it came from, where it opened and why ("Work — rule: comes from Signal"), and
whether it was changed. Select one to see it in full — with the link as it came, if it was changed.
**Copy link**, **Open again**, **Clear log**. The list updates by itself as links come in. A link left
unopened in the *Ask every time* window shows as *(not opened)*; *Open again* sends it through again.

The log stays on this computer only, in `link-log.txt` next to the program, and keeps the newest
1000 links. **Keep a log of links** switches it off.

## Updates

LinkPilot asks GitHub whether a newer version exists by itself — on to begin with: 30 seconds after
it starts, every 6 hours while it runs, and whenever its window opens or comes to the front — at most
every 5 minutes. A check that fails says nothing (it is almost always "no internet"). Untick **Check
for updates automatically** on the *About & updates* tab (or among the setup screen's choices) to stop
that; **Check now** there always asks, gives up after 10 seconds, and says in words what went wrong
if it cannot. **GitHub** there opens the releases page.

When there is a newer version, a strip at the top of the window says so, with **Update** and **✕**
(✕ hides it until LinkPilot next starts). The dock mentions it once too, and its menu has *Update to
X…*. **Update** (or **Get update** on *About & updates*) downloads `LinkPilotSetup-<version>.exe` from
GitHub, with the progress shown, runs it and closes LinkPilot: the installer shows only its progress,
puts the new version in place, keeps your settings, starts LinkPilot again and closes by itself; a
short note then says *LinkPilot updated*. If the install fails, the installer puts the previous
version back and starts it. If the download fails, LinkPilot says why, with **Try again** and
**GitHub** (the release page) — nothing opens by itself. The downloaded installer is deleted the next
time LinkPilot starts. A copy that is a git clone is updated with `git pull` and `build.cmd` instead.
See [Privacy and security](#privacy-and-security) for what that connection involves.

Under *More* on the same tab: **Take the short guide again** and **Show the setup screen again** (see
[Setting it up](#setting-it-up)).

## The window

Click the dock icon, or **Switch browser** on your Desktop. Each tab opens with its name and one line
saying what it is for, and one round **ⓘ** next to that heading: click it and a panel explains every part of that tab — each section by name, in short
points; a click on the ⓘ again, or anywhere else, closes it. While LinkPilot is not the default
browser, the window opens on the **setup screen** instead (see [Setting it up](#setting-it-up)).

- **Top** — where links go right now, with the category's icon. While LinkPilot is not the
  default browser it says so instead — links then go straight to the default browser (it names
  which) — and a yellow strip offers to set it up. It checks again whenever the window comes to the
  front.
- **Tabs:**
  - **Categories** — *Your categories* on the left, each with its icon, what it opens in, its
    shortcut and whether it is in the dock; the live one is tagged **LIVE**; below them **Show in
    dock** and **Dock icon…**. With none yet, the list says how to start. On the right, *Browsers and
    profiles on this PC* — every browser with its profiles, and last, **Ask every time** (see
    [Ask every time](#ask-every-time)). Nothing is hard-coded: browsers come from
    the Windows registry, Firefox profiles from `profiles.ini`, and Chrome / Edge / Brave /
    Vivaldi profiles from each browser's own `Local State` file, so the names shown are the names
    you gave them. Each profile shows its own picture where the browser keeps one, and which of
    your categories use it (`Work (Profile 2)  ←  Work`).
  - **Rules**, **Shortcuts**, **Link cleaning**, **Link log**, **About & updates** — see their
    sections above.
- **Bottom** — *Switch to:* one button per category, with its icon, the live one filled blue: click
  one and links go there from that moment on.

To set a category up: select it on the left, select a profile on the right, press **Use this for …**.

## Files

| | |
|---|---|
| **Switch browser** (Desktop) | opens the window |
| **LinkPilot** (Start menu) | opens the same window |
| **LinkPilot** (Startup folder) | starts the dock when you sign in. Delete it to stop that — nothing else depends on it |
| **Back to normal.cmd** | the panic button — every link goes to your original default browser: no category is live any more, and the rules are switched off too. Nothing is uninstalled and no category or rule is lost |
| `config.txt` | your categories, rules and settings, in plain text. The window writes it; you can edit it by hand (see [config.txt](#configtxt)) |
| `file-icons\` | the icon web page files show right now — drawn after each switch; safe to delete |
| `recent-apps.txt` | the programs that opened links lately — offered when you add app rules |
| `link-log.txt` | the link log — the newest 1000 links; stays on this computer |
| **Install.cmd** | double-click to register LinkPilot with Windows — runs `install.ps1` |
| `install.ps1` | adds LinkPilot to the Windows list of browsers |
| `uninstall.ps1` | removes it completely — after making your previous browser the default again |
| **Uninstall LinkPilot.exe** | put there by the installer — what Settings → Apps runs to remove LinkPilot (the same window as the installer) |
| `get.ps1` | the one-command installer and updater (see [Install](#install)) |
| `build.cmd` | rebuilds the program |
| `BrowserSwitch.ico` | the icon — two opposite arrows — built into the exe |
| `LICENSE` | MIT — use, change and share it freely, keeping the copyright notice |

## config.txt

One setting per line, `name=value`, in plain text. The window keeps it up to date; you can edit it by
hand too, and the dock picks the change up by itself. A missing line means the setting as it is to
begin with.

| Line | What it is |
|---|---|
| `category=Work\|C:\…\brave.exe\|--profile-directory="Profile 2"\|Brave - Work` | a category: name, browser, what it is started with, what it shows. Up to six more after it: in the dock (`1`), the dock icon, its shortcut, the shortcut suggested, `no` if next / previous skip it, `off` if its shortcut is off |
| `category=Ask\|(ask)\|\|Ask every time` | an *Ask every time* category: `(ask)` in place of a browser |
| `active=Work` | the live category |
| `recent-live=Work\|Home\|School` | the categories by recent use, the live one first — the smart queue's order |
| `fallback=` | the browser links went to before LinkPilot, recorded at install |
| `rule=on\|app\|Signal\|Signal.exe\|Work` | a rule: `on` or `off` \| `app`, `address` or `word` \| shown as \| matches \| category. For `word`, it matches the keyword's words in small letters, one space between: `rule=on\|word\|Pull-Request\|pull request\|Work` |
| `rules=on` | `off`: every rule kept, none used |
| `rules-key=`, `default-rules-key=`, `rules-key-on=` | the shortcut that turns rules on and off, the one suggested, and `no` if it is switched off |
| `shortcuts=off` | `on`: keyboard shortcuts work |
| `next=`, `previous=`, `default-next=`, `default-previous=`, `next-on=`, `previous-on=` | the next / previous shortcuts, the ones suggested, and `no` if one is switched off |
| `smart-queue=on` | `off`: next / previous go in list order instead of by recent use |
| `ask-sort=most` | how the *Ask every time* window sorts: `most` (used), `name` or `recent` |
| `docktips=name` | `switch`: dock icons say *Switch to Work* when hovered |
| `fileicon=page` | the look of `.htm` / `.html` files: `page`, `browser` or `own` |
| `taskbar=on` | `off`: no taskbar button, the window lives in the dock |
| `clean=on`, `unwrap=on` | remove tracking parts; skip redirects |
| `clean-copied=off` | `on`: clean a link when it is copied, too |
| `clean-off=`, `unwrap-off=` | one line per tracking part or redirect switched off |
| `clean-add=ref\|example.com` | a tracking part of your own, and where (empty: everywhere) |
| `log=on` | keep the link log |
| `check-updates=on` | `off`: ask GitHub for a newer version only when you press *Check now* |
| `setup=done`, `closed-once=yes`, `version=` | the setup screen was seen; the note about closing the window was shown; the version that last ran |

## Setting it up

The one command under [Install](#install) does all of this by itself. By hand:

1. **Get the program**, either way:
   - **Download** from the [Releases page](https://github.com/Husarp/linkpilot/releases) —
     `LinkPilot-<version>.zip` — and unzip it
     into a folder you will keep. The program is not signed, so Windows may say *"Windows protected your PC"* the
     first time — *More info → Run anyway*.
   - or **build it**: double-click `build.cmd`. It makes `BrowserSwitch.exe` with the C# compiler
     already inside Windows (.NET Framework 4). Nothing is downloaded.

   Keep the folder where it is afterwards: Windows is told to run the program from there.
2. **Double-click `Install.cmd`** from File Explorer. It runs `install.ps1`, which registers Browser
   Switch and opens it. Run it yourself: registry changes made from inside some programs — AI
   coding assistants included — land in a private copy of the registry that only that program sees,
   and Windows Settings would never see LinkPilot.

LinkPilot is **not** your default browser yet after that — Windows requires you to choose it
yourself. So LinkPilot opens on a **setup screen** — on its first start, and again whenever it
is not the default browser — in three steps (Back and Skip on the left, the blue button on the right):

1. **How it works** — why it has to be the default browser (every link must pass through it, and
   Windows sends links only to the default browser), and why it can be trusted: fully offline,
   sends nothing, made for personal use, open, easy to undo.
2. **Your choices** — clean links (on), with a real link shown before and after cleaning; clean
   copied links too (off), keep a log of links (on), check for updates automatically (on). All can be
   changed later on their tabs.
3. **Make it your default browser** — *Open Windows Settings* opens on LinkPilot's own page;
   press *Set default*; the setup screen notices by itself. If LinkPilot already is the
   default, this step just says so.

No program or command can make this choice for you — Windows ignores them, which is what keeps
programs from taking over your browser.

Then a big **You're all set!** — and if you have no category yet, the browser you used until now
becomes the first one, live (for example **Firefox**), so links keep going exactly where they went.
**Set them up →** goes on to the short guide; *Finish - I'll look around myself* opens the main
window instead.

### The short guide

A few optional screens — about a minute — each one decision with a good answer filled in already:
press the blue button on every screen and the setup is good. *Next* saves what the screen shows,
*Skip* saves nothing, and a screen that does not apply is passed over.

1. **Your categories** — one tick per browser profile on this PC, each with a name suggested (the
   name you gave the profile, or the browser's own). A profile a category already uses shows that
   category's name instead.
2. **How you switch** — keyboard shortcuts on, with the keys suggested and whether each is free;
   the smart queue; and an icon per category in the dock.
3. **When no rule decides** — *Open in the live category*, or *Ask every time* (see
   [Ask every time](#ask-every-time)). Only with two categories or more.
4. **Rules for your apps** — up to six apps that opened links on this PC (or are running now) and
   have no rule yet, each with a category guessed for it — Teams to a category called Work, a game
   to Home; none is ticked, so tick the ones you want. *More apps…* opens the full list. Only with two
   categories or more.
5. **Where things live** — one line per tab, and *Finish*.

**To take it again:** *About & updates* tab → **Take the short guide again**. **Show the setup screen
again**, just under it, brings back the three steps above.

By hand, in Settings → Apps → Default apps there are two ways:

- **By app** — under *Set defaults for applications*, find **LinkPilot** (blue square, two
  arrows), open it, press **Set default**.
- **By link type** — in the top box, *Set a default for a file type or link type*, type `HTTP`, click
  the app shown under it, and choose **LinkPilot**. Then the same for `HTTPS`.

Until you do, nothing changes. If you skip the setup screen, the window says so across the top in
yellow, and its *Set it up* button brings the setup screen back.

**If LinkPilot is not offered:** double-click `Install.cmd` again — yourself, not from inside
another program — then close Settings completely (clicking the X only hides it) and open it again.
What `install.ps1` sets up, and why:

- **the registration** — a link handler, `BrowserSwitchURL`, and an entry in `RegisteredApplications`
  saying it handles http, https, .htm and .html. This is what puts it in Windows' list of apps for
  links;
- **an identity** — a name, an icon and a publisher, both inside the exe and on the link handler, and
  one app ID, `com.husarp.linkpilot` (the same as the Android app's), shared by the link handler and
  the Start menu shortcut. Before 2.2.0 the exe had no icon and a blank name, so the most Windows could call it was
  "BrowserSwitch.exe";
- **a Start menu shortcut**, and an entry in Settings → Apps → Installed apps, so it can be found and
  removed like any other program.

## If something goes wrong

A link must never die because the switcher is confused. Where a link goes is worked out in one place,
and `--dry` reports exactly what a real click would do, so the two can never disagree:

0. first the link is cleaned — redirects skipped, tracking removed — if link cleaning is on;
1. *(while rules are on)* the first ticked rule that matches, if its category's browser
   still exists;
2. the live category, if its browser still exists;
3. otherwise the browser that was your default **before** LinkPilot was installed — recorded at
   install time, so a machine with nothing set up behaves exactly as it did before (installed again
   or moved while LinkPilot already is the default, it is taken over from the copy installed
   before);
4. otherwise the first browser Windows lists.

If step 1 or 2 lands on an *Ask every time* category, the small window asks which category's browser
to use (and `--dry` just says it would ask); *Don't open* or Esc opens nothing.

Anything unexpected is written to `errors.log`. Tested against: no config at all, a config with no
categories, a live category with no browser chosen, and a category pointing at a browser that has
been deleted — all four fall through correctly.

Two levels of undo, on purpose:

- **Pause** — `Back to normal.cmd`. Instant, nothing removed; pick a category and tick *Use rules*
  on the Rules tab to switch back on.
- **Remove** — Settings → Apps → Installed apps → LinkPilot, or run `uninstall.ps1`. While
  LinkPilot is the default browser, it first opens Settings on the page of the browser you used
  before — one click on *Set default* (Windows lets no program do that click) — waits for it, and
  then removes everything, so Windows is never left pointing at something that no longer exists.
  Every registry key it created lives under your own user account and is deleted.

## Command line

```
BrowserSwitch.exe <url>          open a link in the live category
BrowserSwitch.exe                open the window (starting the dock if it is not running)
BrowserSwitch.exe --tray         start the dock only - what runs when you sign in
BrowserSwitch.exe --switch Work  make a category live, no window
BrowserSwitch.exe --reset        every link to the original default browser - and rules off
BrowserSwitch.exe --dry <url>    write where the link WOULD go, and what cleaning changes, to dry-run.log; open nothing
   --dry <url> --from Signal.exe   ...as if the link had been clicked in Signal (tries app rules)
BrowserSwitch.exe --list         write the browsers and profiles it can see to detected.txt
BrowserSwitch.exe --selftest     build the window in memory and report what it holds, without showing it
```

## One caveat, with Firefox

Firefox is launched with `-P "profile name"`. If Firefox is already running with a different profile,
it may open the link in the window that is already there rather than starting the profile you asked
for. Chrome, Edge and Brave handle `--profile-directory` cleanly whether or not they are running.
