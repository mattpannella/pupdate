using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Pannella.Services;

public static class PatreonService
{
    private const string PATREON_BASE = "https://www.patreon.com";
    private const int PAGE_SIZE = 20;
    private const int MAX_PAGES = 5;

    // Browser-like UA — Patreon's frontend API rejects some requests without one.
    private const string USER_AGENT =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    public static byte[] FetchAttachment(string sessionCookie, string creatorVanity, string filename, out string sourcePostUrl)
    {
        sourcePostUrl = null;

        if (string.IsNullOrWhiteSpace(sessionCookie))
            throw new Exception("Patreon session cookie is not set.");

        if (string.IsNullOrWhiteSpace(creatorVanity))
            throw new Exception("Creator vanity (Patreon URL slug) is required.");

        if (string.IsNullOrWhiteSpace(filename))
            throw new Exception("Attachment filename is required.");

        using var client = BuildClient(sessionCookie);

        string campaignId = ResolveCampaignId(client, creatorVanity);
        (string postUrl, string downloadUrl) = FindLatestPostWithAttachment(client, campaignId, creatorVanity, filename);

        sourcePostUrl = postUrl;

        return DownloadAttachment(client, downloadUrl);
    }

    public class SessionCookieDiagnostics
    {
        public bool CookieValid { get; set; }
        public string PatreonUserName { get; set; }
        public string CreatorVanity { get; set; }
        public string CampaignId { get; set; }
        public bool IsPatron { get; set; }
        public string TierName { get; set; }
        public string PatronStatus { get; set; }      // e.g. "active_patron", "declined_patron", "former_patron"
        public bool PostsQueryReachable { get; set; }
        public List<string> Messages { get; } = new();
    }

    /// <summary>
    /// Verifies the session cookie works. If <paramref name="creatorVanity"/> is provided,
    /// also checks whether the authenticated user is a patron of that creator and that
    /// the campaign/posts lookup works end-to-end.
    /// </summary>
    public static SessionCookieDiagnostics TestSessionCookie(string sessionCookie, string creatorVanity = null)
    {
        var diag = new SessionCookieDiagnostics { CreatorVanity = creatorVanity };

        if (string.IsNullOrWhiteSpace(sessionCookie))
        {
            diag.Messages.Add("No session cookie provided.");
            return diag;
        }

        using var client = BuildClient(sessionCookie);

        // 1. Validate cookie by hitting /api/current_user
        try
        {
            string url = $"{PATREON_BASE}/api/current_user?include=memberships.currently_entitled_tiers,memberships.campaign" +
                         "&fields[user]=full_name,email" +
                         "&fields[member]=patron_status" +
                         "&fields[tier]=title" +
                         "&fields[campaign]=vanity";
            var response = client.GetAsync(url).Result;

            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                diag.Messages.Add($"Cookie rejected by Patreon (HTTP {(int)response.StatusCode}). It may be expired.");
                return diag;
            }

            if (!response.IsSuccessStatusCode)
            {
                diag.Messages.Add($"current_user request failed (HTTP {(int)response.StatusCode}).");
                return diag;
            }

            string body = response.Content.ReadAsStringAsync().Result;
            JObject json = JObject.Parse(body);

            string userName = json["data"]?["attributes"]?["full_name"]?.ToString();

            if (string.IsNullOrEmpty(userName))
            {
                diag.Messages.Add("Cookie returned a response but no user identity. Patreon may have logged the session out.");
                return diag;
            }

            diag.CookieValid = true;
            diag.PatreonUserName = userName;
            diag.Messages.Add($"Cookie valid. Logged in as: {userName}");

            // 2. If a creator vanity was supplied, look for that membership in 'included'
            if (!string.IsNullOrWhiteSpace(creatorVanity))
            {
                var included = json["included"] as JArray ?? new JArray();
                var campaignById = new Dictionary<string, JToken>();
                var tierById = new Dictionary<string, JToken>();

                foreach (var item in included)
                {
                    string type = item["type"]?.ToString();
                    string id = item["id"]?.ToString();

                    if (string.IsNullOrEmpty(id)) continue;

                    if (type == "campaign") campaignById[id] = item;
                    else if (type == "tier") tierById[id] = item;
                }

                foreach (var item in included)
                {
                    if (item["type"]?.ToString() != "member") continue;

                    string memberCampaignId = item["relationships"]?["campaign"]?["data"]?["id"]?.ToString();

                    if (memberCampaignId == null || !campaignById.TryGetValue(memberCampaignId, out var campaign)) continue;

                    string vanity = campaign["attributes"]?["vanity"]?.ToString();

                    if (!string.Equals(vanity, creatorVanity, StringComparison.OrdinalIgnoreCase)) continue;

                    diag.IsPatron = true;
                    diag.PatronStatus = item["attributes"]?["patron_status"]?.ToString();

                    var tiers = item["relationships"]?["currently_entitled_tiers"]?["data"] as JArray;

                    if (tiers != null && tiers.Count > 0)
                    {
                        var tierNames = new List<string>();

                        foreach (var t in tiers)
                        {
                            string tid = t["id"]?.ToString();

                            if (tid != null && tierById.TryGetValue(tid, out var tier))
                            {
                                string title = tier["attributes"]?["title"]?.ToString();

                                if (!string.IsNullOrEmpty(title)) tierNames.Add(title);
                            }
                        }

                        diag.TierName = string.Join(", ", tierNames);
                    }

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            diag.Messages.Add("current_user request threw: " + ex.Message);
            return diag;
        }

        if (string.IsNullOrWhiteSpace(creatorVanity))
            return diag;

        // 3. Resolve the creator's campaign id (confirms the scraping path works)
        try
        {
            diag.CampaignId = ResolveCampaignId(client, creatorVanity);
            diag.Messages.Add($"Campaign id for '{creatorVanity}' resolved: {diag.CampaignId}");
        }
        catch (Exception ex)
        {
            diag.Messages.Add($"Campaign lookup for '{creatorVanity}' failed: " + ex.Message);
            return diag;
        }

        // 4. Verify the posts endpoint responds (request only 1 post so we don't burn rate limit)
        try
        {
            string postsUrl =
                $"{PATREON_BASE}/api/posts" +
                $"?filter[campaign_id]={diag.CampaignId}" +
                $"&sort=-published_at" +
                $"&page[count]=1";
            var response = client.GetAsync(postsUrl).Result;

            diag.PostsQueryReachable = response.IsSuccessStatusCode;
            diag.Messages.Add(response.IsSuccessStatusCode
                ? "Posts query reachable."
                : $"Posts query failed (HTTP {(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            diag.Messages.Add("Posts query threw: " + ex.Message);
        }

        return diag;
    }

    private static HttpClient BuildClient(string sessionCookie)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            UseCookies = false // we set the Cookie header manually
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(USER_AGENT);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionCookie.Trim()}");

        return client;
    }

    private static string ResolveCampaignId(HttpClient client, string creatorVanity)
    {
        // Scrape the creator's public campaign page — the campaign id is embedded in the
        // bootstrap JSON. This is more resilient than the frontend /api/campaigns filter
        // endpoints, which Patreon changes periodically.
        string url = $"{PATREON_BASE}/{Uri.EscapeDataString(creatorVanity)}";
        var response = client.GetAsync(url).Result;

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"Failed to load Patreon campaign page for '{creatorVanity}' (HTTP {(int)response.StatusCode}).");
        }

        string html = response.Content.ReadAsStringAsync().Result;

        // Matches patterns like "id":"123456","type":"campaign"
        var match = Regex.Match(html, @"""id""\s*:\s*""(\d+)""\s*,\s*""type""\s*:\s*""campaign""");

        if (!match.Success)
        {
            throw new Exception(
                $"Could not locate the campaign id for '{creatorVanity}' on the Patreon page. " +
                "Patreon may have changed their page structure.");
        }

        return match.Groups[1].Value;
    }

    private static (string postUrl, string downloadUrl) FindLatestPostWithAttachment(
        HttpClient client, string campaignId, string creatorVanity, string filename)
    {
        string nextUrl =
            $"{PATREON_BASE}/api/posts" +
            $"?filter[campaign_id]={campaignId}" +
            $"&filter[contains_exclusive_posts]=true" +
            $"&sort=-published_at" +
            $"&include=attachments_media,attachments" +
            $"&fields[post]=title,url,published_at,current_user_can_view" +
            $"&fields[media]=file_name,download_url" +
            $"&page[count]={PAGE_SIZE}";

        for (int page = 0; page < MAX_PAGES && !string.IsNullOrEmpty(nextUrl); page++)
        {
            var response = client.GetAsync(nextUrl).Result;

            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new Exception(
                    "Patreon rejected the session cookie. It may be expired — " +
                    "grab a fresh one from your browser and try again.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Patreon posts request failed (HTTP {(int)response.StatusCode}).");
            }

            string body = response.Content.ReadAsStringAsync().Result;
            JObject json = JObject.Parse(body);

            var included = json["included"] as JArray ?? new JArray();
            var mediaById = BuildMediaMap(included);

            var posts = json["data"] as JArray ?? new JArray();
            bool sawGatedPost = false;

            foreach (var post in posts)
            {
                var relAttachments = post["relationships"]?["attachments_media"]?["data"] as JArray
                                     ?? post["relationships"]?["attachments"]?["data"] as JArray;

                if (relAttachments == null || relAttachments.Count == 0)
                    continue;

                foreach (var rel in relAttachments)
                {
                    string mediaId = rel["id"]?.ToString();

                    if (mediaId == null || !mediaById.TryGetValue(mediaId, out var media))
                        continue;

                    string fileName = media["attributes"]?["file_name"]?.ToString();

                    if (!string.Equals(fileName, filename, StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool canView = post["attributes"]?["current_user_can_view"]?.Value<bool?>() ?? false;
                    string postUrl = post["attributes"]?["url"]?.ToString() ?? "(unknown post)";

                    if (!canView)
                    {
                        sawGatedPost = true;
                        continue;
                    }

                    string downloadUrl = media["attributes"]?["download_url"]?.ToString();

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        throw new Exception(
                            $"Found {filename} on post {postUrl} but no download URL was returned.");
                    }

                    return (postUrl, downloadUrl);
                }
            }

            if (sawGatedPost)
            {
                throw new Exception(
                    $"Found a '{creatorVanity}' post with {filename}, but your account can't view it. " +
                    "Your Patreon subscription tier may not include access to this post.");
            }

            nextUrl = json["links"]?["next"]?.ToString();
        }

        throw new Exception(
            $"No recent '{creatorVanity}' Patreon post with an attachment named '{filename}' was found " +
            $"in the last {MAX_PAGES * PAGE_SIZE} posts.");
    }

    private static Dictionary<string, JToken> BuildMediaMap(JArray included)
    {
        var map = new Dictionary<string, JToken>();

        foreach (var item in included)
        {
            if (item["type"]?.ToString() != "media")
                continue;

            string id = item["id"]?.ToString();

            if (!string.IsNullOrEmpty(id))
                map[id] = item;
        }

        return map;
    }

    private static byte[] DownloadAttachment(HttpClient client, string downloadUrl)
    {
        var response = client.GetAsync(downloadUrl).Result;

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"Attachment download failed (HTTP {(int)response.StatusCode}).");
        }

        return response.Content.ReadAsByteArrayAsync().Result;
    }
}
