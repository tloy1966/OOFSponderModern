using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public sealed class GitHubReleaseUpdateService : IReleaseUpdateService
{
    private static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/tloy1966/OOFSponderModern/releases/latest");
    private readonly HttpClient _httpClient;

    public GitHubReleaseUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OOFSponderModern", "1.0"));
        }
    }

    public async Task<ReleaseInformation?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(LatestReleaseUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (ReadBoolean(root, "draft") || ReadBoolean(root, "prerelease"))
        {
            return null;
        }

        var tag = ReadString(root, "tag_name").TrimStart('v', 'V');
        var url = ReadString(root, "html_url");
        if (!SemanticVersion.TryParse(tag, out _) || !Uri.TryCreate(url, UriKind.Absolute, out var releaseUri) || releaseUri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var publishedAt = DateTimeOffset.TryParse(ReadString(root, "published_at"), out var parsedPublishedAt)
            ? parsedPublishedAt
            : DateTimeOffset.MinValue;

        return new ReleaseInformation(
            tag,
            ReadString(root, "name", $"OOFSponderModern v{tag}"),
            ReadString(root, "body", "No release notes were provided."),
            releaseUri.ToString(),
            publishedAt);
    }

    private static string ReadString(JsonElement element, string name, string fallback = "") =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;

    private static bool ReadBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.True;
}

public readonly record struct SemanticVersion(int Major, int Minor, int Patch, string PreRelease) : IComparable<SemanticVersion>
{
    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().TrimStart('v', 'V');
        var metadataIndex = normalized.IndexOf('+');
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        var parts = normalized.Split('-', 2);
        var numbers = parts[0].Split('.');
        if (numbers.Length != 3 ||
            !int.TryParse(numbers[0], out var major) ||
            !int.TryParse(numbers[1], out var minor) ||
            !int.TryParse(numbers[2], out var patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch, parts.Length == 2 ? parts[1] : string.Empty);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;
        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;
        result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;
        if (string.IsNullOrEmpty(PreRelease)) return string.IsNullOrEmpty(other.PreRelease) ? 0 : 1;
        if (string.IsNullOrEmpty(other.PreRelease)) return -1;
        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    private static int ComparePreRelease(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        for (var index = 0; index < Math.Max(leftParts.Length, rightParts.Length); index++)
        {
            if (index >= leftParts.Length) return -1;
            if (index >= rightParts.Length) return 1;
            var leftNumeric = int.TryParse(leftParts[index], out var leftNumber);
            var rightNumeric = int.TryParse(rightParts[index], out var rightNumber);
            var result = leftNumeric && rightNumeric
                ? leftNumber.CompareTo(rightNumber)
                : leftNumeric ? -1
                : rightNumeric ? 1
                : string.Compare(leftParts[index], rightParts[index], StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;
        }

        return 0;
    }
}
