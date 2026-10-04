package com.husarp.linkpilot

import java.net.URLDecoder

// Link cleaning - the same lists and the same way as LinkPilot for Windows (Cleaner.cs):
//
//   Redirects   Some links go through a middleman first - google.com/url?q=<the real link>, Outlook's
//               Safe Links, Facebook's l.php... - so that the middleman learns where you went. The
//               real link is written inside, so it can be taken out and opened directly.
//   Tracking    Parts added to the end of a link to say where the click came from: utm_source,
//               fbclid, YouTube's si... The page is the same without them.
//
// Only parts known to be tracking are removed, so a link never stops working. Nothing is looked up
// online - which is also why short links (bit.ly, t.co) cannot be followed.
object Cleaner {
    // A tracking part: its name ("utm_*" is every name starting with utm_), the sites it is removed
    // on ("" = every site; "amazon.*" = Amazon in every country), and what it is.
    class Part(val name: String, val sites: String, val what: String) {
        val id get() = if (sites.isEmpty()) name else "$name@$sites"
    }

    // A middleman: the address it lives at and the part of the link that holds the real one.
    class Redirect(val host: String, val path: String, val param: String, val what: String) {
        val id get() = host + path
    }

    class Options(
        val cleanOn: Boolean = true,
        val unwrapOn: Boolean = true,
        val partsOff: Set<String> = emptySet(),
        val redirectsOff: Set<String> = emptySet(),
    ) {
        fun isOn(p: Part) = partsOff.none { it.equals(p.id, ignoreCase = true) }
        fun isOn(r: Redirect) = redirectsOff.none { it.equals(r.id, ignoreCase = true) }
    }

    // The link as it will be opened, and what was done to it ("skipped Google redirect; removed
    // utm_source, fbclid"), or "" if nothing.
    class Result(val url: String, val changes: String)

    val parts = listOf(
        Part("utm_*", "", "Campaign tracking - Google Analytics, newsletters, most websites"),
        Part("fbclid", "", "Facebook click ID"),
        Part("gclid", "", "Google Ads click ID"),
        Part("gclsrc", "", "Google Ads click source"),
        Part("dclid", "", "Google Display Ads click ID"),
        Part("gbraid", "", "Google Ads click ID (apps)"),
        Part("wbraid", "", "Google Ads click ID (web)"),
        Part("msclkid", "", "Microsoft Ads click ID"),
        Part("twclid", "", "X / Twitter Ads click ID"),
        Part("ttclid", "", "TikTok Ads click ID"),
        Part("yclid", "", "Yandex Ads click ID"),
        Part("li_fat_id", "", "LinkedIn Ads click ID"),
        Part("igshid", "", "Instagram share tracking"),
        Part("igsh", "", "Instagram share tracking"),
        Part("mc_cid", "", "Mailchimp campaign"),
        Part("mc_eid", "", "Mailchimp subscriber - says who you are"),
        Part("_hsenc", "", "HubSpot email tracking"),
        Part("_hsmi", "", "HubSpot email tracking"),
        Part("mkt_tok", "", "Marketo email tracking"),
        // more of the same on every site: from the AdGuard, Brave and ClearURLs lists
        Part("srsltid", "", "Google Shopping tracking"),
        Part("gad_source", "", "Google Ads tracking"),
        Part("gad_campaignid", "", "Google Ads tracking"),
        Part("_gl", "", "Google Analytics - links your visits across sites"),
        Part("_ga", "", "Google Analytics visitor ID"),
        Part("_openstat", "", "Openstat tracking"),
        Part("_kx", "", "Klaviyo email tracking"),
        Part("mc_tc", "", "Mailchimp tracking"),
        Part("__hsfp", "", "HubSpot tracking"),
        Part("__hssc", "", "HubSpot tracking"),
        Part("__hstc", "", "HubSpot tracking"),
        Part("hsCtaTracking", "", "HubSpot tracking"),
        Part("hsa_*", "", "HubSpot ad tracking"),
        Part("mtm_*", "", "Matomo campaign tracking"),
        Part("pk_campaign", "", "Matomo campaign tracking"),
        Part("pk_medium", "", "Matomo campaign tracking"),
        Part("pk_source", "", "Matomo campaign tracking"),
        Part("pk_cid", "", "Matomo tracking"),
        Part("pk_vid", "", "Matomo visitor ID"),
        Part("itm_source", "", "Campaign tracking"),
        Part("itm_medium", "", "Campaign tracking"),
        Part("itm_campaign", "", "Campaign tracking"),
        Part("itm_content", "", "Campaign tracking"),
        Part("itm_term", "", "Campaign tracking"),
        Part("wt_mc", "", "Webtrekk campaign tracking"),
        Part("fb_action_ids", "", "Facebook share tracking"),
        Part("fb_action_types", "", "Facebook share tracking"),
        Part("fb_source", "", "Facebook share tracking"),
        Part("fb_ref", "", "Facebook share tracking"),
        Part("action_object_map", "", "Facebook share tracking"),
        Part("action_type_map", "", "Facebook share tracking"),
        Part("action_ref_map", "", "Facebook share tracking"),
        Part("fbadid", "", "Facebook ad ID"),
        Part("ml_subscriber", "", "MailerLite subscriber - says who you are"),
        Part("ml_subscriber_hash", "", "MailerLite subscriber - says who you are"),
        Part("vero_conv", "", "Vero email tracking"),
        Part("vero_id", "", "Vero email tracking"),
        Part("oly_anon_id", "", "Omeda email tracking"),
        Part("oly_enc_id", "", "Omeda email tracking"),
        Part("rb_clickid", "", "Rebel click ID"),
        Part("wickedid", "", "Wicked Reports click ID"),
        Part("s_cid", "", "Adobe campaign tracking"),
        Part("sfmc_id", "", "Salesforce email tracking"),
        Part("sfmc_activityid", "", "Salesforce email tracking"),
        Part("elqTrackId", "", "Oracle Eloqua email tracking"),
        Part("elqaid", "", "Oracle Eloqua email tracking"),
        Part("elqat", "", "Oracle Eloqua email tracking"),
        Part("bsft_clkid", "", "Blueshift email tracking"),
        Part("bsft_uid", "", "Blueshift email tracking"),
        Part("bsft_eid", "", "Blueshift email tracking"),
        Part("bsft_mid", "", "Blueshift email tracking"),
        Part("at_recipient_id", "", "Email tracking"),
        Part("at_recipient_list", "", "Email tracking"),
        Part("sms_click", "", "Text message campaign tracking"),
        Part("sms_source", "", "Text message campaign tracking"),
        Part("sms_uph", "", "Text message campaign tracking"),
        Part("adobe_mc_ref", "", "Adobe visitor tracking"),
        Part("adobe_mc_sdid", "", "Adobe visitor tracking"),
        Part("irclickid", "", "Impact affiliate click ID"),
        Part("irgwc", "", "Impact affiliate tracking"),
        Part("cjevent", "", "CJ affiliate click ID"),
        Part("cjdata", "", "CJ affiliate tracking"),
        Part("ranMID", "", "Rakuten affiliate tracking"),
        Part("ranEAID", "", "Rakuten affiliate tracking"),
        Part("ranSiteID", "", "Rakuten affiliate tracking"),
        Part("awc", "", "Awin affiliate tracking"),
        Part("sscid", "", "ShareASale click ID"),
        Part("ysclid", "", "Yandex click ID"),
        Part("ymclid", "", "Yandex Metrica click ID"),
        Part("syclid", "", "Snapchat click ID"),
        Part("epik", "", "Pinterest click ID"),
        Part("_branch_match_id", "", "Branch link tracking"),
        Part("_branch_referrer", "", "Branch link tracking"),
        Part("guce_referrer", "", "Yahoo consent tracking"),
        Part("guce_referrer_sig", "", "Yahoo consent tracking"),
        Part("ceneo_spo", "", "Ceneo tracking"),
        Part("si", "youtube.com youtu.be open.spotify.com", "Share tracking - who shared the link with you"),
        Part("pp", "youtube.com", "YouTube search tracking"),
        Part("feature", "youtube.com youtu.be", "Where on YouTube the link was shared from"),
        Part("is", "youtu.be", "Share tracking"),
        Part("source_ve_path", "youtube.com", "YouTube tracking"),
        Part("dlsi", "open.spotify.com", "Spotify share tracking"),
        Part("sp_cid", "open.spotify.com", "Spotify tracking"),
        Part("\$3p", "open.spotify.com reddit.com", "Branch link tracking"),
        Part("ref_src", "twitter.com x.com", "X / Twitter referral"),
        Part("ref_url", "twitter.com x.com", "X / Twitter referral"),
        Part("cxt", "twitter.com x.com", "X / Twitter tracking"),
        Part("s", "twitter.com x.com", "X / Twitter share tracking"),
        Part("t", "twitter.com x.com", "X / Twitter share tracking"),
        Part("ig_rid", "instagram.com", "Instagram tracking"),
        Part("igsi", "instagram.com", "Instagram share tracking"),
        Part("stkn", "instagram.com", "Instagram share tracking"),
        Part("click_source", "instagram.com", "Instagram tracking"),
        Part("mibextid", "facebook.com", "Facebook share tracking"),
        Part("__tn__", "facebook.com", "Facebook tracking"),
        Part("__cft__*", "facebook.com", "Facebook tracking"),
        Part("__xts__*", "facebook.com", "Facebook tracking"),
        Part("comment_tracking", "facebook.com", "Facebook tracking"),
        Part("hc_*", "facebook.com", "Facebook tracking"),
        Part("sfnsn", "facebook.com", "Facebook share tracking"),
        Part("ref", "facebook.com", "Facebook referral"),
        Part("_t", "tiktok.com", "TikTok tracking"),
        Part("_r", "tiktok.com", "TikTok tracking"),
        Part("_d", "tiktok.com", "TikTok tracking"),
        Part("u_code", "tiktok.com", "TikTok share tracking"),
        Part("preview_pb", "tiktok.com", "TikTok tracking"),
        Part("share_app_name", "tiktok.com", "TikTok share tracking"),
        Part("share_app_id", "tiktok.com", "TikTok share tracking"),
        Part("share_iid", "tiktok.com", "TikTok share tracking"),
        Part("share_link_id", "tiktok.com", "TikTok share tracking"),
        Part("share_author_id", "tiktok.com", "TikTok share tracking"),
        Part("share_region", "tiktok.com", "TikTok share tracking"),
        Part("social_share_type", "tiktok.com", "TikTok share tracking"),
        Part("tt_from", "tiktok.com", "TikTok share tracking"),
        Part("sender_device", "tiktok.com", "TikTok share tracking"),
        Part("is_from_webapp", "tiktok.com", "TikTok share tracking"),
        Part("is_copy_url", "tiktok.com", "TikTok share tracking"),
        Part("sec_user_id", "tiktok.com", "TikTok share tracking - who shared it"),
        Part("sec_uid", "tiktok.com", "TikTok share tracking - who shared it"),
        Part("embed_source", "tiktok.com", "TikTok tracking"),
        Part("refer", "tiktok.com", "TikTok tracking"),
        Part("referer_url", "tiktok.com", "TikTok tracking"),
        Part("referer_video_id", "tiktok.com", "TikTok tracking"),
        Part("share_id", "reddit.com", "Reddit share tracking"),
        Part("ref", "reddit.com", "Reddit referral"),
        Part("ref_source", "reddit.com", "Reddit referral"),
        Part("ref_campaign", "reddit.com", "Reddit referral"),
        Part("correlation_id", "reddit.com", "Reddit tracking"),
        Part("rdt", "reddit.com", "Reddit tracking"),
        Part("\$deep_link", "reddit.com", "Reddit app-link tracking"),
        Part("\$original_url", "reddit.com", "Reddit app-link tracking"),
        Part("post_index", "reddit.com", "Reddit tracking"),
        Part("post_fullname", "reddit.com", "Reddit tracking"),
        Part("trk", "linkedin.com", "LinkedIn tracking"),
        Part("trackingId", "linkedin.com", "LinkedIn tracking"),
        Part("refId", "linkedin.com", "LinkedIn tracking"),
        Part("lipi", "linkedin.com", "LinkedIn tracking"),
        Part("original_referer", "linkedin.com", "LinkedIn tracking"),
        Part("rcm", "linkedin.com", "LinkedIn share tracking - who shared it"),
        Part("eBP", "linkedin.com", "LinkedIn tracking"),
        Part("alternateChannel", "linkedin.com", "LinkedIn tracking"),
        Part("wprov", "wikipedia.org", "Wikipedia share tracking"),
        Part("ved", "google.*", "Google search click tracking"),
        Part("ei", "google.*", "Google search session tracking"),
        Part("gs_*", "google.*", "Google search tracking"),
        Part("sca_esv", "google.*", "Google search tracking"),
        Part("sca_upv", "google.*", "Google search tracking"),
        Part("sxsrf", "google.*", "Google search tracking"),
        Part("aqs", "google.*", "Google search tracking"),
        Part("sourceid", "google.*", "Google search tracking"),
        Part("iflsig", "google.*", "Google search tracking"),
        Part("oq", "google.*", "Google search tracking - what you typed"),
        Part("sclient", "google.*", "Google search tracking"),
        Part("uact", "google.*", "Google search tracking"),
        Part("ictx", "google.*", "Google search tracking"),
        Part("fbs", "google.*", "Google search tracking"),
        Part("rlz", "google.*", "Google search tracking"),
        Part("sstk", "google.*", "Google search tracking"),
        Part("pcampaignid", "google.*", "Google tracking"),
        Part("ref_", "amazon.*", "Amazon referral"),
        Part("pd_rd_*", "amazon.*", "Amazon recommendation tracking"),
        Part("pf_rd_*", "amazon.*", "Amazon page tracking"),
        Part("crid", "amazon.*", "Amazon search tracking"),
        Part("sprefix", "amazon.*", "Amazon search tracking"),
        Part("qid", "amazon.*", "Amazon search tracking"),
        Part("sr", "amazon.*", "Amazon search tracking"),
        Part("dib", "amazon.*", "Amazon search tracking"),
        Part("dib_tag", "amazon.*", "Amazon search tracking"),
        Part("content-id", "amazon.*", "Amazon page tracking"),
        // online shops: from ClearURLs' rules (clearurls.xyz), plus the share-link parts of Temu and Shein
        Part("__mk_*", "amazon.*", "Amazon language tracking"),
        Part("spIA", "amazon.*", "Amazon tracking"),
        Part("ms3_c", "amazon.*", "Amazon tracking"),
        Part("refRID", "amazon.*", "Amazon tracking"),
        Part("_encoding", "amazon.*", "Amazon tracking"),
        Part("smid", "amazon.*", "Amazon seller tracking"),
        Part("rnid", "amazon.*", "Amazon tracking"),
        Part("dchild", "amazon.*", "Amazon tracking"),
        Part("aaxitk", "amazon.*", "Amazon ad tracking"),
        Part("hsa_cr_id", "amazon.*", "Amazon ad tracking"),
        Part("sb-ci-*", "amazon.*", "Amazon ad tracking"),
        Part("social_share", "amazon.*", "Amazon share tracking"),
        Part("starsLeft", "amazon.*", "Amazon tracking"),
        Part("skipTwisterOG", "amazon.*", "Amazon tracking"),
        Part("linkCode", "amazon.*", "Amazon affiliate tracking"),
        Part("linkId", "amazon.*", "Amazon affiliate tracking"),
        Part("creativeASIN", "amazon.*", "Amazon affiliate tracking"),
        Part("ascsubtag", "amazon.*", "Amazon affiliate tracking"),
        Part("camp", "amazon.*", "Amazon affiliate tracking"),
        Part("creative", "amazon.*", "Amazon affiliate tracking"),
        Part("tag", "amazon.*", "Amazon affiliate tracking"),
        Part("AssociateTag", "amazon.*", "Amazon affiliate tracking"),
        Part("asc_*", "amazon.*", "Amazon affiliate tracking"),
        Part("geniuslink", "amazon.*", "Amazon affiliate tracking"),
        Part("bitCampaignCode", "amazon.*", "Amazon affiliate tracking"),
        Part("ref", "amazon.*", "Amazon referral"),
        Part("refTag", "amazon.*", "Amazon referral"),
        Part("store_ref", "amazon.*", "Amazon referral"),
        Part("adgrpid", "amazon.*", "Amazon ad tracking"),
        Part("hvadid", "amazon.*", "Amazon ad tracking"),
        Part("hvbmt", "amazon.*", "Amazon ad tracking"),
        Part("hvdev", "amazon.*", "Amazon ad tracking"),
        Part("hvlocphy", "amazon.*", "Amazon ad tracking"),
        Part("hvnetw", "amazon.*", "Amazon ad tracking"),
        Part("hvqmt", "amazon.*", "Amazon ad tracking"),
        Part("hvrand", "amazon.*", "Amazon ad tracking"),
        Part("hvtargid", "amazon.*", "Amazon ad tracking"),
        Part("hydadcr", "amazon.*", "Amazon ad tracking"),
        Part("cv_ct_*", "amazon.*", "Amazon ad tracking"),
        Part("plattr", "amazon.*", "Amazon ad tracking"),
        Part("ac_md", "amazon.*", "Amazon ad tracking"),
        Part("imprToken", "amazon.*", "Amazon ad tracking"),
        Part("ingress", "amazon.*", "Amazon tracking"),
        Part("visitId", "amazon.*", "Amazon tracking"),
        Part("_trkparms", "ebay.*", "eBay tracking"),
        Part("_trksid", "ebay.*", "eBay tracking"),
        Part("_from", "ebay.*", "eBay tracking"),
        Part("hash", "ebay.*", "eBay tracking"),
        Part("amdata", "ebay.*", "eBay tracking"),
        Part("mkcid", "ebay.*", "eBay marketing tracking"),
        Part("mkevt", "ebay.*", "eBay marketing tracking"),
        Part("mkrid", "ebay.*", "eBay marketing tracking"),
        Part("campid", "ebay.*", "eBay affiliate tracking"),
        Part("toolid", "ebay.*", "eBay affiliate tracking"),
        Part("customid", "ebay.*", "eBay affiliate tracking"),
        Part("mkgroupid", "ebay.*", "eBay marketing tracking"),
        Part("mkpid", "ebay.*", "eBay marketing tracking"),
        Part("mktype", "ebay.*", "eBay marketing tracking"),
        Part("emsid", "ebay.*", "eBay email tracking"),
        Part("euid", "ebay.*", "eBay email tracking"),
        Part("ssspo", "ebay.*", "eBay share tracking"),
        Part("sssrc", "ebay.*", "eBay share tracking"),
        Part("ssrc", "ebay.*", "eBay share tracking"),
        Part("ssuid", "ebay.*", "eBay share tracking - who shared it"),
        Part("widget_ver", "ebay.*", "eBay tracking"),
        Part("sojTags", "ebay.*", "eBay tracking"),
        Part("segname", "ebay.*", "eBay tracking"),
        Part("itmmeta", "ebay.*", "eBay tracking"),
        Part("spm", "aliexpress.*", "AliExpress tracking"),
        Part("scm*", "aliexpress.*", "AliExpress tracking"),
        Part("pvid", "aliexpress.*", "AliExpress tracking"),
        Part("algo_*", "aliexpress.*", "AliExpress tracking"),
        Part("ws_ab_test", "aliexpress.*", "AliExpress tracking"),
        Part("btsid", "aliexpress.*", "AliExpress tracking"),
        Part("gps-id", "aliexpress.*", "AliExpress tracking"),
        Part("cv", "aliexpress.*", "AliExpress tracking"),
        Part("af", "aliexpress.*", "AliExpress tracking"),
        Part("dp", "aliexpress.*", "AliExpress tracking"),
        Part("sk", "aliexpress.*", "AliExpress share tracking"),
        Part("mall_affr", "aliexpress.*", "AliExpress affiliate tracking"),
        Part("terminal_id", "aliexpress.*", "AliExpress tracking - which device"),
        Part("aff_*", "aliexpress.*", "AliExpress affiliate tracking"),
        Part("afSmartRedirect", "aliexpress.*", "AliExpress affiliate tracking"),
        Part("srcSns", "aliexpress.*", "AliExpress share tracking"),
        Part("spreadType", "aliexpress.*", "AliExpress share tracking"),
        Part("bizType", "aliexpress.*", "AliExpress share tracking"),
        Part("social_params", "aliexpress.*", "AliExpress share tracking"),
        Part("pdp_npi", "aliexpress.*", "AliExpress tracking - the price you saw"),
        Part("pdp_ext_f", "aliexpress.*", "AliExpress tracking"),
        Part("gatewayAdapt", "aliexpress.*", "AliExpress tracking"),
        Part("curPageLogUid", "aliexpress.*", "AliExpress tracking"),
        Part("utparam*", "aliexpress.*", "AliExpress tracking - what you searched"),
        Part("initiative_id", "aliexpress.*", "AliExpress search tracking"),
        Part("fromRankId", "aliexpress.*", "AliExpress tracking"),
        Part("bi_*", "allegro.*", "Allegro ad and listing tracking"),
        Part("reco_id", "allegro.*", "Allegro recommendation tracking"),
        Part("sid", "allegro.*", "Allegro session tracking"),
        Part("emission_unit_id", "allegro.*", "Allegro ad tracking"),
        Part("emission_id", "allegro.*", "Allegro ad tracking"),
        Part("clickId", "allegro.*", "Allegro ad tracking"),
        Part("ad_reason_recommended_items", "olx.*", "OLX recommendation tracking"),
        Part("_x_*", "temu.*", "Temu tracking"),
        Part("refer_page_*", "temu.*", "Temu tracking - where you came from"),
        Part("share_uin", "temu.*", "Temu share tracking - who shared it"),
        Part("_bg_fs", "temu.*", "Temu tracking"),
        Part("_oak_*", "temu.*", "Temu tracking"),
        Part("_p_rfs", "temu.*", "Temu tracking"),
        Part("refer_share_*", "temu.*", "Temu share tracking - who shared it"),
        Part("from_share", "temu.*", "Temu share tracking"),
        Part("refer_source", "temu.*", "Temu tracking - where you came from"),
        Part("freesia_scene", "temu.*", "Temu tracking"),
        Part("adg_ctx", "temu.*", "Temu ad tracking"),
        Part("mrk_rec", "temu.*", "Temu tracking"),
        Part("_p_jump_id", "temu.*", "Temu tracking"),
        Part("src_module", "shein.*", "Shein tracking"),
        Part("src_identifier", "shein.*", "Shein tracking"),
        Part("src_tab_page_id", "shein.*", "Shein tracking"),
        Part("url_from", "shein.*", "Shein share tracking"),
        Part("click_key", "etsy.com", "Etsy tracking"),
        Part("click_sum", "etsy.com", "Etsy tracking"),
        Part("organic_search_click", "etsy.com", "Etsy tracking"),
        Part("ref", "etsy.com", "Etsy referral"),
        Part("ga_*", "etsy.com", "Etsy tracking"),
        Part("u1", "walmart.*", "Walmart tracking"),
        Part("ath*", "walmart.*", "Walmart ad tracking"),
        Part("tag", "ceneo.pl", "Ceneo tracking"),
    )

    val redirects = listOf(
        Redirect("google.*", "/url", "q url", "Google search results and Gmail"),
        Redirect("googleadservices.com", "/pagead/aclk", "adurl", "Google Ads"),
        Redirect("google.*", "/aclk", "adurl", "Google Ads"),
        Redirect("safelinks.protection.outlook.com", "/", "url", "Outlook Safe Links"),
        Redirect("statics.teams.cdn.office.net", "/evergreen-assets/safelinks/", "url", "Microsoft Teams Safe Links"),
        Redirect("l.facebook.com", "/l.php", "u", "Facebook"),
        Redirect("lm.facebook.com", "/l.php", "u", "Facebook (mobile)"),
        Redirect("l.messenger.com", "/l.php", "u", "Messenger"),
        Redirect("l.instagram.com", "/", "u", "Instagram"),
        Redirect("youtube.com", "/redirect", "q", "YouTube descriptions and comments"),
        Redirect("steamcommunity.com", "/linkfilter/", "u url", "Steam"),
        Redirect("linkedin.com", "/safety/go", "url", "LinkedIn"),
        Redirect("duckduckgo.com", "/l/", "uddg", "DuckDuckGo"),
        Redirect("vk.com", "/away.php", "to", "VK"),
        Redirect("slack-redir.net", "/link", "url", "Slack"),
        Redirect("out.reddit.com", "/", "url", "Reddit"),
        Redirect("click.redditmail.com", "/", "url", "Reddit emails"),
        Redirect("t.umblr.com", "/redirect", "z", "Tumblr"),
        Redirect("rover.ebay.*", "/rover", "mpre", "eBay"),
        Redirect("search.app", "/", "link", "Google app shared links"),
        Redirect("app.adjust.com", "/", "redirect", "Adjust app links"),
        // shopping links that pay whoever sent them: the shop's own address is written inside
        Redirect("click.linksynergy.com", "/", "murl", "Rakuten affiliate links"),
        Redirect("awin1.com", "/", "ued p", "Awin affiliate links"),
        Redirect("tradedoubler.com", "/", "url _td_deeplink", "Tradedoubler affiliate links"),
        Redirect("ad.admitad.com", "/", "ulp", "Admitad affiliate links"),
        Redirect("dpbolvw.net", "/", "url", "CJ affiliate links"),
        Redirect("shareasale.com", "/r.cfm", "urllink", "ShareASale affiliate links"),
        Redirect("go.skimresources.com", "/", "url", "Skimlinks affiliate links"),
        Redirect("redirect.viglink.com", "/", "u", "VigLink affiliate links"),
        Redirect("digidip.net", "/", "url", "Digidip affiliate links"),
        Redirect("webgains.com", "/", "wgtarget", "Webgains affiliate links"),
        Redirect("idealo-partner.com", "/", "trg", "idealo affiliate links"),
        Redirect("partner-ads.com", "/", "htmlurl", "Partner-ads affiliate links"),
        Redirect("flexlinkspro.com", "/", "url", "FlexOffers affiliate links"),
    )

    fun isWebLink(url: String) = url.startsWith("http://", ignoreCase = true) || url.startsWith("https://", ignoreCase = true)

    fun apply(link: String, o: Options): Result {
        if (!isWebLink(link)) return Result(link, "")
        var url = link
        val said = mutableListOf<String>()
        try {
            // a middleman can wrap another (Outlook Safe Links around a Google link): unwrap in turns
            if (o.unwrapOn) for (i in 0 until 5) {
                val (inner, what) = unwrap(url, o) ?: break
                said += "skipped $what redirect"
                url = inner
            }
            if (o.cleanOn) {
                val removed = mutableListOf<String>()
                url = removeTracking(url, removed, o)
                if (removed.isNotEmpty()) said += "removed " + removed.distinct().joinToString(", ")
            }
        } catch (_: Exception) {
            // a link this cannot read is opened as it came
        }
        return Result(url, said.joinToString("; "))
    }

    // The site of a link, "www.youtube.com" - or null if it is not an address with one.
    fun hostOf(url: String): String? = split(url)?.host

    private class Pieces(val host: String, val path: String, val query: String)

    // scheme://[user@]host[:port]/path?query#fragment - read by hand, so an odd character
    // somewhere in a link does not stop it being read.
    private fun split(url: String): Pieces? {
        val s = url.indexOf("://")
        if (s <= 0) return null
        val rest = url.substring(s + 3)
        val end = rest.indexOfFirst { it == '/' || it == '?' || it == '#' }.let { if (it < 0) rest.length else it }
        val host = rest.substring(0, end).substringAfterLast('@').substringBefore(':').lowercase()
        if (host.isEmpty()) return null
        val after = rest.substring(end).substringBefore('#')
        val path = after.substringBefore('?').ifEmpty { "/" }
        val query = if (after.contains('?')) after.substringAfter('?') else ""
        return Pieces(host, path, query)
    }

    // %XX written out; a "+" stays a "+"; anything that is not proper %-writing stays as it is.
    private fun unescape(s: String): String =
        if (!s.contains('%')) s
        else try { URLDecoder.decode(s.replace("+", "%2B"), "UTF-8") } catch (_: Exception) { s }

    // The link inside a middleman's link, and the middleman's name - or null if it is not one.
    private fun unwrap(url: String, o: Options): Pair<String, String>? {
        val p = split(url) ?: return null
        for (r in redirects) {
            if (!o.isOn(r) || !onSite(p.host, r.host)) continue
            if (r.path != "/" && !p.path.startsWith(r.path, ignoreCase = true)) continue
            for (name in r.param.split(' ')) {
                val value = unescape(queryValue(p.query, name) ?: continue).trim()
                // only ever a real web address - never a script or a file hidden in the parameter
                if (isWebLink(value) && split(value) != null) return value to r.what.split(' ')[0].trimEnd(',')
            }
        }
        return null
    }

    private fun queryValue(query: String, name: String): String? {
        for (pair in query.split('&')) {
            val eq = pair.indexOf('=')
            if (eq > 0 && pair.substring(0, eq).equals(name, ignoreCase = true)) return pair.substring(eq + 1)
        }
        return null
    }

    // Takes the tracking parts out of the query (after the ?), leaving everything else - the other
    // parts, their order and their exact spelling, and anything after a # - untouched. Amazon also
    // writes its tracking into the path itself, "/ref=sr_1_3", which goes too.
    private fun removeTracking(link: String, removed: MutableList<String>, o: Options): String {
        var url = link
        var fragment = ""
        val hash = url.indexOf('#')
        if (hash >= 0) { fragment = url.substring(hash); url = url.substring(0, hash) }
        var query = ""
        val q = url.indexOf('?')
        if (q >= 0) { query = url.substring(q + 1); url = url.substring(0, q) }

        val host = hostOf(url) ?: ""
        val active = parts.filter { o.isOn(it) && (it.sites.isEmpty() || it.sites.split(' ').any { s -> onSite(host, s) }) }

        if (host.isNotEmpty() && onSite(host, "amazon.*") && active.any { it.name == "ref_" }) {
            val at = url.indexOf("/ref=", ignoreCase = true)
            if (at > url.indexOf("//") + 1) { url = url.substring(0, at); removed += "/ref=" }
        }

        if (query.isNotEmpty()) {
            val kept = mutableListOf<String>()
            for (pair in query.split('&')) {
                if (pair.isEmpty()) continue
                val eq = pair.indexOf('=')
                val name = unescape(if (eq >= 0) pair.substring(0, eq) else pair)
                if (active.any { matches(it.name, name) }) removed += name else kept += pair
            }
            if (kept.isNotEmpty()) url += "?" + kept.joinToString("&")
        }
        return url + fragment
    }

    private fun matches(pattern: String, name: String) =
        if (pattern.endsWith("*")) name.startsWith(pattern.trimEnd('*'), ignoreCase = true)
        else pattern.equals(name, ignoreCase = true)

    // "youtube.com" is youtube.com and every site under it (www., m., music.); "amazon.*" is Amazon
    // in every country (amazon.de, amazon.co.uk, www.amazon.pl).
    fun onSite(host: String, site: String): Boolean =
        if (site.endsWith(".*")) ".$host.".contains("." + site.dropLast(2) + ".")
        else host == site || host.endsWith(".$site")
}
