package com.husarp.linkpilot

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Assume.assumeTrue
import org.junit.Test

// The hints while typing a rule (Sites): only what starts with what you typed, your own first, at most 8.
class SitesTest {
    private val none = emptyList<Pair<String, Int>>()

    @Test fun reNeverShowsYoutube() {
        val hints = Sites.suggest("Re", none)
        assertTrue(hints.contains("reddit.com"))
        assertFalse(hints.any { it.contains("youtube") })
    }

    @Test fun oneLetterShowsSitesStartingWithIt() {
        val hints = Sites.suggest("w", none)
        assertTrue(hints.size >= 4)
        assertTrue(hints.all { it.startsWith("w") })
        assertEquals("wikipedia.org", hints.first())
    }

    @Test fun wholeNameBeforeALaterPart() {
        val hints = Sites.suggest("goo", none)
        assertEquals("google.com", hints.first())
        assertTrue(hints.contains("docs.google.com"))
        assertTrue(Sites.suggest("you", none).contains("youtube.com"))
        assertEquals("docs.google.com", Sites.suggest("docs", none).first())
    }

    @Test fun notTheEndingNorAPlainPart() {
        assertEquals(listOf("player.pl"), Sites.suggest("pl", none))
        assertFalse(Sites.suggest("co", none).contains("pekao.com.pl"))
        assertFalse(Sites.suggest("gov", none).contains("epuap.gov.pl"))
        assertTrue(Sites.suggest("gov", none).contains("gov.pl"))
    }

    @Test fun typedAddressIsReadLikeAnAddress() {
        assertEquals("youtube.com", Sites.suggest("https://www.YouTube.c", none).first())
    }

    @Test fun yourSitesFirst() {
        val used = listOf("rossmann.pl" to 3, "reddit.com" to 1, "revolut.com" to 7)
        assertEquals(listOf("revolut.com", "rossmann.pl", "reddit.com"), Sites.suggest("r", used).take(3))
        // a later part of a site you use still comes after the popular sites that start with it
        assertEquals("gmail.com", Sites.suggest("gm", listOf("x.gmx.de" to 9)).first())
    }

    @Test fun nothingTypedShowsOnlyYourSites() {
        assertTrue(Sites.suggest("", none).isEmpty())
        val used = (1..10).map { "site$it.com" to 20 - it }
        assertEquals(used.take(8).map { it.first }, Sites.suggest("  ", used))
    }

    @Test fun atMostEight() {
        assertTrue(Sites.suggest("m", none).size <= Sites.MAX)
        assertTrue(Sites.suggest("a", (1..20).map { "a$it.com" to it }).size == Sites.MAX)
    }

    @Test fun yourSitesFromTheLinkLog() {
        val used = Sites.used(listOf("https://www.youtube.com/watch?v=1", "https://m.youtube.com/x", "https://github.com/a",
                                     "not a link", "https://localhost/x"))
        assertEquals(listOf("youtube.com" to 2, "github.com" to 1), used)
    }

    @Test fun popularListHasNoRepeats() {
        assertEquals(Sites.popular.size, Sites.popular.distinct().size)
        assertTrue(Sites.popular.size >= 140)
    }

    // The Windows app (Sites.cs at the top of the project) must suggest the same sites in the same order.
    @Test fun popularListMatchesWindows() {
        val cs = File("../../Sites.cs")
        assumeTrue("Sites.cs not next to the Android project", cs.exists())
        val list = cs.readText().substringAfter("Popular = {").substringBefore("};")
        assertEquals(Sites.popular, Regex("\"([^\"]+)\"").findAll(list).map { it.groupValues[1] }.toList())
    }

    // ---- apps ----

    private val apps = listOf("Brave", "Chrome", "Firefox", "Signal", "Slack", "Spotify", "Visual Studio Code", "Microsoft Teams")
    private fun appHints(typed: String, uses: (String) -> Int = { 0 }) = Sites.apps(typed, apps, { it }, uses)

    @Test fun appsByNameThenByWord() {
        assertEquals(listOf("Visual Studio Code"), appHints("code"))
        assertEquals(listOf("Microsoft Teams"), appHints("TEA"))
        assertEquals(listOf("Signal", "Slack", "Spotify", "Visual Studio Code"), appHints("s"))
        assertTrue(appHints("rom").isEmpty())   // never the middle of a word
        assertTrue(appHints("").isEmpty())
    }

    @Test fun appsYouUseFirst() {
        assertEquals(listOf("Spotify", "Signal", "Slack", "Visual Studio Code"),
                     appHints("s") { if (it == "Spotify") 5 else 0 })
    }

    @Test fun atMostEightApps() {
        val many = (1..20).map { "App $it" }
        assertEquals(Sites.MAX, Sites.apps("app", many, { it }, { 0 }).size)
    }
}
