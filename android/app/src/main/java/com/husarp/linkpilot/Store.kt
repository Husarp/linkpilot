package com.husarp.linkpilot

import android.content.Context
import android.content.SharedPreferences
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

// A category: a name and the browser its links open in. (Android browsers have no profiles another
// app can pick, so here a category is simply a browser.) profile: the serial number of another
// profile of the phone the browser is in - the work profile - or null for this one (Profiles.kt).
data class Category(val name: String, val pkg: String, val profile: Long? = null)

// Links from an app (byApp: match is its package name), to an address, or with a keyword in the address
// (byWord: match is the keyword as Router.keyword makes it, shown as typed), sent to a category.
// profile: for an app in the work profile, that profile's serial number (its links come here through
// LinkPilot over there - Profiles.relay); null for an app in this profile.
data class Rule(val on: Boolean, val byApp: Boolean, val shown: String, val match: String, val category: String,
                val profile: Long? = null, val byWord: Boolean = false) {
    fun describe() = if (byApp) "comes from $shown" else if (byWord) "word $shown" else "address $shown"
}

class LogEntry(val time: String, val from: String, val openedIn: String, val why: String,
               val asked: String, val opened: String, val changes: String)

// Everything LinkPilot keeps - on this phone only: the settings in the app's own preferences,
// the link log in its own file.
class Store(context: Context) {
    companion object {
        fun prefs(ctx: Context): SharedPreferences = ctx.applicationContext.getSharedPreferences("config", Context.MODE_PRIVATE)
    }

    private val prefs = prefs(context)
    private val logFile = File(context.applicationContext.filesDir, "link-log.txt")

    var categories: List<Category>
        get() = objects("categories").map {
            Category(it.getString("name"), it.getString("pkg"), if (it.has("profile")) it.getLong("profile") else null)
        }
        set(v) = put("categories", v.map { c ->
            JSONObject().put("name", c.name).put("pkg", c.pkg).also { o -> c.profile?.let { o.put("profile", it) } }
        })

    // Setting it - from the app, a tile or a widget - also makes it the most recent one (the smart queue).
    var active: String
        get() = prefs.getString("active", "") ?: ""
        set(v) = prefs.edit().putString("active", v)
            .putString("recentLive", (if (v.isEmpty()) recentLive else listOf(v) + recentLive.filter { it != v }).take(50).joinToString("\n"))
            .apply()

    // The categories by recent use, the live one first - the order of the smart queue.
    var recentLive: List<String>
        get() = (prefs.getString("recentLive", "") ?: "").split('\n').filter { it.isNotBlank() }
        set(v) = prefs.edit().putString("recentLive", v.take(50).joinToString("\n")).apply()

    var rules: List<Rule>
        get() = objects("rules").map {
            Rule(it.optBoolean("on", true), it.optBoolean("byApp"), it.optString("shown"), it.optString("match"), it.optString("category"),
                 if (it.has("profile")) it.getLong("profile") else null, it.optBoolean("byWord"))
        }
        set(v) = put("rules", v.map { r ->
            JSONObject().put("on", r.on).put("byApp", r.byApp).put("shown", r.shown).put("match", r.match).put("category", r.category)
                .also { o -> r.profile?.let { o.put("profile", it) }; if (r.byWord) o.put("byWord", true) }
        })

    var rulesOn by flag("rulesOn", true)
    var cleanOn by flag("cleanOn", true)
    var unwrapOn by flag("unwrapOn", true)
    var logOn by flag("logOn", true)
    // The setup screen has been seen (Finish or Skip). Until then the app opens on it; afterwards
    // only while LinkPilot is not the default browser - as on Windows.
    var setupDone by flag("setupDone", false)
    // Copied links cleaned by themselves (CopyWatch.kt) - off until turned on.
    var cleanCopiedAuto by flag("cleanCopiedAuto", false)
    // Next by recent use, like Alt+Tab (NextQueue) - on to begin with. Off: in list order.
    var smartQueue by flag("smartQueue", true)
    // Ask GitHub for a newer version when the app opens or you come back to it (Updates.kt) - on to begin with.
    var checkUpdates by flag("checkUpdates", true)

    // The icon a Quick Settings tile shows ("work", "home"...), or null for its own.
    fun tileIcon(tile: String): String? = prefs.getString("tileIcon.$tile", null)
    fun setTileIcon(tile: String, name: String) = prefs.edit().putString("tileIcon.$tile", name).apply()

    var partsOff: Set<String>
        get() = prefs.getStringSet("partsOff", emptySet())!!.toSet()
        set(v) = prefs.edit().putStringSet("partsOff", v).apply()
    var redirectsOff: Set<String>
        get() = prefs.getStringSet("redirectsOff", emptySet())!!.toSet()
        set(v) = prefs.edit().putStringSet("redirectsOff", v).apply()

    // Apps that sent links lately, newest first - offered first when making an app rule. Each is a
    // package name, or "package@serial" for an app in the work profile (Profiles.appKey).
    var recentApps: List<String>
        get() = (prefs.getString("recentApps", "") ?: "").split('\n').filter { it.isNotBlank() }
        set(v) = prefs.edit().putString("recentApps", v.take(20).joinToString("\n")).apply()

    fun cleanOptions() = Cleaner.Options(cleanOn, unwrapOn, partsOff, redirectsOff)

    // The live category; the first one if the live one was removed.
    fun live(): Category? = categories.let { all -> all.firstOrNull { it.name == active } ?: all.firstOrNull() }

    // Makes the next (or, by -1, the previous) category live, and returns it. In list order, or with
    // the smart queue by recent use (NextQueue). A run of taps is kept in the settings, not in memory:
    // each tap may come to a tile or widget Android has only just started.
    fun step(by: Int): Category? {
        val all = categories
        if (all.isEmpty()) return null
        val names = all.map { it.name }
        val now = live()?.name ?: ""
        val time = System.currentTimeMillis()
        val going = time - prefs.getLong("runAt", 0) in 0 until NextQueue.RUN_GAP_MS && prefs.getString("runLanded", null) == now
        val run = if (going) (prefs.getString("run", "") ?: "").split('\n').filter { it.isNotBlank() }
                  else NextQueue.order(names, recentLive, now)
        val order = if (smartQueue) NextQueue.order(names, run, "") else names
        val to = NextQueue.step(order, now, by) ?: return null
        if (smartQueue) {
            prefs.edit().putString("run", run.joinToString("\n")).putLong("runAt", time).putString("runLanded", to).apply()
            recentLive = NextQueue.landed(order, to)
        }
        active = to
        return all.first { it.name == to }
    }

    fun rememberApp(pkg: String) { recentApps = listOf(pkg) + recentApps.filter { it != pkg } }

    // ---- the link log: one line per link, the newest 300 ----

    fun addLog(from: String, openedIn: String, why: String, asked: String, opened: String, changes: String) {
        if (!logOn) return
        try {
            val time = SimpleDateFormat("yyyy-MM-dd HH:mm", Locale.ROOT).format(Date())
            val line = listOf(time, from, openedIn, why, asked, if (opened == asked) "" else opened, changes)
                .joinToString("\t") { it.replace('\t', ' ').replace('\n', ' ').replace('\r', ' ') }
            logFile.appendText(line + "\n")
            val lines = logFile.readLines()
            if (lines.size > 400) logFile.writeText(lines.takeLast(300).joinToString("\n", postfix = "\n"))
        } catch (_: Exception) {
        }
    }

    // Newest first.
    fun readLog(): List<LogEntry> = try {
        logFile.readLines().mapNotNull { line ->
            val b = line.split('\t')
            if (b.size < 7) null
            else LogEntry(b[0], b[1], b[2], b[3], b[4], b[5].ifEmpty { b[4] }, b[6])
        }.reversed()
    } catch (_: Exception) {
        emptyList()
    }

    fun clearLog() { logFile.delete() }

    // ---- helpers ----

    private fun objects(key: String): List<JSONObject> {
        val a = try { JSONArray(prefs.getString(key, "[]")) } catch (_: Exception) { JSONArray() }
        return (0 until a.length()).mapNotNull { a.optJSONObject(it) }
    }

    private fun put(key: String, list: List<JSONObject>) {
        val a = JSONArray()
        list.forEach { a.put(it) }
        prefs.edit().putString(key, a.toString()).apply()
    }

    private fun flag(key: String, default: Boolean) = object : kotlin.properties.ReadWriteProperty<Any?, Boolean> {
        override fun getValue(thisRef: Any?, property: kotlin.reflect.KProperty<*>) = prefs.getBoolean(key, default)
        override fun setValue(thisRef: Any?, property: kotlin.reflect.KProperty<*>, value: Boolean) =
            prefs.edit().putBoolean(key, value).apply()
    }
}

// The smart queue: Next by recent use, like Alt+Tab. Next goes to the category live before this one,
// so a single tap flips back. Taps in quick succession - each within RUN_GAP_MS of the last, with
// nothing else changing the live category in between - are one run: they walk on along the queue as
// it was when the run began, instead of flipping between the same two. After a run the one it ended
// on comes first and the rest keep their order: walking A, B, C leaves C, A, B. The same as on Windows
// (Tray.Step there).
object NextQueue {
    const val RUN_GAP_MS = 3000L

    // The live one first, then the others by how recently they were live, then any never live yet in
    // list order.
    fun order(names: List<String>, recent: List<String>, live: String): List<String> =
        names.sortedBy { if (it == live) -1 else recent.indexOf(it).let { i -> if (i < 0) Int.MAX_VALUE else i } }

    // The one `by` steps on from the live one along the order, wrapping round; from the start (or
    // the end, going back) when the live one is not in it.
    fun step(order: List<String>, live: String, by: Int): String? {
        if (order.isEmpty()) return null
        val at = order.indexOf(live)
        return if (at < 0) (if (by > 0) order.first() else order.last()) else order[Math.floorMod(at + by, order.size)]
    }

    // The recent order once a step landed on `to`: it first, the rest as they were.
    fun landed(order: List<String>, to: String): List<String> = listOf(to) + order.filter { it != to }
}
