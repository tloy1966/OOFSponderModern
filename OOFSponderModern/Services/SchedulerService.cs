using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public sealed class SchedulerService : ISchedulerService
{
    private readonly TimeZoneInfo _localTimeZone;

    public SchedulerService(TimeZoneInfo? localTimeZone = null)
    {
        _localTimeZone = localTimeZone ?? TimeZoneInfo.Local;
    }

    public OofWindow CalculateNextWindow(IReadOnlyList<ScheduleDay> weeklySchedule, DateTimeOffset now)
    {
        if (weeklySchedule.Count == 0)
        {
            return new OofWindow(now, now.AddHours(1), "No weekly schedule is configured yet.");
        }

        if (weeklySchedule.All(day => day.IsOffWork))
        {
            return new OofWindow(now, now.AddDays(7), "All days are marked off work; preview keeps OOF active for one week.");
        }

        var today = GetDay(weeklySchedule, now.DayOfWeek);
        if (today is not null && !today.IsOffWork)
        {
            var todayStart = AtLocalTime(now.Date, today.StartTime);
            var todayEnd = AtLocalTime(now.Date, today.EndTime);
            if (todayEnd <= todayStart)
            {
                todayEnd = AtLocalTime(now.Date.AddDays(1), today.EndTime);
            }

            if (now < todayStart)
            {
                return new OofWindow(now, todayStart, "Before today's working hours; keep OOF active until today's start.");
            }

            if (now <= todayEnd)
            {
                var nextStart = FindNextWorkingStart(weeklySchedule, todayEnd.AddSeconds(1));
                return new OofWindow(todayEnd, nextStart, "Currently in working hours; schedule OOF from today's end through the next working start.");
            }

            return new OofWindow(now, FindNextWorkingStart(weeklySchedule, now.AddSeconds(1)), "After today's working hours; keep OOF active through the next working start.");
        }

        return new OofWindow(
            now,
            FindNextWorkingStart(weeklySchedule, now.AddSeconds(1)),
            "Today is marked off work; keep OOF active through the next working start.");
    }

    public bool IsWithinWorkingHours(IReadOnlyList<ScheduleDay> weeklySchedule, DateTimeOffset now)
    {
        var today = GetDay(weeklySchedule, now.DayOfWeek);
        if (today is null || today.IsOffWork)
        {
            return false;
        }

        var start = AtLocalTime(now.Date, today.StartTime);
        var end = AtLocalTime(now.Date, today.EndTime);
        if (end <= start)
        {
            end = AtLocalTime(now.Date.AddDays(1), today.EndTime);
        }

        return now >= start && now <= end;
    }

    private static ScheduleDay? GetDay(IReadOnlyList<ScheduleDay> schedule, DayOfWeek dayOfWeek) =>
        schedule.FirstOrDefault(day => day.DayOfWeek == dayOfWeek);

    private DateTimeOffset AtLocalTime(DateTime date, TimeSpan time)
    {
        var localDateTime = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
        if (_localTimeZone.IsInvalidTime(localDateTime))
        {
            localDateTime = localDateTime.AddHours(1);
        }

        return new DateTimeOffset(localDateTime, _localTimeZone.GetUtcOffset(localDateTime));
    }

    private DateTimeOffset FindNextWorkingStart(IReadOnlyList<ScheduleDay> schedule, DateTimeOffset after)
    {
        for (var offset = 0; offset < 14; offset++)
        {
            var date = after.Date.AddDays(offset);
            var day = GetDay(schedule, date.DayOfWeek);
            if (day is null || day.IsOffWork)
            {
                continue;
            }

            var start = AtLocalTime(date, day.StartTime);
            if (start > after)
            {
                return start;
            }
        }

        return after.AddDays(7);
    }
}
