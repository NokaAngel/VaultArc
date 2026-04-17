using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace VaultArc.Avalonia.Services;

internal sealed class UpdateCheckService
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "VaultArc" } },
        Timeout = TimeSpan.FromSeconds(10)
    };

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
    }

    public static async Task<(bool Available, string Version, string Url)?> CheckAsync()
    {
        try
        {
            var release = await Http.GetFromJsonAsync<GitHubRelease>(
                "https://api.github.com/repos/NokaAngel/VaultArc/releases/latest")
                .ConfigureAwait(false);

            if (release?.TagName == null) return null;

            var current = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            var latest = release.TagName.TrimStart('v');

            if (Version.TryParse(latest, out var latestVer) &&
                Version.TryParse(current, out var currentVer) &&
                latestVer > currentVer)
            {
                return (true, latest, release.HtmlUrl ?? $"https://github.com/NokaAngel/VaultArc/releases/tag/{release.TagName}");
            }

            return (false, latest, "");
        }
        catch
        {
            return null;
        }
    }
}
