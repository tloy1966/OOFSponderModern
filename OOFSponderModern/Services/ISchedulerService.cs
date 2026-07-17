using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public interface ISchedulerService
{
    OofWindow CalculateNextWindow(IReadOnlyList<ScheduleDay> weeklySchedule, DateTimeOffset now);
    OofWindow CalculateLongLeaveWindow(DateTimeOffset start, DateTimeOffset end);
    bool IsWithinWorkingHours(IReadOnlyList<ScheduleDay> weeklySchedule, DateTimeOffset now);
}
