using OOFSponderModern.Models;
using OOFSponderModern.Services;

var tests = new SchedulerServiceTests();
tests.BeforeWorkingHoursStartsAtNow();
tests.DuringWorkingHoursStartsAtWorkdayEnd();
tests.OffWorkDayStartsAtNow();
tests.AllOffWorkUsesOneWeekWindow();
tests.LinkedEndAdjustmentShiftsStartTime();
tests.NextWorkingStartUsesPostDstOffset();
tests.DefaultSettingsUseNineToSixWeekdays();
Console.WriteLine("OOFSponderModern scheduler tests passed.");

internal sealed class SchedulerServiceTests
{
    private static readonly TimeZoneInfo FixedUtcPlusEight = TimeZoneInfo.CreateCustomTimeZone(
        "OOFSponderTests-UTC+8",
        TimeSpan.FromHours(8),
        "OOFSponder Tests UTC+8",
        "OOFSponder Tests UTC+8");
    private readonly SchedulerService _scheduler = new(FixedUtcPlusEight);

    public void BeforeWorkingHoursStartsAtNow()
    {
        var now = new DateTimeOffset(2026, 7, 10, 7, 30, 0, TimeSpan.FromHours(8));
        var window = _scheduler.CalculateNextWindow(CreateDefaultSchedule(), now);

        AssertEqual(now, window.Start, nameof(window.Start));
        AssertEqual(new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.FromHours(8)), window.End, nameof(window.End));
    }

    public void DuringWorkingHoursStartsAtWorkdayEnd()
    {
        var now = new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.FromHours(8));
        var window = _scheduler.CalculateNextWindow(CreateDefaultSchedule(), now);

        AssertEqual(new DateTimeOffset(2026, 7, 10, 18, 0, 0, TimeSpan.FromHours(8)), window.Start, nameof(window.Start));
        AssertEqual(new DateTimeOffset(2026, 7, 13, 9, 0, 0, TimeSpan.FromHours(8)), window.End, nameof(window.End));
    }

    public void OffWorkDayStartsAtNow()
    {
        var now = new DateTimeOffset(2026, 7, 11, 10, 0, 0, TimeSpan.FromHours(8));
        var window = _scheduler.CalculateNextWindow(CreateDefaultSchedule(), now);

        AssertEqual(now, window.Start, nameof(window.Start));
        AssertEqual(new DateTimeOffset(2026, 7, 13, 9, 0, 0, TimeSpan.FromHours(8)), window.End, nameof(window.End));
    }

    public void AllOffWorkUsesOneWeekWindow()
    {
        var now = new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.FromHours(8));
        var window = _scheduler.CalculateNextWindow(CreateDefaultSchedule(allOffWork: true), now);

        AssertEqual(now, window.Start, nameof(window.Start));
        AssertEqual(now.AddDays(7), window.End, nameof(window.End));
    }

    public void LinkedEndAdjustmentShiftsStartTime()
    {
        var model = new ScheduleDay
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(18, 0, 0)
        };
        var viewModel = new OOFSponderModern.ViewModels.ScheduleDayViewModel(model, () => { }, () => true);

        viewModel.MoveEndLaterCommand.Execute(null);

        AssertEqual("09:30", viewModel.StartTimeText, nameof(viewModel.StartTimeText));
        AssertEqual("18:30", viewModel.EndTimeText, nameof(viewModel.EndTimeText));
    }

    public void NextWorkingStartUsesPostDstOffset()
    {
        var pacificTime = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var scheduler = new SchedulerService(pacificTime);
        var now = new DateTimeOffset(2026, 3, 6, 10, 0, 0, TimeSpan.FromHours(-8));

        var window = scheduler.CalculateNextWindow(CreateDefaultSchedule(), now);

        AssertEqual(new DateTimeOffset(2026, 3, 6, 18, 0, 0, TimeSpan.FromHours(-8)), window.Start, nameof(window.Start));
        AssertEqual(new DateTimeOffset(2026, 3, 9, 9, 0, 0, TimeSpan.FromHours(-7)), window.End, nameof(window.End));
    }

    public void DefaultSettingsUseNineToSixWeekdays()
    {
        var state = new InMemorySettingsService().LoadAsync().GetAwaiter().GetResult();
        var weekdays = state.WeeklySchedule.Where(day => day.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday);

        foreach (var weekday in weekdays)
        {
            AssertEqual(new TimeSpan(9, 0, 0), weekday.StartTime, $"{weekday.DayOfWeek} start");
            AssertEqual(new TimeSpan(18, 0, 0), weekday.EndTime, $"{weekday.DayOfWeek} end");
            AssertEqual(false, weekday.IsOffWork, $"{weekday.DayOfWeek} off-work flag");
        }
    }

    private static List<ScheduleDay> CreateDefaultSchedule(bool allOffWork = false)
    {
        return Enum.GetValues<DayOfWeek>()
            .Select(day => new ScheduleDay
            {
                DayOfWeek = day,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(18, 0, 0),
                IsOffWork = allOffWork || day is DayOfWeek.Saturday or DayOfWeek.Sunday
            })
            .ToList();
    }

    private static void AssertEqual<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{name}: expected {expected}, actual {actual}");
        }
    }
}