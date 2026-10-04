package com.husarp.linkpilot

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

// Keyword rules (Router.hasWord): a whole word in the site, the path or a value after the ? - never
// part of a word, never a parameter's name. The same as on Windows (Router.HasWord there).
class KeywordTest {
    private fun hit(keyword: String, url: String) = Router.hasWord(keyword, url)

    @Test fun wordInPath() = assertTrue(hit("invoice", "https://shop.com/invoice/12"))
    @Test fun wordInSite() = assertTrue(hit("github", "https://gist.github.com/x"))
    @Test fun wordInValue() = assertTrue(hit("invoice", "https://shop.com/search?q=my+invoice"))
    @Test fun wordInEscapedValue() = assertTrue(hit("invoice", "https://shop.com/search?q=my%20invoice%2C2024"))
    @Test fun strayPercentLeavesTheRestDecoded() {   // as Uri.UnescapeDataString on Windows
        assertTrue(hit("invoice", "https://shop.com/search?q=100%25off&x=50%%2Bmy%20invoice"))
        assertTrue(hit("invoice", "https://shop.com/search?q=50%+my%20invoice"))
    }
    @Test fun bigOrSmallLetters() = assertTrue(hit("Invoice", "https://shop.com/INVOICE-12"))
    @Test fun notPartOfAWord() {
        assertFalse(hit("invoice", "https://shop.com/invoices/12"))
        assertFalse(hit("voice", "https://shop.com/invoice/12"))
    }
    @Test fun neverAParametersName() {
        assertFalse(hit("invoice", "https://shop.com/?invoice=1"))
        assertFalse(hit("invoice", "https://shop.com/?invoice"))
        assertTrue(hit("invoice", "https://shop.com/?invoice=invoice"))
    }
    @Test fun notInTheFragmentPortOrLogin() {
        assertFalse(hit("invoice", "https://shop.com/a#invoice"))
        assertFalse(hit("8080", "https://shop.com:8080/a"))
        assertFalse(hit("invoice", "https://invoice@shop.com/a"))
    }
    @Test fun severalWordsOneAfterAnother() {
        assertTrue(hit("pull request", "https://git.com/pull-request/5"))
        assertTrue(hit("Pull-Request", "https://git.com/a/pull/request"))
        assertFalse(hit("pull request", "https://git.com/request/pull"))
        assertFalse(hit("pull request", "https://git.com/pull/x/request"))
    }
    @Test fun wordsDoNotRunFromOnePartIntoTheNext() {
        assertFalse(hit("com pull", "https://git.com/pull"))       // the site, then the path
        assertFalse(hit("foo bar", "https://x.com/?a=foo&b=bar"))  // two values
    }
    @Test fun linkInAValueReadTheSameWay() {
        assertTrue(hit("invoice", "https://x.com/go?to=https%3A%2F%2Fshop.com%2Finvoice"))
        assertFalse(hit("invoice", "https://x.com/go?to=https%3A%2F%2Fshop.com%2F%3Finvoice%3D1"))
    }
    @Test fun notALink() = assertFalse(hit("invoice", "invoice"))
    @Test fun atLeastThreeLettersOrDigits() {
        assertNull(Router.keyword("ab"))
        assertNull(Router.keyword(" a-b "))
        assertNull(Router.keyword("--"))
        assertEquals("abc", Router.keyword("abc"))
        assertEquals("pull request", Router.keyword("  Pull-Request! "))
        assertFalse(hit("ab", "https://ab.com/ab"))
    }
    @Test fun lettersBeyondEnglish() = assertTrue(hit("zażółć", "https://x.pl/Zażółć-gęślą"))
}
