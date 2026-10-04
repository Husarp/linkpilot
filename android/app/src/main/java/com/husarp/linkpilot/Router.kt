package com.husarp.linkpilot

import android.content.Context
import java.io.ByteArrayOutputStream
import java.nio.ByteBuffer

// Where a link goes: the first rule that matches (rules win over the live category), otherwise the
// live category. A rule whose category is gone, or whose browser is not installed, is passed over,
// so a link is never sent nowhere.
object Router {
    class Choice(val category: Category?, val why: String)

    // fromProfile: the work profile's serial number when the link was tapped in an app there (and
    // passed here by LinkPilot over there); null for an app in this profile. Signal here and Signal
    // in the work profile are two different apps to a rule.
    fun decide(ctx: Context, store: Store, url: String, fromApp: String?, fromProfile: Long? = null): Choice {
        val cats = store.categories
        if (store.rulesOn) for (r in store.rules) {
            if (!r.on) continue
            val c = cats.firstOrNull { it.name.equals(r.category, ignoreCase = true) } ?: continue
            if (!Browsers.installed(ctx, c)) continue
            val hit = if (r.byApp) fromApp != null && r.profile == fromProfile &&
                                   r.match.split(';').any { it.trim().equals(fromApp, ignoreCase = true) }
                      else if (r.byWord) hasWord(r.match, url)
                      else hasAddress(r.match, url)
            if (hit) return Choice(c, "${c.name} (rule: ${r.describe()})")
        }
        val live = store.live()
        return Choice(live, if (live != null) "live category ${live.name}" else "")
    }

    // "github.com" matches github.com and any site under it (gist.github.com, www.github.com). Text
    // with a "/" in it is looked for anywhere in the address ("github.com/my-company").
    fun hasAddress(pattern: String, url: String): Boolean {
        var p = pattern.trim().lowercase()
        if (p.isEmpty() || url.isEmpty()) return false
        val u = url.lowercase()
        if (p.contains('/')) return u.contains(p)
        if (p.startsWith("*.")) p = p.substring(2)
        val host = Cleaner.hostOf(url) ?: return u.contains(p)
        return host == p || host.endsWith(".$p")
    }

    // A keyword rule: "invoice" matches a whole word in the site, the path, or a value after the ?
    // (example.com/invoice/12, ?q=my+invoice) - never part of a word (invoices), nor a parameter's
    // name (?invoice=1). Several words must come one after another ("pull request": .../pull-request).
    // Big or small letters do not matter. The same as on Windows (Router.HasWord there).
    fun hasWord(keyword: String, url: String): Boolean {
        val want = keyword(keyword)?.split(' ') ?: return false
        return linkWords(url).any { java.util.Collections.indexOfSubList(it, want) >= 0 }
    }

    // What someone typed for a keyword, as a rule matches it: its words in small letters, one space
    // between ("Pull-Request" -> "pull request"). Null if it has fewer than 3 letters and digits.
    fun keyword(text: String): String? = words(text).joinToString(" ").takeIf { it.replace(" ", "").length >= 3 }

    private val notWord = Regex("[^\\p{L}\\p{Nd}]+")
    private fun words(s: String) = s.lowercase().split(notWord).filter { it.isNotEmpty() }

    // The words of a link, part by part: the site, the path, and each value after the ? - never a
    // parameter's name, so a word cannot run from one part into the next. A value that is itself a
    // link is read the same way.
    private fun linkWords(url: String): List<List<String>> {
        val s = url.indexOf("://")
        if (s <= 0) return emptyList()
        val rest = url.substring(s + 3).substringBefore('#')
        val end = rest.indexOfFirst { it == '/' || it == '?' }.let { if (it < 0) rest.length else it }
        val after = rest.substring(end)
        val parts = mutableListOf(words(rest.substring(0, end).substringAfterLast('@').substringBefore(':')),
                                  words(unescape(after.substringBefore('?'))))
        if (after.contains('?')) for (pair in after.substringAfter('?').split('&')) {
            val eq = pair.indexOf('=')
            if (eq < 0) continue   // a name alone, no value
            val value = unescape(pair.substring(eq + 1))
            if (isLink(value)) parts += linkWords(value) else parts += words(value)
        }
        return parts
    }

    private fun isLink(s: String) = s.indexOf("://").let { it > 0 && s.substring(0, it).all { c -> c.isLetterOrDigit() || c in "+.-" } }

    // Each %XX is decoded on its own run, and a stray % is left as it is - as Uri.UnescapeDataString
    // does on Windows, so one bad % does not leave the rest of the value undecoded on Android only.
    private fun unescape(s: String): String {
        if (!s.contains('%')) return s
        val out = StringBuilder()
        var i = 0
        while (i < s.length) {
            val start = i
            val bytes = ByteArrayOutputStream()
            while (i + 2 < s.length && s[i] == '%' && hex(s[i + 1]) >= 0 && hex(s[i + 2]) >= 0) {
                bytes.write(hex(s[i + 1]) * 16 + hex(s[i + 2])); i += 3
            }
            if (i == start) { out.append(s[i]); i++; continue }
            out.append(try { Charsets.UTF_8.newDecoder().decode(ByteBuffer.wrap(bytes.toByteArray())) } catch (_: Exception) { s.substring(start, i) })
        }
        return out.toString()
    }

    private fun hex(c: Char) = "0123456789abcdef".indexOf(c.lowercaseChar())

    // What someone typed for an address, as a rule matches it: "https://github.com/" -> "github.com".
    fun cleanAddress(text: String): String {
        var t = text.trim()
        for (scheme in listOf("https://", "http://")) if (t.startsWith(scheme, ignoreCase = true)) t = t.substring(scheme.length)
        return t.trimEnd('/')
    }
}
