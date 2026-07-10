namespace OOFSponderModern.Models;

public sealed class AppState
{
    public int SchemaVersion { get; set; } = 1;
    public IList<ScheduleDay> WeeklySchedule { get; set; } = new List<ScheduleDay>();
    public IList<MessageTemplate> MessageTemplates { get; set; } = new List<MessageTemplate>();
    public MessageProfile Messages { get; set; } = new();
    public SyncState Sync { get; set; } = new();
    public UserPreferences Preferences { get; set; } = new();
    public UpdateState Updates { get; set; } = new();
}
