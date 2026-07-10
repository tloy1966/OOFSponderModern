namespace OOFSponderModern.Models;

public sealed class AppState
{
    public IList<ScheduleDay> WeeklySchedule { get; } = new List<ScheduleDay>();
    public MessageProfile Messages { get; set; } = new();
    public SyncState Sync { get; set; } = new();
    public UserPreferences Preferences { get; set; } = new();
}
