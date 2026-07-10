namespace OOFSponderModern.Models;

public sealed class SyncState
{
    public string AuthState { get; set; } = "Not connected";
    public string SyncStatus { get; set; } = "Preview only";
    public DateTimeOffset? LastSyncAttempt { get; set; }
    public bool IsMockMode { get; set; } = true;
    public IList<string> RecentActivity { get; } = new List<string>();
}
