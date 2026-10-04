package com.husarp.linkpilot

import com.husarp.linkpilot.UpdateLogic.Asset
import com.husarp.linkpilot.UpdateLogic.Release
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

// The updater's pure logic (UpdateLogic in Updates.kt): versions, which APK, how often GitHub is asked,
// and the banner's ✕.
class UpdateLogicTest {
    @Test fun comparesAsNumbersNotText() {
        assertTrue(UpdateLogic.isNewer("0.10.0", "0.9.9"))
        assertTrue(UpdateLogic.isNewer("0.10.1", "0.10.0"))
        assertTrue(UpdateLogic.isNewer("1.0.0", "0.10.0"))
        assertFalse(UpdateLogic.isNewer("0.9.9", "0.10.0"))
        assertFalse(UpdateLogic.isNewer("0.10.0", "0.10.0"))
        // a leading v, a -suffix and missing parts don't matter
        assertTrue(UpdateLogic.isNewer("v0.11.0", "0.10.0"))
        assertEquals(0, UpdateLogic.compare("0.11.0-beta", "0.11.0"))
        assertEquals(0, UpdateLogic.compare("1.0", "1.0.0"))
        assertTrue(UpdateLogic.isNewer("1.0.1", "1.0"))
    }

    @Test fun readsTheVersionFromTheApkName() {
        assertEquals("0.10.0", UpdateLogic.apkVersion("LinkPilot-Android-0.10.0.apk"))
        assertEquals("0.10.0", UpdateLogic.apkVersion("linkpilot-android-v0.10.0.APK"))
        assertNull(UpdateLogic.apkVersion("LinkPilotSetup-4.6.0.exe"))
        assertNull(UpdateLogic.apkVersion("LinkPilot-4.6.0.zip"))
        assertNull(UpdateLogic.apkVersion("LinkPilot-Android.apk"))
    }

    @Test fun picksTheApkNotTheWindowsFiles() {
        // a release as it is today: the zip, the program, the Setup and the APK - the tag is Windows' version
        val files = listOf(
            Asset("LinkPilot-4.6.0.zip", "zip", 1),
            Asset("BrowserSwitch.exe", "exe", 1),
            Asset("LinkPilotSetup-4.6.0.exe", "setup", 1),
            Asset("LinkPilot-Android-0.10.0.apk", "apk", 2))
        val (a, v) = UpdateLogic.pickApk(files)!!
        assertEquals("apk", a.url); assertEquals("0.10.0", v)
        assertEquals("0.10.1", UpdateLogic.pickApk(files + Asset("LinkPilot-Android-0.10.1.apk", "apk2", 2))!!.second)
        assertNull(UpdateLogic.pickApk(files.take(3)))
    }

    @Test fun aWindowsOnlyReleaseOnTopDoesNotHideThePhoneUpdate() {
        // newest first, as GitHub lists them
        val win47 = Release("v4.7.0", listOf(Asset("LinkPilotSetup-4.7.0.exe", "s47", 1), Asset("BrowserSwitch.exe", "b47", 1)))
        val both46 = Release("v4.6.0", listOf(Asset("LinkPilotSetup-4.6.0.exe", "s46", 1), Asset("LinkPilot-Android-0.11.0.apk", "a11", 2)))
        val both45 = Release("v4.5.0", listOf(Asset("LinkPilot-Android-0.10.0.apk", "a10", 2)))
        val (rel, apk, v) = UpdateLogic.newestApk(listOf(win47, both46, both45))!!
        assertEquals("v4.6.0", rel.page); assertEquals("a11", apk.url); assertEquals("0.11.0", v)
        // drafts and pre-releases don't count
        val draft = Release("d", listOf(Asset("LinkPilot-Android-0.12.0.apk", "d", 2)), draft = true)
        val pre = Release("p", listOf(Asset("LinkPilot-Android-0.13.0.apk", "p", 2)), prerelease = true)
        assertEquals("0.11.0", UpdateLogic.newestApk(listOf(draft, pre, win47, both46, both45))!!.third)
        // the highest version wins, not the release on top
        assertEquals("0.11.0", UpdateLogic.newestApk(listOf(both45, both46))!!.third)
        // no APK anywhere: no update for the phone
        assertNull(UpdateLogic.newestApk(listOf(win47)))
        assertNull(UpdateLogic.newestApk(emptyList()))
    }

    @Test fun asksGithubAtMostEveryFiveMinutes() {
        val t = 1_000_000_000L
        assertTrue(UpdateLogic.mayCheck(t, 0))                      // never asked yet
        assertFalse(UpdateLogic.mayCheck(t + 60_000, t))            // 1 min later
        assertFalse(UpdateLogic.mayCheck(t + 299_999, t))           // 4:59.999
        assertTrue(UpdateLogic.mayCheck(t + 300_000, t))            // 5:00
        assertTrue(UpdateLogic.mayCheck(t - 1, t))                  // the clock went back: don't get stuck
    }

    @Test fun closingTheBannerHidesItUntilTheNextStart() {
        val g = UpdateLogic.BannerGate()
        assertTrue(g.visible("0.11.0", "0.10.0"))
        assertFalse(g.visible("0.10.0", "0.10.0"))                  // not newer
        assertFalse(g.visible(null, "0.10.0"))                      // nothing found
        g.dismiss()
        assertFalse(g.visible("0.11.0", "0.10.0"))                  // coming back from another app: still hidden
        g.onAppStart()
        assertTrue(g.visible("0.11.0", "0.10.0"))                   // a fresh start: back again, never hidden for good
    }
}
