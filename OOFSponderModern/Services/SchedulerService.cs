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

        if (TryGetActiveWorkingEnd(weeklySchedule, now, out var activeWorkingEnd))
        {
            var nextStart = FindNextWorkingStart(weeklySchedule, activeWorkingEnd.AddSeconds(1));
            return new OofWindow(activeWorkingEnd, nextStart, "Currently in working hours; schedule OOF when the current work period ends through the next working start.");
        }

        var today = GetDay(weeklySchedule, now.DayOfWeek);
        if (today is not null && !today.IsOffWork)
        {
            var todayStart = AtLocalTime(now.Date, today.StartTime);

            if (now < todayStart)
            {
                return new OofWindow(now, todayStart, "Before today's working hours; keep OOF active until today's start.");
            }

            return new OofWindow(now, FindNextWorkingStart(weeklySchedule, now.AddSeconds(1)), "After today's working hours; keep OOF active through the next working start.");
        }

        return new OofWindow(
            now,
            FindNextWorkingStart(weeklySchedule, now.AddSeconds(1)),
            "Today is marked off work; keep OOF active through the next working start.");
    }

    public OofWindow CalculateLongLeaveWindow(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new ArgumentException("Long leave return must be later than its start.", nameof(end));
        }

        return new OofWindow(start, end, "Explicit long-leave interval.");
    }

    public bool IsWithinWorkingHours(IReadOnlyList<ScheduleDay> weeklySchedule, DateTimeOffset now) =>
        TryGetActiveWorkingEnd(weeklySchedule, now, out _);

    private bool TryGetActiveWorkingEnd(
        IReadOnlyList<ScheduleDay> weeklySchedule,
        DateTimeOffset now,
        out DateTimeOffset activeWorkingEnd)
    {
        activeWorkingEnd = default;
        var isActive = false;
        foreach (var date in new[] { now.Date.AddDays(-1), now.Date })
        {
            var day = GetDay(weeklySchedule, date.DayOfWeek);
            if (day is null || day.IsOffWork)
            {
                continue;
            }

            var start = AtLocalTime(date, day.StartTime);
            var end = AtLocalTime(date, day.EndTime);
            if (end <= start)
            {
                end = AtLocalTime(date.AddDays(1), day.EndTime);
            }

            if (now >= start && now <= end && (!isActive || end > activeWorkingEnd))
            {
                activeWorkingEnd = end;
                isActive = true;
            }
        }

        return isActive;
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
