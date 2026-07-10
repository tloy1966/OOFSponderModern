using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public interface ISchedulerService
{
    OofWindow CalculateNextWindow(IReadOnlyList<ScheduleDay> weeklySchedule, DateTimeOffset now);
    bool IsWithinWorkingHours(IReadOnlyList<ScheduleDay> weeklySchedule, DateTimeOffset now);
}
