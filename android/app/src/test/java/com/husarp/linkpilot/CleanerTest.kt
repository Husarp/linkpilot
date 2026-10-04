package com.husarp.linkpilot

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

// The same known answers as LinkPilot for Windows' cleaning test (CleanTest.cs), so both clean
// links the same way.
class CleanerTest {
    private val on = Cleaner.Options()
    private fun clean(link: String, o: Cleaner.Options = on) = Cleaner.apply(link, o).url

    @Test fun utmRemovedRestKeptInOrder() = assertEquals("https://example.com/page?id=5&b=2",
        clean("https://example.com/page?id=5&utm_source=x&b=2&utm_medium=y"))
    @Test fun onlyTrackingTheQuestionMarkGoesToo() = assertEquals("https://example.com/", clean("https://example.com/?fbclid=abc"))
    @Test fun fragmentKept() = assertEquals("https://example.com/a#section", clean("https://example.com/a?utm_campaign=z#section"))
    @Test fun namesInCapitals() = assertEquals("https://example.com/?k=1", clean("https://example.com/?UTM_Source=x&k=1"))
    @Test fun youtubeShortSi() = assertEquals("https://youtu.be/dQw4w9WgXcQ", clean("https://youtu.be/dQw4w9WgXcQ?si=abc123"))
    @Test fun youtubeVideoAndTimeKept() = assertEquals("https://www.youtube.com/watch?v=abc&t=42s",
        clean("https://www.youtube.com/watch?v=abc&t=42s&si=zz&pp=ygUE"))
    @Test fun siOnAnotherSiteLeftAlone() = assertEquals("https://example.com/?si=5", clean("https://example.com/?si=5"))
    @Test fun googleRedirectUrlEncoded() = assertEquals("https://github.com/x",
        clean("https://www.google.com/url?sa=t&url=https%3A%2F%2Fgithub.com%2Fx%3Futm_source%3Dg&ved=2"))
    @Test fun googleRedirectQPlain() = assertEquals("https://example.org/page", clean("https://www.google.com/url?q=https://example.org/page&sa=D"))
    @Test fun googleDocsRedirect() = assertEquals("https://example.org/doc", clean("https://docs.google.com/url?q=https%3A%2F%2Fexample.org%2Fdoc"))
    @Test fun googleSearchIsNotARedirect() = assertEquals("https://www.google.com/search?q=https://x.com",
        clean("https://www.google.com/search?q=https://x.com"))
    @Test fun outlookAroundGoogle() = assertEquals("https://example.org/deep",
        clean("https://eur01.safelinks.protection.outlook.com/?url=https%3A%2F%2Fwww.google.com%2Furl%3Fq%3Dhttps%253A%252F%252Fexample.org%252Fdeep%26sa%3DD&data=05"))
    @Test fun facebookRedirectFbclidInside() = assertEquals("https://news.example/story",
        clean("https://l.facebook.com/l.php?u=https%3A%2F%2Fnews.example%2Fstory%3Ffbclid%3DIwAR&h=AT0"))
    @Test fun youtubeDescriptionLink() = assertEquals("https://example.org/shop",
        clean("https://www.youtube.com/redirect?event=video_description&q=https%3A%2F%2Fexample.org%2Fshop"))
    @Test fun steamLinkFilter() = assertEquals("https://example.org", clean("https://steamcommunity.com/linkfilter/?u=https%3A%2F%2Fexample.org"))
    @Test fun scriptInsideRedirectRefused() = assertEquals("https://www.google.com/url?q=javascript:alert(1)",
        clean("https://www.google.com/url?q=javascript:alert(1)"))
    @Test fun amazon() = assertEquals("https://www.amazon.de/Some-Product/dp/B000123?keywords=cable&th=1",
        clean("https://www.amazon.de/Some-Product/dp/B000123/ref=sr_1_3?crid=ABC&keywords=cable&qid=1700&sprefix=cab&sr=8-3&th=1"))
    @Test fun aliExpressShareLink() = assertEquals("https://www.aliexpress.com/item/1005006123456789.html",
        clean("https://www.aliexpress.com/item/1005006123456789.html?spm=a2g0o.productlist.main.1&algo_pvid=x&aff_fcid=1&aff_fsk=2&aff_platform=link-c-tool&sk=_d7x&aff_trace_key=k&terminal_id=t&afSmartRedirect=y&pdp_npi=4%40dis%21PLN&gatewayAdapt=glo2pol"))
    @Test fun allegroOffer() = assertEquals("https://allegro.pl/oferta/sluchawki-12345",
        clean("https://allegro.pl/oferta/sluchawki-12345?bi_s=ads&bi_m=listing%3Adesktop%3Aquery&bi_c=abc&bi_t=ape&reco_id=r1&sid=s1"))
    @Test fun allegroUnknownPartKept() = assertEquals("https://allegro.pl/oferta/x-1?snapshot=MjAy", clean("https://allegro.pl/oferta/x-1?reco_id=r&snapshot=MjAy"))
    @Test fun temuShareLink() = assertEquals("https://www.temu.com/pl/goods.html?goods_id=601099",
        clean("https://www.temu.com/pl/goods.html?_bg_fs=1&goods_id=601099&refer_page_name=home&refer_page_id=10005&_x_sessn_id=abc&share_uin=XYZ"))
    @Test fun ebayVariantKept() = assertEquals("https://www.ebay.com/itm/1234?var=7", clean("https://www.ebay.com/itm/1234?hash=item1c&_trkparms=x&mkcid=1&campid=5&var=7"))
    @Test fun amazonAffiliate() = assertEquals("https://www.amazon.pl/dp/B0ABC", clean("https://www.amazon.pl/dp/B0ABC?linkCode=ll1&linkId=abc&ascsubtag=z&smid=A1"))
    @Test fun etsyListing() = assertEquals("https://www.etsy.com/listing/123/mug",
        clean("https://www.etsy.com/listing/123/mug?click_key=a&click_sum=b&ref=hp_rv&ga_order=most_relevant"))
    @Test fun shopPartsElsewhereKept() = assertEquals("https://example.com/?sk=1&ref=x", clean("https://example.com/?sk=1&ref=x"))
    @Test fun aliExpressSearchResult() = assertEquals("https://pl.aliexpress.com/item/1005007956595041.html",
        clean("https://pl.aliexpress.com/item/1005007956595041.html?curPageLogUid=DKXRIswVvOxB&utparam-url=scene%3Asearch%7Cquery_from%3A%7Cx_object_id%3A1005007956595041%7C_p_origin_prod%3A"))
    @Test fun aliExpressWhatWasChanged() = assertEquals("removed curPageLogUid, utparam-url",
        Cleaner.apply("https://pl.aliexpress.com/item/1.html?curPageLogUid=D&utparam-url=s", on).changes)
    @Test fun amazonTagAndAdsGoVariantKept() = assertEquals("https://www.amazon.com/dp/B0ABC?th=1&psc=1",
        clean("https://www.amazon.com/dp/B0ABC?tag=site-20&th=1&ref=as_li&hvadid=5&asc_campaign=c&psc=1&cv_ct_cx=x"))
    @Test fun googleSearchQueryKept() = assertEquals("https://www.google.com/search?q=cats&ie=UTF-8",
        clean("https://www.google.com/search?q=cats&oq=cat&gs_lcrp=EgZ&sourceid=chrome&ie=UTF-8&ved=2ahU&ei=k3x&sca_esv=1"))
    @Test fun googleAdLink() = assertEquals("https://shop.example/p",
        clean("https://www.google.com/aclk?sa=l&ai=DChc&adurl=https%3A%2F%2Fshop.example%2Fp%3Fgad_source%3D1"))
    @Test fun googleAdLinkWithoutAddressLeftAlone() = assertEquals("https://www.google.com/aclk?sa=l&ai=DChc&adurl=",
        clean("https://www.google.com/aclk?sa=l&ai=DChc&adurl="))
    @Test fun globalAdditions() = assertEquals("https://shop.example/p?id=3",
        clean("https://shop.example/p?srsltid=AfmB&id=3&_gl=1*abc&_ga=2.1&mtm_campaign=x&__hstc=1&irclickid=z&ysclid=q"))
    @Test fun shortNamesOnlyOnTheirSite() = assertEquals("https://example.com/?s=1&t=2&tag=x&ref=y&is=1&trk=a",
        clean("https://example.com/?s=1&t=2&tag=x&ref=y&is=1&trk=a"))
    @Test fun xShareLink() = assertEquals("https://x.com/u/status/1", clean("https://x.com/u/status/1?s=46&t=AbC"))
    @Test fun facebookPhotoAlbumKept() = assertEquals("https://www.facebook.com/photo/?fbid=1&set=a.2",
        clean("https://www.facebook.com/photo/?fbid=1&set=a.2&__cft__%5B0%5D=AZ&__tn__=EH-R&mibextid=Zb"))
    @Test fun tiktokShareLink() = assertEquals("https://www.tiktok.com/@u/video/123",
        clean("https://www.tiktok.com/@u/video/123?_t=8x&_r=1&is_from_webapp=1&sender_device=pc&share_app_id=1233"))
    @Test fun redditShareLink() = assertEquals("https://www.reddit.com/r/x/comments/abc/t/?context=3",
        clean("https://www.reddit.com/r/x/comments/abc/t/?share_id=z&%24deep_link=true&context=3&utm_name=web&ref=share&rdt=1"))
    @Test fun linkedInJob() = assertEquals("https://www.linkedin.com/jobs/view/123/",
        clean("https://www.linkedin.com/jobs/view/123/?trk=x&refId=y&trackingId=z&eBP=e&alternateChannel=search"))
    @Test fun ebayShareLink() = assertEquals("https://www.ebay.de/itm/1234?var=7",
        clean("https://www.ebay.de/itm/1234?var=7&mkgroupid=1&ssspo=a&sssrc=2&ssuid=u&widget_ver=artemis&itmmeta=01H"))
    @Test fun temuShareLinkNewParts() = assertEquals("https://www.temu.com/goods.html?goods_id=601099",
        clean("https://www.temu.com/goods.html?goods_id=601099&refer_share_id=a&refer_share_channel=copy&from_share=1"))
    @Test fun wikipediaShare() = assertEquals("https://en.wikipedia.org/wiki/Link", clean("https://en.wikipedia.org/wiki/Link?wprov=sfla1"))
    @Test fun affiliateLink() = assertEquals("https://www.example.com/p",
        clean("https://click.linksynergy.com/deeplink?id=x&mid=1&murl=https%3A%2F%2Fwww.example.com%2Fp"))
    @Test fun ebayRover() = assertEquals("https://www.ebay.com/itm/123",
        clean("https://rover.ebay.com/rover/1/711-53200-19255-0/1?mpre=https%3A%2F%2Fwww.ebay.com%2Fitm%2F123&campid=5"))
    @Test fun notAWebLinkUntouched() = assertEquals("content://x/page.htm?utm_source=x", clean("content://x/page.htm?utm_source=x"))

    @Test fun siSwitchedOff() = assertEquals("https://youtu.be/x?si=abc",
        clean("https://youtu.be/x?si=abc", Cleaner.Options(partsOff = setOf("si@youtube.com youtu.be open.spotify.com"))))
    @Test fun trackingOffRedirectStillSkipped() = assertEquals("https://example.org/?utm_source=x",
        clean("https://www.google.com/url?q=https://example.org/?utm_source=x", Cleaner.Options(cleanOn = false)))
    @Test fun redirectsOffOwnTrackingRemoved() = assertEquals("https://www.google.com/url?q=https://example.org/",
        clean("https://www.google.com/url?q=https://example.org/&utm_source=x", Cleaner.Options(unwrapOn = false)))
    @Test fun googleRedirectSwitchedOff() = assertEquals("https://www.google.com/url?q=https://example.org/",
        clean("https://www.google.com/url?q=https://example.org/", Cleaner.Options(redirectsOff = setOf("google.*/url"))))

    @Test fun whatWasChanged() = assertEquals("skipped Google redirect; removed utm_source",
        Cleaner.apply("https://www.google.com/url?q=https%3A%2F%2Fexample.org%2Fa%3Futm_source%3Dx%26id%3D7", on).changes)
    @Test fun plusSignKept() = assertEquals("https://example.org/?q=a+b",
        clean("https://www.google.com/url?q=https%3A%2F%2Fexample.org%2F%3Fq%3Da%2Bb"))

    @Test fun addressRules() {
        assertTrue(Router.hasAddress("github.com", "https://gist.github.com/x"))
        assertTrue(Router.hasAddress("github.com", "https://github.com/"))
        assertFalse(Router.hasAddress("github.com", "https://notgithub.com/"))
        assertTrue(Router.hasAddress("github.com/my-company", "https://github.com/my-company/repo"))
        assertFalse(Router.hasAddress("github.com/my-company", "https://github.com/other"))
        assertEquals("github.com", Router.cleanAddress(" https://github.com/ "))
    }
}
