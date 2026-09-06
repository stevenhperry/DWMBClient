using System.Text.Json.Serialization;

namespace DWMB_AIO.DWMB.Updates
{
    /// <summary>
    /// The subset of GitHub's "latest release" API response this app cares about. Unknown
    /// fields are ignored by System.Text.Json's default deserialization behavior.
    /// </summary>
    class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public required string TagName { get; set; }

        [JsonPropertyName("html_url")]
        public required string HtmlUrl { get; set; }
    }
}
