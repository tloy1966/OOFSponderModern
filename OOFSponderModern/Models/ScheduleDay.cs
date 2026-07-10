namespace OOFSponderModern.Models;

public sealed class ScheduleDay
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsOffWork { get; set; }
}
