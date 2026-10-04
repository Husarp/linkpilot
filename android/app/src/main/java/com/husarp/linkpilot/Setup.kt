package com.husarp.linkpilot

import android.content.Context
import android.os.Build
import android.widget.Toast
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.selection.toggleable
import androidx.compose.foundation.layout.widthIn
import androidx.compose.runtime.saveable.Saver
import androidx.compose.runtime.snapshots.SnapshotStateMap
import androidx.compose.ui.semantics.Role
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Checkbox
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

// The setup screen - the same steps as LinkPilot for Windows, fitted to a phone. It shows on
// the first start, and again whenever LinkPilot is not the default browser.
//
//   1  How it works: why it has to be the default browser, and why it can be trusted.
//   2  Your choices: link cleaning (with a link before and after), the link log.
//   3  Make it the default - Android's own question - or "already done". Coming back from it as the
//      default browser goes on by itself.
//   4  You're all set - finish here, or go on to the short guide.
//   5-8  The short guide, all optional: your categories, how you switch, rules for your apps, and
//      where things live. A screen that does not apply is passed over. Home's About card starts it
//      again at 5 (start).
//
// Back and Skip on the left, the button that goes on on the right. Next saves what is on the screen,
// Skip saves nothing. Run again from About, the guide has no Back on its first screen - Android's back
// ends it there.
@Composable
fun SetupScreen(m: Model, makeDefault: () -> Unit, openSettings: () -> Unit, start: Int, finish: () -> Unit,
                openTab: (Int) -> Unit) {
    m.tick
    val ctx = LocalContext.current
    val store = m.store
    var step by rememberSaveable(start) { mutableIntStateOf(start) }
    val isDefault = m.isDefault

    // step 3 notices by itself when Android's question was answered with LinkPilot
    LaunchedEffect(isDefault, step) { if (step == 3 && isDefault && m.askedForDefault) step = 4 }

    // The browsers step 5 offers - looked up away from the screen (the work profile's apps take a
    // while), from step 4 on. null: not known yet.
    var offered by rememberSaveable(stateSaver = OffersSaver) { mutableStateOf<List<Offer>?>(null) }
    LaunchedEffect(step >= 4) {
        if (step >= 4 && offered == null) offered = withContext(Dispatchers.Default) { guideBrowsers(ctx) }
    }

    // The guide's screens that apply: 5 needs a browser to offer, 7 two categories (with one, a rule
    // changes nothing). go() steps over the others.
    fun applies(s: Int) = when (s) { 5 -> offered?.isNotEmpty() != false; 7 -> store.categories.size >= 2; else -> true }
    fun go(by: Int) { var s = step + by; while (s in 5..7 && !applies(s)) s += by; step = s }
    // counted as if 7 will apply whenever 5 does - 5 is where the second category is made - so the
    // total does not grow on the way
    val extras = listOf(5, 6, 7).filter { applies(it) || (it == 7 && applies(5)) }
    fun count(s: Int) = "Optional · ${extras.indexOf(s) + 1} of ${extras.size}"
    // run again from About: the first screen has nothing before it in this run
    val first = start == 5 && step == extras.first()
    BackHandler(enabled = step >= 5) { if (first) finish() else go(-1) }
    // the browsers were not known yet when 5 came up, and there are none
    LaunchedEffect(step, offered) { if (step == 5 && offered?.isEmpty() == true) go(1) }

    Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(16.dp),
           verticalArrangement = Arrangement.spacedBy(12.dp)) {
        when (step) {
            1 -> {
                Heading("Welcome to LinkPilot", "Step 1 of 3 - how it works")
                Sub("One step is needed - here is why")
                Body("LinkPilot decides which browser each link opens in. For that, every link has to pass through it " +
                     "first, and Android sends links only to your default browser. So LinkPilot has to be your default " +
                     "browser; it then hands each link straight on to the browser you chose. This is the only way it can work.\n" +
                     "Android lets only you make this choice - it asks you itself. Step 3 shows you where.")
                Sub("Why you can trust it")
                Column(Modifier.fillMaxWidth().background(MaterialTheme.colorScheme.surfaceVariant, RoundedCornerShape(12.dp)).padding(14.dp),
                       verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Trust("Offline", "Your links, settings and the link log stay on this phone. LinkPilot goes online only " +
                          "to ask GitHub for a newer version (you can switch that off on Home, under Updates) and to download " +
                          "it when you tap Update.")
                    Trust("Sends nothing", "No account, no ads, no tracking, nothing collected. Your links, settings and the " +
                          "link log stay on this phone.")
                    Trust("Made for personal use", "A small project made for private use, and shared freely - nothing is sold.")
                    Trust("Open", "Every file is public on GitHub, to read or to build yourself.")
                    Trust("Easy to undo", "Your browsers stay as they are. Make one of them the default again at any time, or " +
                          "uninstall LinkPilot.")
                }
                Nav(back = "Skip setup" to finish, next = "Next  →" to { step = 2 })
            }
            2 -> {
                Heading("Your choices", "Step 2 of 3 - both can be changed later, on their tabs")
                SwitchRow("Clean links", "Takes tracking out of links - utm_source, fbclid, YouTube's si... - and skips redirects " +
                    "such as google.com/url?q=..., so the page opens directly. Only parts known to be tracking are removed.",
                    store.cleanOn && store.unwrapOn) { store.cleanOn = it; store.unwrapOn = it; m.changed() }
                CleanExample(store)
                SwitchRow("Keep a log of links", "Which app each link came from, where it opened, and what was changed - on " +
                    "this phone only, the newest 300.", store.logOn) { store.logOn = it; m.changed() }
                Nav(back = "←  Back" to { step = 1 }, next = "Next  →" to { step = 3 })
            }
            3 -> {
                Heading("Make LinkPilot your default browser", "Step 3 of 3 - the one step Android leaves to you")
                if (isDefault) {
                    Banner("✓", "Already done", "LinkPilot is your default browser - there is nothing to do here.")
                    Nav(back = "←  Back" to { step = 2 }, next = "Next  →" to { step = 4 })
                } else {
                    Body("1.  Press Make it the default below.\n" +
                         "2.  Android asks which app should be your default browser - choose LinkPilot, then Set as default.\n" +
                         "3.  You come back here, and this screen goes on by itself.")
                    Body("If Android does not ask (it stops asking after you said no twice), Open settings takes you to " +
                         "Default apps → Browser app instead.")
                    TextButton(onClick = openSettings) { Text("Open settings") }
                    Nav(back = "←  Back" to { step = 2 }, next = "Make it the default" to { m.askedForDefault = true; makeDefault() })
                }
            }
            4 -> {
                Banner("✓", "You're all set!", "LinkPilot is ready.")
                val live = store.live()
                Body(if (live != null) "Links open in ${live.name}, just as before."
                     else "Next: add categories on Home - Work, Home... each with its browser - and the Quick Settings tiles, " +
                          "to switch between them with one tap.")
                val work = if (Profiles.isWorkCopy(ctx)) null else Profiles.others(ctx).firstOrNull()
                if (work != null) {
                    Body("Your work profile (Island): install LinkPilot there too - in Island, clone it - and make it that " +
                         "profile's default browser as well. Then its browsers can be categories here, and links tapped " +
                         "in work apps come here too, so these rules can name work apps. Add a category on Home says if " +
                         "anything else is still needed.")
                    if (Profiles.state(ctx, work) == Profiles.State.NotInstalled) IslandButton()
                }
                Column(Modifier.fillMaxWidth().background(MaterialTheme.colorScheme.surfaceVariant, RoundedCornerShape(12.dp)).padding(14.dp)) {
                    Text("A few quick things - about a minute, all optional", fontWeight = FontWeight.Bold)
                    Text("Your categories · How you switch · Rules for your apps", style = MaterialTheme.typography.bodySmall)
                }
                Nav(back = "Finish - I'll look around myself" to finish, next = "Set them up  →" to { store.setupDone = true; go(1) })
            }
            5 -> offered.let { if (it == null) Body("Looking for your browsers…")
                                else GuideCategories(m, it, count(5), back = if (first) null else { { go(-1) } }, skip = { go(1) }, next = { go(1) }) }
            6 -> GuideSwitch(m, count(6), back = if (first) null else { { go(-1) } }, skip = { go(1) }, next = { go(1) })
            7 -> GuideRules(m, count(7), back = { go(-1) }, skip = { go(1) }, next = { go(1) })
            else -> GuideTour(back = { go(-1) }, finish = finish, openTab = openTab)
        }
    }
}

// Step 2: the same link before and after cleaning - worked out by the real cleaner, so it cannot drift.
@Composable
private fun CleanExample(store: Store) {
    val before = "https://www.google.com/url?q=https://www.youtube.com/watch%3Fv%3DdQw4w9WgXcQ%26si%3DxYz123&sa=D&utm_source=chat"
    val after = Cleaner.apply(before, store.cleanOptions()).url
    val grey = MaterialTheme.colorScheme.onSurfaceVariant
    Column(Modifier.fillMaxWidth().background(MaterialTheme.colorScheme.surfaceVariant, RoundedCornerShape(12.dp)).padding(12.dp),
           verticalArrangement = Arrangement.spacedBy(2.dp)) {
        Text("Before", style = MaterialTheme.typography.labelMedium, color = grey)
        Text(before, style = MaterialTheme.typography.bodySmall, color = grey, maxLines = 1, overflow = TextOverflow.Ellipsis)
        Text("After", style = MaterialTheme.typography.labelMedium, color = grey, modifier = Modifier.padding(top = 4.dp))
        if (after != before) Text(after, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Bold,
                                  color = MaterialTheme.colorScheme.primary)
        else Text("$before  (left as it is)", style = MaterialTheme.typography.bodySmall, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}

// ---- the short guide (steps 5-8) ---------------------------------------------------------------------

// A browser the guide offers as a category - on this phone, or in the work profile when LinkPilot can
// reach its copy there (as Add a category lists them).
private class Offer(val pkg: String, val profile: Long?, val label: String) {
    val key get() = Profiles.appKey(pkg, profile)
}

// The offers kept through a turn of the phone: three strings each ("" for no profile).
private val OffersSaver = Saver<List<Offer>?, ArrayList<String>>(
    save = { list -> list?.flatMapTo(ArrayList()) { listOf(it.pkg, it.profile?.toString() ?: "", it.label) } },
    restore = { flat -> flat.chunked(3).map { Offer(it[0], it[1].toLongOrNull(), it[2]) } })

// Ticks and names typed on a screen, kept through a turn of the phone.
private fun <T : Any> stateMapSaver() = Saver<SnapshotStateMap<String, T>, HashMap<String, T>>(
    save = { HashMap(it) }, restore = { mutableStateMapOf<String, T>().apply { putAll(it) } })

private fun guideBrowsers(ctx: Context): List<Offer> {
    val here = Browsers.all(ctx)
    val pkgs = here.map { it.pkg }.toSet()
    val work = Profiles.others(ctx).filter { Profiles.state(ctx, it) == Profiles.State.Ready }.flatMap { user ->
        Profiles.apps(ctx, user).filter { it.pkg in pkgs || Profiles.isKnownBrowser(it.pkg) }.map { Offer(it.pkg, it.profile, "${it.label} (work)") }
    }
    return here.map { Offer(it.pkg, null, it.label) } + work
}

// Step 5: one tick per browser. Nothing is ticked to begin with - phones often come with browsers
// nobody uses. A browser a category already opens shows ticked, greyed, with that category's name.
@Composable
private fun GuideCategories(m: Model, offered: List<Offer>, count: String, back: (() -> Unit)?, skip: () -> Unit, next: () -> Unit) {
    val store = m.store
    val cats = store.categories
    val live = store.live()
    fun owner(o: Offer) = cats.firstOrNull { it.pkg == o.pkg && it.profile == o.profile }
    val ticked = rememberSaveable(saver = stateMapSaver()) { mutableStateMapOf<String, Boolean>() }
    // the suggested name: the browser's own; " 2" added if a category (or a row above) has it already
    val names = rememberSaveable(saver = stateMapSaver()) {
        mutableStateMapOf<String, String>().apply {
            val taken = cats.map { it.name.lowercase() }.toMutableSet()
            offered.filter { owner(it) == null }.forEach { o ->
                var name = o.label
                var n = 2
                while (name.lowercase() in taken) name = "${o.label} ${n++}"
                taken += name.lowercase()
                put(o.key, name)
            }
        }
    }
    val chosen = offered.filter { owner(it) == null && ticked[it.key] == true }
    fun bad(o: Offer): Boolean {
        val name = names[o.key].orEmpty().trim()
        return name.isEmpty() || cats.any { it.name.equals(name, ignoreCase = true) } ||
               chosen.count { names[it.key].orEmpty().trim().equals(name, ignoreCase = true) } > 1
    }

    Heading("Your categories", "$count - one category per browser you use. Change them later on Home.")
    if (cats.isNotEmpty()) Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Text("Already set up:", style = MaterialTheme.typography.labelLarge)
        cats.forEach { c ->
            Row(verticalAlignment = Alignment.CenterVertically) {
                SmallIcon(remember(c) { m.icon(c) })
                Text(c.name + if (c.name == live?.name) "  ·  live" else "")
            }
        }
    }
    Column {
        offered.forEach { o ->
            val by = owner(o)
            val on = by != null || ticked[o.key] == true
            Row(Modifier.fillMaxWidth().toggleable(on, enabled = by == null, role = Role.Checkbox) { ticked[o.key] = it },
                verticalAlignment = Alignment.CenterVertically) {
                Checkbox(checked = on, onCheckedChange = null, enabled = by == null, modifier = Modifier.padding(12.dp))
                val icon = remember(o.key) { m.icon(Category("", o.pkg, o.profile)) }
                if (icon != null) Image(icon, null, Modifier.size(36.dp)) else Spacer(Modifier.size(36.dp))
                Spacer(Modifier.width(8.dp))
                Column(Modifier.weight(1f)) {
                    Text(o.label)
                    if (by != null) Text("← ${by.name}", style = MaterialTheme.typography.bodySmall,
                                         color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
            if (by == null && on) OutlinedTextField(value = names[o.key].orEmpty(), onValueChange = { names[o.key] = it },
                label = { Text("Name") }, singleLine = true, isError = bad(o),
                supportingText = { if (bad(o)) Text(if (names[o.key].isNullOrBlank()) "Give it a name" else "There is one with this name already") },
                modifier = Modifier.fillMaxWidth().padding(start = 48.dp, bottom = 8.dp))
        }
    }
    Text("Tip: names like Work, Home or School make switching easy to read.", style = MaterialTheme.typography.bodySmall,
         color = MaterialTheme.colorScheme.onSurfaceVariant)
    Nav(back = back?.let { "←  Back" to it }, skip = "Skip" to skip, next = "Next  →" to {
        if (chosen.none { bad(it) }) {
            if (chosen.isNotEmpty()) {   // the live category stays as it is
                store.categories = store.categories + chosen.map { Category(names[it.key]!!.trim(), it.pkg, it.profile) }
                m.changed()
            }
            next()
        }
    })
}

// Step 6: the tile and the widget - each added at once, as on the Shortcuts tab - and the smart queue.
@Composable
private fun GuideSwitch(m: Model, count: String, back: (() -> Unit)?, skip: () -> Unit, next: () -> Unit) {
    val ctx = LocalContext.current
    var smart by rememberSaveable { mutableStateOf(m.store.smartQueue) }   // kept on Next only
    Heading("How you switch", "$count - switch categories without opening the app")
    Row(verticalAlignment = Alignment.CenterVertically) {
        Column(Modifier.weight(1f)) {
            Text("Quick Settings tile - Next browser", style = MaterialTheme.typography.titleMedium)
            Text(if (Build.VERSION.SDK_INT >= 33) "In the panel you pull down from the top"
                 else "By hand: pull down the panel › pencil › drag the tile up",
                 style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        if (Build.VERSION.SDK_INT >= 33) OutlinedButton(onClick = { addTile(ctx, Tiles.next) }) { Text("Add") }
    }
    Row(verticalAlignment = Alignment.CenterVertically) {
        Column(Modifier.weight(1f)) {
            Text("Home-screen widget", style = MaterialTheme.typography.titleMedium)
            Text("Shows the live category - a tap goes to the next", style = MaterialTheme.typography.bodySmall,
                 color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        OutlinedButton(onClick = {
            if (!Widgets.pin(ctx, true)) Toast.makeText(ctx, "If nothing appears: long-press an empty spot on the home screen › " +
                                                             "Widgets › LinkPilot.", Toast.LENGTH_LONG).show()
        }) { Text("Add") }
    }
    SwitchRow("Smart queue", "Next goes back to the category used before - one tap flips back, like Alt+Tab.", smart) { smart = it }
    Nav(back = back?.let { "←  Back" to it }, skip = "Skip" to skip, next = "Next  →" to {
        if (smart != m.store.smartQueue) { m.store.smartQueue = smart; m.changed() }
        next()
    })
}

// Step 7: apps that sent links lately, then those with the most links in the log - up to 6, none with a
// rule yet. Nothing is ticked: rules are personal, so this only suggests.
@Composable
private fun GuideRules(m: Model, count: String, back: () -> Unit, skip: () -> Unit, next: () -> Unit) {
    val store = m.store
    val cats = store.categories
    val ruled = store.rules.filter { it.byApp }.map { Profiles.appKey(it.match, it.profile) }.toSet()
    val often = m.log.orEmpty().map { it.from }.filter { it.isNotEmpty() }.groupingBy { it }.eachCount()
        .entries.sortedByDescending { it.value }.map { it.key }
    val apps = (store.recentApps + often).distinct().filter { it !in ruled }.take(6)
    val ticked = rememberSaveable(saver = stateMapSaver()) { mutableStateMapOf<String, Boolean>() }
    val goesTo = rememberSaveable(saver = stateMapSaver()) { mutableStateMapOf<String, String>() }
    fun target(app: String) = goesTo[app] ?: store.live()?.name ?: cats.firstOrNull()?.name ?: ""

    Heading("Rules for your apps", "$count - links from these apps can always go to one category")
    if (apps.isEmpty()) Body("Rules need a few links first - add them later on the Rules tab.")
    apps.forEach { app -> key(app) {
        var open by remember { mutableStateOf(false) }
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            // the tick and the app's name are one choice; the category beside them is its own button
            Row(Modifier.weight(1f).toggleable(ticked[app] == true, role = Role.Checkbox) { ticked[app] = it },
                verticalAlignment = Alignment.CenterVertically) {
                Checkbox(checked = ticked[app] == true, onCheckedChange = null, modifier = Modifier.padding(12.dp))
                val icon = remember(app) { m.appIcon(app) }
                if (icon != null) Image(icon, null, Modifier.size(32.dp)) else Spacer(Modifier.size(32.dp))
                Spacer(Modifier.width(8.dp))
                Text(m.appLabel(app), modifier = Modifier.weight(1f), maxLines = 1, overflow = TextOverflow.Ellipsis)
            }
            Box {
                // a long category name gives way, so the app's name still shows
                TextButton(onClick = { open = true }, modifier = Modifier.widthIn(max = 140.dp)) {
                    Text("→ ${target(app)}  ▾", maxLines = 1, overflow = TextOverflow.Ellipsis)
                }
                DropdownMenu(expanded = open, onDismissRequest = { open = false }) {
                    cats.forEach { c ->
                        DropdownMenuItem(text = { Text(c.name) }, onClick = { goesTo[app] = c.name; ticked[app] = true; open = false },
                                         leadingIcon = { val pic = remember(c) { m.icon(c) }
                                                         if (pic != null) Image(pic, null, Modifier.size(24.dp)) })
                    }
                }
            }
        }
    } }
    Nav(back = "←  Back" to back, skip = "Skip" to skip, next = "Next  →" to {
        val add = apps.filter { ticked[it] == true }.map { app ->
            val (pkg, profile) = Profiles.splitKey(app)
            Rule(on = true, byApp = true, shown = m.appLabel(app), match = pkg, category = target(app), profile = profile)
        }
        if (add.isNotEmpty()) { store.rules = store.rules + add; m.changed() }
        next()
    })
}

// Step 8: one line per tab - a tap ends the guide and opens that tab.
@Composable
private fun GuideTour(back: () -> Unit, finish: () -> Unit, openTab: (Int) -> Unit) {
    Heading("Where things live", "That's it. Here is where everything is.")
    listOf("Home" to "Your categories - tap one to make it live. About is at the bottom.",
           "Shortcuts" to "Quick Settings tiles and home-screen widgets.",
           "Rules" to "Links from an app, to a site, or with a word, sent to their own category.",
           "Cleaning" to "What is taken out of links, and copied links.",
           "Log" to "Every link, and what happened to it.").forEachIndexed { i, (tab, line) ->
        Column(Modifier.fillMaxWidth().clip(RoundedCornerShape(8.dp)).clickable { openTab(i) }.padding(vertical = 6.dp, horizontal = 4.dp)) {
            Text(tab, style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.primary)
            Text(line, style = MaterialTheme.typography.bodyMedium)
        }
    }
    Nav(back = "←  Back" to back, next = "Finish" to finish)
}

@Composable
private fun Heading(title: String, step: String) {
    Column {
        Text(title, style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Bold)
        Text(step, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
private fun Sub(text: String) = Text(text, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)

@Composable
private fun Body(text: String) = Text(text, style = MaterialTheme.typography.bodyMedium)

@Composable
private fun Trust(title: String, text: String) {
    Column {
        Text(title, fontWeight = FontWeight.Bold)
        Text(text, style = MaterialTheme.typography.bodySmall)
    }
}

@Composable
private fun Banner(mark: String, title: String, text: String) {
    Row(Modifier.fillMaxWidth().background(MaterialTheme.colorScheme.secondaryContainer, RoundedCornerShape(12.dp)).padding(16.dp),
        verticalAlignment = Alignment.CenterVertically) {
        Text(mark, fontSize = 32.sp, color = MaterialTheme.colorScheme.onSecondaryContainer)
        Spacer(Modifier.width(14.dp))
        Column {
            Text(title, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold,
                 color = MaterialTheme.colorScheme.onSecondaryContainer)
            Text(text, color = MaterialTheme.colorScheme.onSecondaryContainer)
        }
    }
}

@Composable
private fun Nav(back: Pair<String, () -> Unit>?, next: Pair<String, () -> Unit>, skip: Pair<String, () -> Unit>? = null) {
    Row(Modifier.fillMaxWidth().padding(top = 8.dp), verticalAlignment = Alignment.CenterVertically) {
        Row(Modifier.weight(1f), verticalAlignment = Alignment.CenterVertically) {   // what is left after the button
            if (back != null) TextButton(onClick = back.second) { Text(back.first) }
            if (skip != null) TextButton(onClick = skip.second) { Text(skip.first) }
        }
        Button(onClick = next.second) { Text(next.first) }
    }
}
