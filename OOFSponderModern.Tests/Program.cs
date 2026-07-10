using OOFSponderModern.Models;
using OOFSponderModern.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

var tests = new SchedulerServiceTests();
tests.BeforeWorkingHoursStartsAtNow();
tests.DuringWorkingHoursStartsAtWorkdayEnd();
tests.OffWorkDayStartsAtNow();
tests.AllOffWorkUsesOneWeekWindow();
tests.LinkedEndAdjustmentShiftsStartTime();
tests.NextWorkingStartUsesPostDstOffset();
tests.DefaultSettingsUseNineToSixWeekdays();
tests.DefaultSettingsIncludeNamedMessageTemplates();
tests.MessageTemplateVariablesResolveFromCurrentWindow();
tests.MessageTemplateUnknownVariablesAreReported();
tests.SemanticVersionsSortCorrectly();
tests.SettingsCollectionsRoundTripThroughJson();
Console.WriteLine("OOFSponderModern regression tests passed.");

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

    public void DefaultSettingsIncludeNamedMessageTemplates()
    {
        var state = new InMemorySettingsService().LoadAsync().GetAwaiter().GetResult();
        var names = state.MessageTemplates.Select(template => template.Name).ToArray();

        AssertEqual(true, names.Contains("Vacation"), "Vacation template");
        AssertEqual(true, names.Contains("Weekend"), "Weekend template");
        AssertEqual(true, names.Contains("Holiday"), "Holiday template");
        AssertEqual(true, names.Contains("Business Travel"), "Business Travel template");
    }

    public void MessageTemplateVariablesResolveFromCurrentWindow()
    {
        var window = new OofWindow(
            new DateTimeOffset(2026, 7, 10, 18, 0, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 7, 13, 9, 0, 0, TimeSpan.FromHours(8)),
            "test");
        var template = new MessageTemplate
        {
            Name = "Test",
            InternalTemplate = "{UserName}: {StartDate} {StartTime} to {ReturnDate} {ReturnTime} ({Duration})",
            ExternalTemplate = "Back {ReturnDate}"
        };

        var result = new MessageTemplateRenderer().Render(template, window, "Taylor", window.Start);

        AssertEqual(true, result.InternalTemplate.Contains("Taylor"), "Resolved user name");
        AssertEqual(true, result.InternalTemplate.Contains("2d 15h"), "Resolved duration");
        AssertEqual(false, result.InternalTemplate.Contains("{ReturnDate}"), "Resolved return date token");
    }

    public void MessageTemplateUnknownVariablesAreReported()
    {
        var template = new MessageTemplate
        {
            InternalTemplate = "Known {UserName}; unknown {ManagerName} and {managername}.",
            ExternalTemplate = "{Unsupported}"
        };

        var unknown = MessageTemplateRenderer.FindUnknownVariables(template);

        AssertEqual(2, unknown.Count, "Unknown variable count");
        AssertEqual(true, unknown.Contains("ManagerName", StringComparer.OrdinalIgnoreCase), "ManagerName warning");
        AssertEqual(true, unknown.Contains("Unsupported", StringComparer.OrdinalIgnoreCase), "Unsupported warning");
    }

    public void SemanticVersionsSortCorrectly()
    {
        AssertEqual(true, SemanticVersion.TryParse("v0.10.0", out var newer), "Parse v0.10.0");
        AssertEqual(true, SemanticVersion.TryParse("0.9.0", out var older), "Parse 0.9.0");
        AssertEqual(true, SemanticVersion.TryParse("0.10.0-beta.1", out var prerelease), "Parse prerelease");
        AssertEqual(true, newer.CompareTo(older) > 0, "0.10.0 greater than 0.9.0");
        AssertEqual(true, newer.CompareTo(prerelease) > 0, "Stable greater than prerelease");
        AssertEqual(false, SemanticVersion.TryParse("release-one", out _), "Reject invalid version");
    }

    public void SettingsCollectionsRoundTripThroughJson()
    {
        var state = new AppState { SchemaVersion = 2 };
        state.WeeklySchedule.Add(new ScheduleDay
        {
            DayOfWeek = DayOfWeek.Wednesday,
            StartTime = new TimeSpan(7, 30, 0),
            EndTime = new TimeSpan(16, 30, 0)
        });
        state.MessageTemplates.Add(new MessageTemplate { Name = "Custom template" });
        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };

        var roundTripped = JsonSerializer.Deserialize<AppState>(JsonSerializer.Serialize(state, options), options)
            ?? throw new InvalidOperationException("Settings round trip returned null.");

        AssertEqual(1, roundTripped.WeeklySchedule.Count, "Persisted schedule count");
        AssertEqual(new TimeSpan(7, 30, 0), roundTripped.WeeklySchedule[0].StartTime, "Persisted custom start");
        AssertEqual(1, roundTripped.MessageTemplates.Count, "Persisted template count");
        AssertEqual("Custom template", roundTripped.MessageTemplates[0].Name, "Persisted template name");
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