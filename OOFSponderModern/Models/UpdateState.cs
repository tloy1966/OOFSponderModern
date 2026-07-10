namespace OOFSponderModern.Models;

public sealed class UpdateState
{
    public DateTimeOffset? LastCheckedAt { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public string LatestName { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string ReleaseUrl { get; set; } = string.Empty;
    public string SkippedVersion { get; set; } = string.Empty;
}
