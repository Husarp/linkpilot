# LinkPilot for Android

The Android companion of [LinkPilot for Windows](https://github.com/Husarp/linkpilot): it
stands in as your default browser and hands every link on to the browser you chose, with tracking
taken out on the way. Switch where links go with one tap on a Quick Settings tile.

## What it does

- **Setup screen** — as on Windows: how it works and why it can be trusted, your choices (cleaning,
  with a real link shown before and after; the log), making it the default browser (Android's own
  question), and "You're all set". It shows on the first start and whenever LinkPilot is not the
  default browser.
- **The short guide** — after "You're all set", **Set them up →** goes on to a few optional screens,
  each with a good answer filled in already (*Finish - I'll look around myself* skips them):
  - **Your categories** — one tick per browser on the phone (and in the work profile, when LinkPilot
    can reach its copy there), none ticked to begin with, each with a name to change;
  - **How you switch** — add the *Next browser* tile and widget, and the smart queue;
  - **Rules for your apps** — up to six apps that sent links lately, none with a rule yet, each with
    a category to pick; none is ticked (only with two categories or more);
  - **Where things live** — one line per tab; a tap opens that tab.

  *Next* saves what the screen shows, *Skip* saves nothing. To run it again: Home › About › **Take the
  short guide again**; **Show the setup again** is just under it.
- **Categories** — Work, Home… each is one of the browsers on your phone. The live one gets your
  links. On the first start, the browser you used until then becomes the first category.
- **Quick Settings tiles** — three, for the panel you pull down from the top, on the **Shortcuts**
  tab; add any of them:
  - **Next browser** — each tap makes the next category live (a short message says which).
  - **Choose browser** — a tap shows the list of categories to pick from.
  - **Clean copied link** — a tap cleans the link you copied (see *Copied links* below).

  *Next browser* and *Choose browser* show the live category's name; a long press on any tile opens
  the app. **Change icon** picks each tile's icon from twelve (Android draws tile icons in one colour,
  so they are simple shapes). *Add* asks Android to place a tile - its question, "Add tile / Do not
  add tile", is Android's own wording.
- **Home-screen widgets** — two, *Next browser* and *Choose browser*, showing the live category with
  its browser's icon. *Add* on the Shortcuts tab places one.
- **Smart queue** (Shortcuts tab, on to begin with) — *Next browser* goes by recent use, like Alt+Tab:
  a tap goes back to the category used before, so one tap flips back; more taps in a row (within
  about three seconds) go on through the rest. Off: each tap goes to the next category in the list.
- **Link rules** — links from chosen apps (Signal → Work), to chosen sites (github.com → Home) or
  with a chosen word in them (invoice → Work) go to their own category, whatever is live. A keyword
  matches a whole word in the site, the path or a value after the `?` — not part of a word
  (`invoices`), nor a parameter's name (`?invoice=1`) — the same as on Windows; while you type it, the
  dialog says how many of the links in your log it would match, and on which sites. *Use rules*
  switches them all off and on.
- **Suggestions as you type a rule** — as on Windows: up to 8 sites under the address or keyword box,
  the ones in your link log first, then about 180 well-known ones (the same list as on Windows); only
  those that start with what you typed, or one of whose parts does (`re` → reddit.com, never
  youtube.com). Before you type: your own sites. A tap puts it in the box. For an app rule, the app
  list itself does this: the apps whose name (or a word of it) starts with what you typed, those that
  sent links first, then any other with it in the name.
- **Link cleaning** — the same as on Windows: 314 tracking parts removed — `utm_*`, `fbclid`, the ad
  click IDs, email tracking, the share parts of YouTube, Spotify, TikTok, Instagram, Facebook, X,
  LinkedIn, Reddit and Google search, and the online shops' (Amazon, eBay, AliExpress, Allegro, Temu,
  Shein, Etsy, Walmart, OLX…) — and 34 redirects skipped (`google.com/url?q=…`, Facebook's `l.php`,
  Outlook Safe Links, eBay, affiliate links…). Each has its own tick, grouped by site; **Try a link**
  shows what a link becomes. When a link had something taken out, a short message says so ("Link
  cleaned - removed utm_source").
- **Copied links** — **Clean copied links automatically** (Cleaning tab, off to begin with): copy a
  link with tracking and it is cleaned right away, so you paste the clean one. Android lets an app see
  what was copied only while it is on screen, so this is done by an accessibility service that
  listens to **one app only - the system's own "copied" preview** (Android 13+ shows it for every
  copy, with what was copied), never to other apps, and cannot read the screen. It has to be switched
  on once in Android's Accessibility settings (the app shows the way). Android 8-9: a plain clipboard
  listener. **Android 10-12 give no sign of a copy**, so there it is on a tap only. Always also on a
  tap: the **Clean copied link** Quick Settings tile, *Clean the copied link now*, or share a link to
  LinkPilot and pick *Copy clean link*. Only a link on its own is changed; what a password
  manager marks private is left alone.
- **Browsers in the work profile** (Island, or a work phone's profile) — a category can open its
  links in a browser over there, badged with the work briefcase. Needs Android 11+, **LinkPilot
  installed in both profiles** (Island: clone it - the setup and Home's (i) say so), and permission
  to connect the two copies. Android
  gives that switch only if the work profile's app lists LinkPilot as a "connected app"; Island
  does not, so it is allowed once from a computer:
  `adb shell appops set com.husarp.linkpilot INTERACT_ACROSS_PROFILES allow`.
  *Add a category* then lists the work profile's browsers (by name - Android does not tell one
  profile what the other's apps open; *Show all its apps* shows the rest). Where LinkPilot is still
  missing there, **Open Island** opens Island to clone it (Island lets no other app do the cloning).
- **One LinkPilot decides for both profiles** — make LinkPilot the work profile's default browser too,
  and the copy there only passes each link tapped in a work app - with the app it came from - to
  LinkPilot here, whose categories, rules and log cover it. So everything is set up in one place, and
  app rules can name work apps (marked *(work)*; Signal there and Signal here are different apps to a
  rule). If it cannot reach this copy, it decides by itself with its own settings - its Home says which.
  It cannot work without the copy there: Android lets an app start only itself in the other profile.
- **Link log** — every link, grouped by day: the app it came from (its icon), the site, the time,
  where it went and whether it was cleaned. Tap one for the whole link, what it came as, what was
  taken out and why it went there, with **Copy link** and **Open again**. The newest 300, on the
  phone only; can be switched off and cleared.
- **Updates** — the app checks GitHub for a newer version by itself (on to begin with) and installs
  it from inside the app; see [Updates](#updates).

## How it differs from Windows

| | Windows | Android |
|---|---|---|
| A category is | a browser **and profile** | a browser (Android browsers have no profiles another app can choose) |
| Switching | the dock, keyboard shortcuts | Quick Settings tiles, home-screen widgets, the app |
| Cleaning copied links | by itself, when you copy | by itself on Android 13+ (and 8-9), via an accessibility service that hears only the system's "copied" preview; else on a tap |
| Which app a link came from | always known | known when the app says so (most do) |

## Privacy

**Your links stay on your phone:** your categories, rules and link log stay in the app's own storage
on your phone and are never sent anywhere. The app uses the internet only to check GitHub for a newer
version and to download it — nothing about your links goes with it (Home › Updates; the automatic
check can be switched off there).

## Limits

Some links never reach any default browser, so LinkPilot cannot see them either:

- apps that open links in their **own built-in browser** (Instagram, Facebook, some others);
- some **Google apps**, which always open links in Chrome;
- links an app opens itself — a YouTube link goes straight to the YouTube app, if it is installed.

## Install

Download **`LinkPilot-Android-<version>.apk`** from the
[Releases page](https://github.com/Husarp/linkpilot/releases) on your phone and open it - the steps
are in the main README, [Install on Android](../README.md#install-on-android). Or build it yourself
from this folder (see [Building](#building)).

Then, in the app: **Make it the default** — Android shows its own "Set as default browser?"
question; no app can do this without you.

## Updates

- **Checked by itself** — on to begin with: when the app opens and whenever you come back to it, at
  most every 5 minutes. A check that fails says nothing (it is almost always "no internet").
- **Home › Updates** — *Check for updates* (the on/off switch), your version with **CHECK NOW** (gives
  up after 10 seconds and says in words what went wrong), **GITHUB** (the releases page) and **GET
  UPDATE** when there is one.
- **A newer version** shows as a banner on Home with **UPDATE** and **✕** (✕ hides it until the app is
  next started; coming back from another app does not bring it back).
- **UPDATE** downloads the new APK inside the app, with the progress (0–100%), into the app's own
  storage — no browser, no Downloads folder — and hands it to Android's installer, which asks you to
  confirm. The downloaded file is deleted once the new version runs. Your settings are kept.
- **The first time only**, Android must let LinkPilot install apps: the app opens Android's *Install
  unknown apps* screen for it; switch it on and come back — the update carries on by itself.
- **If it fails**, it says why, with **TRY AGAIN** and **GITHUB** — nothing opens by itself.
- The copy in a work profile does not update itself: updating LinkPilot in your personal profile
  updates that one too.

Only an update signed with LinkPilot's own key installs over the app.

## Building

- Android Studio (its own Java), Android SDK 35.
- `gradlew assembleRelease` → `app/build/outputs/apk/release/app-release.apk` — optimised, about
  1 MB; use this one on a phone (a debug build scrolls noticeably slower). It is signed with the
  release key named in `~/.keystores/linkpilot-signing.properties` (`storeFile`, `storePassword`,
  `keyAlias`, `keyPassword`) if that file exists - only the author's PC has it - and with your own
  debug key otherwise. An APK signed with another key cannot be installed over the published one.
- `gradlew assembleDebug` → `app/build/outputs/apk/debug/app-debug.apk` — for testing.
- `gradlew testDebugUnitTest` — link cleaning checked against the same known answers as the Windows
  app, address and keyword rules, the smart queue's order, and the updater's rules (versions compared
  as numbers, which APK is the update, the 5-minute limit, the banner's ✕).

Kotlin, Jetpack Compose (Material 3); no other libraries. Minimum Android 8.0.

## Files

| File | What it is |
|---|---|
| `Cleaner.kt` | link cleaning — the same lists and rules as the Windows `Cleaner.cs` |
| `Router.kt` | which category a link goes to: rules first (app, address, keyword), then the live category |
| `Store.kt` | settings and the link log, on the phone; the smart queue (`NextQueue`) |
| `Browsers.kt` | the browsers and apps on the phone; whether LinkPilot is the default |
| `LinkActivity.kt` | receives every link and hands it on — nothing shows |
| `ShareActivity.kt` | Share → *Copy clean link* |
| `Tiles.kt` | the Quick Settings tiles (*Next browser*, *Choose browser*), and the icons all three pick from |
| `Widgets.kt` | the two home-screen widgets, and the list *Choose browser* shows |
| `Clipboard.kt` | *Clean copied link*: the tile, and the moment on screen it needs to read |
| `CopyWatch.kt` | copied links cleaned by themselves - the accessibility service |
| `Profiles.kt` | browsers in the work profile, and handing links to LinkPilot there |
| `Setup.kt` | the setup screen, and the short guide after it |
| `MainActivity.kt` | the app: what its screens share (`Model`), and the five tabs |
| `Screens.kt` | the tabs themselves: Home, Shortcuts, Rules, Cleaning, Log |
| `Updates.kt` | the updater: asking GitHub, the banner, Home › Updates, the download and handing it to Android's installer |
| `res/xml/update_paths.xml` | the one folder Android's installer may read the downloaded update from |
| `test/…/CleanerTest.kt` | link cleaning: the same known answers as the Windows app |
| `test/…/KeywordTest.kt` | keyword rules: which links a word matches |
| `test/…/NextQueueTest.kt` | the smart queue: what Next does tap after tap |
| `test/…/UpdateLogicTest.kt` | the updater: versions, which APK, how often GitHub is asked, the banner's ✕ |

## Licence

MIT — see [LICENSE](../LICENSE).
