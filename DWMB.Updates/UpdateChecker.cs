using System.Text.RegularExpressions;
using DWMB_AIO.DWMB.Diagnostics;
using RestSharp;

namespace DWMB_AIO.DWMB.Updates
{
    /// <summary>
    /// Checks GitHub Releases for a newer DWMB Client version than the one currently
    /// running. Pure/UI-agnostic — the caller decides how and when to run this (on a
    /// background thread) and what to do with the result. Every failure mode (network
    /// error, rate limiting, no releases yet, an unrecognized tag format, an unparseable
    /// running version) is swallowed and logged rather than thrown: an update check is a
    /// nice-to-have notification and must never crash or block app startup.
    /// </summary>
    static class UpdateChecker
    {
        private const string ApiBaseUrl = "https://api.github.com";
        private const string LatestReleaseEndpoint = "/repos/stevenhperry/DWMBClient/releases/latest";

        // Same format the release workflow already enforces on the tag before it will
        // build a release (see .github/workflows/installer.yml), so any tag that made it
        // into a real GitHub Release is guaranteed to match this.
        private static readonly Regex TagVersionRegex = new(@"^v(\d+)\.(\d+)\.(\d+)$", RegexOptions.Compiled);

        public readonly record struct UpdateResult(string LatestVersion, string ReleaseUrl);

        /// <summary>
        /// Returns the new version + release URL if a strictly newer, well-formed release
        /// is available, otherwise null (no update, or the check couldn't be completed).
        /// Blocking/synchronous — run this on a background thread.
        /// </summary>
        public static UpdateResult? CheckForUpdate(string currentVersion, Logger logger)
        {
            try
            {
                // GitHub's API returns 403 for requests with no User-Agent header, unlike
                // the DWMB server which doesn't require one.
                var options = new RestClientOptions(ApiBaseUrl)
                {
                    UserAgent = $"DWMBClient/{currentVersion}"
                };
                var client = new RestClient(options);

                var request = new RestRequest(LatestReleaseEndpoint, Method.Get);
                request.AddHeader("Accept", "application/vnd.github+json");

                var response = client.Execute<GitHubReleaseResponse>(request);
                if (!response.IsSuccessful || response.Data == null)
                {
                    logger.Log($"[UPDATE-CHECK] GitHub releases request failed: HTTP {(int)response.StatusCode} {response.StatusCode}; {response.ErrorMessage}");
                    return null;
                }

                Match match = TagVersionRegex.Match(response.Data.TagName);
                if (!match.Success)
                {
                    logger.Log($"[UPDATE-CHECK] Unrecognized release tag format, skipping: '{response.Data.TagName}'");
                    return null;
                }

                Version latest = new(
                    int.Parse(match.Groups[1].Value),
                    int.Parse(match.Groups[2].Value),
                    int.Parse(match.Groups[3].Value),
                    0);

                if (!Version.TryParse(currentVersion, out Version? current))
                {
                    logger.Log($"[UPDATE-CHECK] Could not parse running version '{currentVersion}' for comparison; skipping.");
                    return null;
                }

                if (latest.CompareTo(current) <= 0)
                {
                    logger.Log($"[UPDATE-CHECK] No update available (running {current}, latest {latest}).");
                    return null;
                }

                string latestVersionString = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}";
                logger.Log($"[UPDATE-CHECK] Update available: {latestVersionString} (running {current}).");
                return new UpdateResult(latestVersionString, response.Data.HtmlUrl);
            }
            catch (Exception ex)
            {
                logger.Log("[UPDATE-CHECK] Failed to check for updates: " + ex.Message);
                return null;
            }
        }
    }
}
