package com.husarp.linkpilot

// Hints while you type a rule: the sites (address and keyword rules) and the apps (app rules) you
// may be looking for. Only what starts with what you typed: the whole name first ("re" -> reddit.com),
// then a later part of it ("goo" -> docs.google.com, "code" -> Visual Studio Code) - never something
// with the letters in its middle, so "re" never shows youtube.com. Within each, what you use first,
// then the popular ones in the list's order. The same as on Windows.
object Sites {
    const val MAX = 8

    // Widely used sites, the most visited first - the hints before you have opened a link there.
    // MUST MATCH Sites.Popular on Windows (Sites.cs): the same sites in the same order - SitesTest checks it.
    val popular = listOf(
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
        "khanacademy.org", "chess.com", "lichess.org", "store.steampowered.com", "epicgames.com", "roblox.com")

    // Parts of a site's name that say nothing about it - "co" must not bring up every .com.pl site.
    private val plain = setOf("com", "co", "org", "net", "edu", "gov", "ac")

    // The site of a link as the hints show it: "https://www.youtube.com/watch?v=1" -> "youtube.com".
    fun of(url: String): String? = Cleaner.hostOf(url)?.let { trimmed(it) }?.takeIf { it.contains('.') }

    private fun trimmed(host: String) = host.removePrefix("www.").removePrefix("m.")

    // Your sites from the link log (the links you opened), the most used first, with how many links.
    fun used(links: List<String>): List<Pair<String, Int>> =
        links.mapNotNull { of(it) }.groupingBy { it }.eachCount().toList().sortedByDescending { it.second }

    // The sites to hint for what you typed: nothing typed - your own sites; otherwise yours and the
    // popular ones that start with it (whole name, then a later part), at most MAX.
    fun suggest(typed: String, used: List<Pair<String, Int>>): List<String> {
        val t = Router.cleanAddress(typed).lowercase().removePrefix("www.")
        if (t.isEmpty()) return used.take(MAX).map { it.first }
        val count = used.toMap()
        return (used.map { it.first } + popular).distinct()
            .mapNotNull { s -> siteTier(t, s)?.let { tier -> s to tier } }
            .sortedWith(compareBy<Pair<String, Int>>({ it.second }, { -(count[it.first] ?: 0) },
                                                     { popular.indexOf(it.first).let { i -> if (i < 0) Int.MAX_VALUE else i } }))
            .take(MAX).map { it.first }
    }

    // 0: the site starts with it; 1: a later part of it does ("goo" -> docs.google.com, "google.com" ->
    // mail.google.com) - not its ending (.pl) nor a plain part (com, gov); null: neither.
    private fun siteTier(t: String, site: String): Int? {
        if (site.startsWith(t)) return 0
        val labels = site.split('.')
        for (i in 1 until labels.size - 1)
            if (labels[i] !in plain && labels.drop(i).joinToString(".").startsWith(t)) return 1
        return null
    }

    // The apps to hint for what you typed, at most limit: those whose name starts with it, then those
    // with a later word that does ("code" -> Visual Studio Code); within each the most used first
    // (uses: bigger is more), then in the list's order. Nothing typed - none.
    fun <T> apps(typed: String, all: List<T>, name: (T) -> String, uses: (T) -> Int, limit: Int = MAX): List<T> {
        val t = typed.trim()
        if (t.isEmpty()) return emptyList()
        return all.mapNotNull { a -> appTier(t, name(a))?.let { tier -> a to tier } }
            .sortedWith(compareBy({ it.second }, { -uses(it.first) }))
            .take(limit).map { it.first }
    }

    private fun appTier(t: String, name: String): Int? {
        if (name.startsWith(t, ignoreCase = true)) return 0
        for (i in 1 until name.length)
            if (!name[i - 1].isLetterOrDigit() && name[i].isLetterOrDigit() && name.startsWith(t, i, ignoreCase = true)) return 1
        return null
    }
}
