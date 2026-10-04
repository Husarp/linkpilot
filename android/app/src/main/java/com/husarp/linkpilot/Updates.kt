package com.husarp.linkpilot

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Handler
import android.os.Looper
import android.provider.Settings
import android.widget.Toast
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.core.content.FileProvider
import org.json.JSONArray
import java.io.File
import java.io.IOException
import java.lang.ref.WeakReference
import java.net.HttpURLConnection
import java.net.SocketTimeoutException
import java.net.URL
import java.net.UnknownHostException
import java.util.Locale
import java.util.concurrent.Executors
import javax.net.ssl.SSLException

// The in-app updater (APP-STANDARDS sections 2 and 3), the same as Lockdown Mobile's. It asks GitHub
// for the recent releases when the app opens and every time you come back to it (at most every 5
// minutes), finds the APK by its ".apk" ending, downloads it over the app's own connection into the
// cache with the progress shown in the app, and hands it to Android's installer. The APK is deleted
// once the new version runs.
//
// Only an APK signed with LinkPilot's own release key (~/.keystores) installs over this app.

// The pure logic (no Android), unit-tested in UpdateLogicTest: versions compared as numbers, the APK
// found by its ".apk" ending, GitHub asked at most every 5 minutes, and a banner ✕ that hides it
// until the next start.
object UpdateLogic {
    const val MIN_CHECK_GAP_MS = 5 * 60_000L

    // A release file as GitHub lists it.
    data class Asset(val name: String, val url: String, val size: Long)

    // Compares two versions number by number ("0.10.0" > "0.9.9"); a leading "v" and any "-suffix" are ignored.
    fun compare(a: String, b: String): Int {
        val x = parts(a); val y = parts(b)
        for (i in 0 until maxOf(x.size, y.size)) {
            val c = x.getOrElse(i) { 0 }.compareTo(y.getOrElse(i) { 0 })
            if (c != 0) return c
        }
        return 0
    }

    fun isNewer(candidate: String, current: String): Boolean = compare(candidate, current) > 0

    private fun parts(v: String): List<Int> =
        v.trim().removePrefix("v").removePrefix("V").substringBefore('-').substringBefore('+')
            .split('.').map { p -> p.takeWhile { it.isDigit() }.toIntOrNull() ?: 0 }

    // The version in an APK's file name ("LinkPilot-Android-0.10.0.apk" -> "0.10.0"), or null if it has none.
    fun apkVersion(name: String): String? {
        if (!name.endsWith(".apk", ignoreCase = true)) return null
        return Regex("""\d+(?:\.\d+)+""").findAll(name.dropLast(4)).lastOrNull()?.value
    }

    // The Android update among a release's files: the .apk files (found by their ending, never an exact
    // name) that carry a version, the highest one. The release tag is the Windows version, so it is not used.
    fun pickApk(assets: List<Asset>): Pair<Asset, String>? =
        assets.mapNotNull { a -> apkVersion(a.name)?.let { a to it } }
            .maxWithOrNull { p, q -> compare(p.second, q.second) }

    // A GitHub release: its page and files.
    data class Release(val page: String, val assets: List<Asset>, val draft: Boolean = false, val prerelease: Boolean = false)

    // The newest Android update across the recent releases: Windows and Android share the repo, so a
    // Windows-only release (no .apk) must not hide the phone's update on the one before it. Drafts and
    // pre-releases don't count. (release, apk, version), or null when none of them carries an APK.
    fun newestApk(releases: List<Release>): Triple<Release, Asset, String>? =
        releases.filter { !it.draft && !it.prerelease }
            .mapNotNull { r -> pickApk(r.assets)?.let { (a, v) -> Triple(r, a, v) } }
            .maxWithOrNull { p, q -> compare(p.third, q.third) }

    // True when GitHub may be asked again (lastMs = time of the last ask, 0 = never).
    fun mayCheck(nowMs: Long, lastMs: Long, gapMs: Long = MIN_CHECK_GAP_MS): Boolean =
        lastMs == 0L || nowMs - lastMs >= gapMs || nowMs < lastMs

    // The banner's ✕: hides it until the app is next started (not when coming back from another app).
    class BannerGate {
        var dismissed = false
            private set

        fun dismiss() { dismissed = true }

        // A fresh start of the app's screen (not a return, not a rotation): the banner may show again.
        fun onAppStart() { dismissed = false }

        fun visible(latest: String?, current: String): Boolean =
            !dismissed && latest != null && isNewer(latest, current)
    }
}

object Updates {
    // Recent releases, not /releases/latest: a Windows-only release on top would hide the phone's update.
    private const val API = "https://api.github.com/repos/Husarp/linkpilot/releases?per_page=20"
    const val RELEASES_PAGE = "https://github.com/Husarp/linkpilot/releases"
    // It downloads and hands to Android only what comes from here.
    private const val DOWNLOADS = "https://github.com/Husarp/linkpilot/releases/download/"
    private const val TIMEOUT_MS = 10_000

    // The newest Android version on GitHub; page is its release page.
    data class Latest(val version: String, val apkName: String, val apkUrl: String, val apkSize: Long, val page: String)

    sealed interface Phase {
        data object Idle : Phase
        data class Downloading(val percent: Int, val bytes: Long) : Phase   // percent -1: the size is not known
        data object NeedsPermission : Phase      // Android's "install unknown apps" screen is open
        data object Ready : Phase                // downloaded while the app was in the background; installs on return
        data object Installing : Phase           // handed to Android's installer
        data class Failed(val why: String) : Phase
    }

    data class State(
        val latest: Latest? = null,              // newest version found on GitHub (may be the one you have)
        val pageUrl: String = RELEASES_PAGE,     // the page of the release with the newest APK, for GITHUB
        val checking: Boolean = false,
        val message: String? = null,             // the answer to CHECK NOW
        val bannerHidden: Boolean = false,       // mirrors the gate, so the screen redraws when ✕ is tapped
        val phase: Phase = Phase.Idle,
    )

    // Written on the main thread only; the work runs on its own threads (a check stuck in DNS must not
    // hold up the next one or a download).
    var state by mutableStateOf(State()); private set
    private val main = Handler(Looper.getMainLooper())
    private val work = Executors.newCachedThreadPool()
    private val gate = UpdateLogic.BannerGate()
    private var lastCheckMs = 0L
    private var asking = 0                       // which check is the current one; a late answer is dropped
    private var pendingApk: File? = null
    // The app's screen while it is in front: Android's screens are opened from it (same task, so Back
    // returns here), and never while the app is in the background (Android 10+ silently blocks that).
    private var front: WeakReference<Activity>? = null

    private fun set(f: (State) -> State) {
        if (Looper.myLooper() == Looper.getMainLooper()) state = f(state) else main.post { state = f(state) }
    }

    fun current(ctx: Context): String =
        runCatching { ctx.packageManager.getPackageInfo(ctx.packageName, 0).versionName ?: "?" }.getOrDefault("?")

    fun bannerVisible(s: State, current: String): Boolean = !s.bannerHidden && gate.visible(s.latest?.version, current)

    // Whether Home has the banner now (never in the work-profile copy).
    fun showBanner(ctx: Context): Boolean = bannerVisible(state, current(ctx)) && !Profiles.isWorkCopy(ctx)

    fun available(s: State, current: String): Boolean = s.latest != null && UpdateLogic.isNewer(s.latest.version, current)

    // A fresh start of the app's screen (not a return, not a rotation): the banner may show again, old
    // APKs go. Not the process start: every link click starts LinkPilot's process and keeps it alive.
    fun onAppStart(ctx: Context) {
        val app = ctx.applicationContext
        gate.onAppStart()
        marker(app).delete()   // a fresh start is not a return from the "install unknown apps" screen
        // an old failure, or an installer left without installing, doesn't carry over to a fresh start
        set { it.copy(bannerHidden = false, phase = if (it.phase is Phase.Failed || it.phase == Phase.Installing) Phase.Idle else it.phase) }
        if (state.phase is Phase.Downloading) return   // the process outlived the screen mid-download
        val cur = current(app)
        work.execute {
            dir(app).listFiles()?.forEach { f ->
                val v = UpdateLogic.apkVersion(f.name)
                if (v == null || !UpdateLogic.isNewer(v, cur)) f.delete()   // also drops unfinished .part files
            }
        }
    }

    // Every time the app comes to the front: carry on an install waiting on the permission, then check.
    fun onResume(activity: Activity) {
        front = WeakReference(activity)
        val app = activity.applicationContext
        val phase = state.phase
        // Some phones restart the app when "install unknown apps" is switched on; the marker file survives that.
        val waiting = phase == Phase.NeedsPermission || (phase == Phase.Idle && marker(app).exists())
        when {
            waiting -> {
                val apk = pendingApk ?: runCatching { File(dir(app), marker(app).readText().trim()) }.getOrNull()
                marker(app).delete()
                if (canInstall(app) && apk != null && apk.isFile) install(app, apk)
                else fail("LinkPilot isn't allowed to install apps yet. Allow \"Install unknown apps\" for LinkPilot, then TRY AGAIN.")
            }
            phase == Phase.Ready -> pendingApk?.takeIf { it.isFile }?.let { install(app, it) } ?: fail("The downloaded update is gone.")
            phase == Phase.Installing -> fail("The update wasn't installed. If Android said it couldn't install it, the update " +
                "was signed with a different key than this app - get it from GitHub.")
        }
        autoCheck(app)
    }

    fun onPause() { front = null }

    fun dismissBanner() { gate.dismiss(); set { it.copy(bannerHidden = true) } }

    // The automatic check: only when switched on, at most every 5 minutes, silent when it fails. Never
    // in the work-profile copy - installing the update in the personal profile updates both.
    fun autoCheck(ctx: Context) {
        val app = ctx.applicationContext
        if (!Store(app).checkUpdates || state.checking || Profiles.isWorkCopy(app)) return
        if (!UpdateLogic.mayCheck(System.currentTimeMillis(), lastCheckMs)) return
        check(app, manual = false)
    }

    // CHECK NOW: gives up after 10 seconds and says in words what is likely wrong.
    fun checkNow(ctx: Context) {
        if (state.checking) return
        check(ctx.applicationContext, manual = true)
    }

    private fun check(app: Context, manual: Boolean) {
        val before = lastCheckMs
        lastCheckMs = System.currentTimeMillis()
        val ask = ++asking
        set { it.copy(checking = true, message = if (manual) "Checking…" else it.message) }
        val cur = current(app)
        // The 10-second limit is kept here, not by the request, so it holds even if the socket hangs in DNS.
        main.postDelayed({ if (ask == asking) answered(ask, manual, cur, Result.failure(SocketTimeoutException()), before) }, TIMEOUT_MS.toLong())
        work.execute {
            val res = runCatching { fetchLatest() }
            main.post { if (ask == asking) answered(ask, manual, cur, res, before) }
        }
    }

    // Main thread. A check that never reached GitHub doesn't count toward the 5 minutes (it cost GitHub nothing).
    private fun answered(ask: Int, manual: Boolean, cur: String, res: Result<Latest?>, before: Long) {
        asking = ask + 1   // any later answer to this check is dropped
        res.onSuccess { latest ->
            set {
                it.copy(checking = false, latest = latest, pageUrl = latest?.page ?: RELEASES_PAGE,
                    message = if (!manual) it.message
                    else if (latest != null && UpdateLogic.isNewer(latest.version, cur)) "LinkPilot ${latest.version} is available."
                    else "You're up to date ($cur).")
            }
        }.onFailure { e ->
            if (e !is HttpStatus) lastCheckMs = before
            set { it.copy(checking = false, message = if (manual) describe(e, download = false) else it.message) }
        }
    }

    private class HttpStatus(val code: Int) : IOException("HTTP $code")

    // The newest APK among the recent releases (null when none has one). Blocking; off the main thread.
    private fun fetchLatest(): Latest? {
        val c = open(API).apply { setRequestProperty("Accept", "application/vnd.github+json") }
        try {
            if (c.responseCode != 200) throw HttpStatus(c.responseCode)
            val list = JSONArray(c.inputStream.bufferedReader().use { it.readText() })
            val releases = (0 until list.length()).map { i ->
                val j = list.getJSONObject(i)
                val arr = j.optJSONArray("assets")
                val assets = (0 until (arr?.length() ?: 0)).map { k ->
                    val a = arr!!.getJSONObject(k)
                    UpdateLogic.Asset(a.getString("name"), a.getString("browser_download_url"), a.optLong("size", -1))
                }
                UpdateLogic.Release(j.optString("html_url", RELEASES_PAGE), assets, j.optBoolean("draft"), j.optBoolean("prerelease"))
            }
            val (rel, a, v) = UpdateLogic.newestApk(releases) ?: return null
            return Latest(v, a.name, a.url, a.size, rel.page)
        } finally { c.disconnect() }
    }

    private fun open(url: String) = (URL(url).openConnection() as HttpURLConnection).apply {
        connectTimeout = TIMEOUT_MS; readTimeout = TIMEOUT_MS
        instanceFollowRedirects = true   // GitHub sends the download on to its file server
        setRequestProperty("User-Agent", "LinkPilot-Android")
    }

    // UPDATE / GET UPDATE / TRY AGAIN: download (unless it is already here), then install.
    fun update(ctx: Context) {
        val app = ctx.applicationContext
        if (state.phase is Phase.Downloading || Profiles.isWorkCopy(app)) return   // installed from the personal profile
        val latest = state.latest ?: run {
            // e.g. TRY AGAIN after Android restarted the app: use the file already here, else look again
            pendingApk?.takeIf { it.isFile }?.let { install(app, it) } ?: checkNow(app)
            return
        }
        if (!latest.apkUrl.startsWith(DOWNLOADS)) { fail("The update isn't on LinkPilot's GitHub page."); return }
        val file = File(dir(app), latest.apkName.substringAfterLast('/'))
        if (file.exists() && (latest.apkSize <= 0 || file.length() == latest.apkSize)) { install(app, file); return }
        set { it.copy(phase = Phase.Downloading(if (latest.apkSize > 0) 0 else -1, 0)) }
        work.execute {
            runCatching { download(latest, file) }
                .onSuccess { main.post { install(app, file) } }
                .onFailure { e -> main.post { fail(describe(e, download = true)) } }
        }
    }

    private fun download(latest: Latest, file: File) {
        val part = File(file.parentFile, file.name + ".part")
        val c = open(latest.apkUrl)
        try {
            if (c.responseCode != 200) throw HttpStatus(c.responseCode)
            val total = c.contentLengthLong.takeIf { it > 0 } ?: latest.apkSize
            var done = 0L; var shown = -2L
            c.inputStream.use { inp ->
                part.outputStream().use { out ->
                    val buf = ByteArray(64 * 1024)
                    while (true) {
                        val n = inp.read(buf); if (n < 0) break
                        out.write(buf, 0, n); done += n
                        // the percent when the size is known, else every 100 KB - the progress never sits still
                        val pct = if (total > 0) (done * 100 / total).toInt().coerceIn(0, 100) else -1
                        val step = if (total > 0) pct.toLong() else done / 100_000
                        if (step != shown) { shown = step; val d = done; set { it.copy(phase = Phase.Downloading(pct, d)) } }
                    }
                }
            }
            if (total > 0 && done != total) throw IOException("incomplete")
            if (!part.renameTo(file)) throw IOException("rename")
        } catch (e: Throwable) {
            part.delete(); throw e
        } finally { c.disconnect() }
    }

    private fun canInstall(ctx: Context) = ctx.packageManager.canRequestPackageInstalls()

    // Hands the APK to Android's installer; the first time, opens "install unknown apps" for LinkPilot
    // instead. Main thread only. With the app in the background it waits (Ready) and goes on in onResume.
    private fun install(app: Context, apk: File) {
        pendingApk = apk
        val act = front?.get()
        if (act == null) { set { it.copy(phase = Phase.Ready) }; return }
        if (!canInstall(app)) {
            set { it.copy(phase = Phase.NeedsPermission) }
            runCatching { marker(app).writeText(apk.name) }
            runCatching {
                act.startActivity(Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:${app.packageName}")))
            }.onFailure {
                marker(app).delete()
                fail("Couldn't open Android's \"Install unknown apps\" setting. Allow it for LinkPilot in Settings, then TRY AGAIN.")
            }
            return
        }
        runCatching {
            val uri = FileProvider.getUriForFile(app, "${app.packageName}.files", apk)
            act.startActivity(Intent(Intent.ACTION_VIEW).setDataAndType(uri, "application/vnd.android.package-archive")
                .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION))
            set { it.copy(phase = Phase.Installing) }
        }.onFailure { fail("Couldn't open Android's installer.") }
    }

    private fun fail(why: String) = set { it.copy(phase = Phase.Failed(why)) }

    private fun dir(ctx: Context) = File(ctx.cacheDir, "updates").apply { mkdirs() }

    // Names the APK waiting on "install unknown apps" (no version in its name, so the start-up cleanup drops it).
    private fun marker(ctx: Context) = File(dir(ctx), "pending-install")

    // What went wrong, in words (never "Failed to fetch").
    private fun describe(e: Throwable, download: Boolean): String = when {
        e is UnknownHostException -> "Can't reach GitHub. Check that the phone is online - or a firewall (like AFWall) may be blocking LinkPilot."
        e is SocketTimeoutException -> "GitHub didn't answer within 10 seconds. The connection may be slow or blocked."
        e is SSLException -> "Couldn't open a secure connection to GitHub. The network may be intercepting it, or the phone's date is wrong."
        e is HttpStatus && (e.code == 403 || e.code == 429) -> "GitHub's limit of checks per hour was reached. Try again in a while."
        e is HttpStatus && e.code == 404 -> if (download) "The update file is no longer on GitHub." else "No release found on GitHub."
        download && e.message == "incomplete" -> "The download stopped before it finished."
        download && (e.message == "rename" || e.message?.contains("ENOSPC") == true) -> "Couldn't save the update - is the phone's storage full?"
        download -> "The download failed - the connection to GitHub dropped."
        else -> "Couldn't check for updates - the connection to GitHub failed."
    }
}

// ---- on the screen ---------------------------------------------------------------------------------

// GitHub's page, in your browser - through LinkPilot's own link routing, like any link.
private fun openPage(ctx: Context, url: String) {
    try { ctx.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) }
    catch (_: Exception) { Toast.makeText(ctx, "No browser to open GitHub in", Toast.LENGTH_SHORT).show() }
}

// Home's banner: the new version, UPDATE and ✕ (hides it until the app is next started). Not in the
// work-profile copy.
@Composable
fun UpdateBanner() {
    val ctx = LocalContext.current
    val s = Updates.state
    val latest = s.latest
    if (latest == null || !Updates.showBanner(ctx)) return
    Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.secondaryContainer)) {
        Column(Modifier.fillMaxWidth().padding(start = 16.dp, top = 8.dp, bottom = 8.dp, end = 4.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Column(Modifier.weight(1f)) {
                    Text("Update available", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                    Text("LinkPilot ${latest.version}", style = MaterialTheme.typography.bodyMedium)
                }
                Button(onClick = { Updates.update(ctx) }, enabled = s.phase !is Updates.Phase.Downloading) { Text("UPDATE") }
                IconButton(onClick = { Updates.dismissBanner() }) { Icon(Icons.Default.Close, "Close") }
            }
            UpdateProgress(s, Modifier.padding(end = 12.dp))
        }
    }
}

// Home › Updates: the automatic check on/off, your version + CHECK NOW, GITHUB, GET UPDATE.
@Composable
fun UpdateSection(m: Model) {
    val ctx = LocalContext.current
    m.tick
    val s = Updates.state
    val cur = remember { Updates.current(ctx) }
    val workCopy = remember { Profiles.isWorkCopy(ctx) }
    val muted = MaterialTheme.colorScheme.onSurfaceVariant
    SectionCard("Updates", "New versions come from GitHub") {
        SwitchRow("Check for updates", "When the app opens or you come back to it - at most every 5 minutes.", m.store.checkUpdates) { on ->
            m.store.checkUpdates = on; m.changed()
            if (on) Updates.autoCheck(ctx)
        }
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            Text("Version $cur", style = MaterialTheme.typography.bodyMedium, modifier = Modifier.weight(1f))
            TextButton(onClick = { Updates.checkNow(ctx) }, enabled = !s.checking) { Text("CHECK NOW") }
        }
        s.message?.let { Text(it, style = MaterialTheme.typography.bodyMedium) }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            OutlinedButton(onClick = { openPage(ctx, Updates.RELEASES_PAGE) }) { Text("GITHUB") }
            if (Updates.available(s, cur) && !workCopy) {
                Button(onClick = { Updates.update(ctx) }, enabled = s.phase !is Updates.Phase.Downloading) { Text("GET UPDATE") }
            }
        }
        // a work profile often may not install apps at all - the personal copy's update covers this one
        if (Updates.available(s, cur) && workCopy)
            Text("Update LinkPilot in your personal profile - that updates this copy too.", style = MaterialTheme.typography.bodyMedium)
        UpdateProgress(s)
        Text("Android asks you to confirm every update, and only an update signed with LinkPilot's own key installs.",
             style = MaterialTheme.typography.bodySmall, color = muted)
    }
}

// The download's progress, the steps waiting on Android, and a failed update (TRY AGAIN + GITHUB).
@Composable
private fun UpdateProgress(s: Updates.State, modifier: Modifier = Modifier) {
    val ctx = LocalContext.current
    val small = MaterialTheme.typography.bodySmall
    Column(modifier.fillMaxWidth()) {
        when (val p = s.phase) {
            Updates.Phase.Idle -> {}
            is Updates.Phase.Downloading -> {
                if (p.percent >= 0) {
                    Text("Downloading… ${p.percent}%", style = small, modifier = Modifier.padding(top = 8.dp, bottom = 4.dp))
                    LinearProgressIndicator(progress = { p.percent / 100f }, modifier = Modifier.fillMaxWidth().height(6.dp))
                } else {
                    Text("Downloading… " + String.format(Locale.ROOT, "%.1f MB", p.bytes / 1_000_000.0), style = small,
                         modifier = Modifier.padding(top = 8.dp, bottom = 4.dp))
                    LinearProgressIndicator(modifier = Modifier.fillMaxWidth().height(6.dp))
                }
            }
            Updates.Phase.NeedsPermission -> Text(
                "Allow \"Install unknown apps\" for LinkPilot, then come back - the update carries on by itself.",
                style = small, modifier = Modifier.padding(top = 8.dp))
            Updates.Phase.Ready -> Text("Downloaded - Android's installer opens now.", style = small, modifier = Modifier.padding(top = 8.dp))
            Updates.Phase.Installing -> Text("Confirm the update in Android's window.", style = small, modifier = Modifier.padding(top = 8.dp))
            is Updates.Phase.Failed -> {
                Text("Update failed. ${p.why}", style = small, color = MaterialTheme.colorScheme.error, modifier = Modifier.padding(top = 8.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    TextButton(onClick = { Updates.update(ctx) }) { Text("TRY AGAIN") }
                    TextButton(onClick = { openPage(ctx, s.pageUrl) }) { Text("GITHUB") }
                }
            }
        }
    }
}
