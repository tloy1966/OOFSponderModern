using OOFSponderModern.Models;
using OOFSponderModern.Services;
using OOFSponderModern.ViewModels;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

var tests = new SchedulerServiceTests();
tests.BeforeWorkingHoursStartsAtNow();
tests.DuringWorkingHoursStartsAtWorkdayEnd();
tests.OvernightWorkingHoursContinueAfterMidnight();
tests.OvernightWorkingHoursPreserveDstOffsets();
tests.OffWorkDayStartsAtNow();
tests.AllOffWorkUsesOneWeekWindow();
tests.LongLeaveUsesExplicitWindow();
tests.LongLeaveRejectsInvalidWindow();
tests.LongLeavePreservesDstOffsets();
tests.LinkedEndAdjustmentShiftsStartTime();
tests.NextWorkingStartUsesPostDstOffset();
tests.DefaultSettingsUseNineToSixWeekdays();
tests.DefaultSettingsIncludeNamedMessageTemplates();
tests.DefaultSettingsIncludeValidLongLeaveDraft();
tests.LocalSuggestionDatesAreAlwaysEnglish();
tests.SavedTemplateDatesAreAlwaysEnglish();
tests.MessageTemplateVariablesResolveFromCurrentWindow();
tests.MessageTemplateUnknownVariablesAreReported();
tests.SemanticVersionsSortCorrectly();
tests.SettingsCollectionsRoundTripThroughJson();
tests.SchemaTwoSettingsMigrateWithoutChangingCustomTemplates();
tests.WeeklyScheduleTimesPersistAcrossRestart();
tests.LongLeaveViewModelWorkflowValidatesAndPreservesProfileChoice();
tests.StartupPreferenceTracksWindowsStartupState();
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

    public void OvernightWorkingHoursContinueAfterMidnight()
    {
        var schedule = CreateDefaultSchedule();
        var monday = schedule.Single(day => day.DayOfWeek == DayOfWeek.Monday);
        monday.StartTime = new TimeSpan(18, 0, 0);
        monday.EndTime = new TimeSpan(9, 0, 0);
        schedule.Single(day => day.DayOfWeek == DayOfWeek.Tuesday).IsOffWork = true;
        var beforeMidnight = new DateTimeOffset(2026, 7, 20, 23, 0, 0, TimeSpan.FromHours(8));
        var now = new DateTimeOffset(2026, 7, 21, 1, 0, 0, TimeSpan.FromHours(8));
        var exactEnd = new DateTimeOffset(2026, 7, 21, 9, 0, 0, TimeSpan.FromHours(8));
        var afterEnd = exactEnd.AddSeconds(1);

        var isWorking = _scheduler.IsWithinWorkingHours(schedule, now);
        var window = _scheduler.CalculateNextWindow(schedule, now);

        AssertEqual(true, _scheduler.IsWithinWorkingHours(schedule, beforeMidnight), "Overnight shift active before midnight");
        AssertEqual(true, isWorking, "Overnight shift remains active after midnight");
        AssertEqual(true, _scheduler.IsWithinWorkingHours(schedule, exactEnd), "Overnight shift includes exact end");
        AssertEqual(false, _scheduler.IsWithinWorkingHours(schedule, afterEnd), "Overnight shift ends after exact end");
        AssertEqual(new DateTimeOffset(2026, 7, 21, 9, 0, 0, TimeSpan.FromHours(8)), window.Start, "Overnight OOF start");
        AssertEqual(new DateTimeOffset(2026, 7, 22, 9, 0, 0, TimeSpan.FromHours(8)), window.End, "Overnight next working start");
    }

    public void OvernightWorkingHoursPreserveDstOffsets()
    {
        var pacificTime = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var scheduler = new SchedulerService(pacificTime);
        var schedule = CreateDefaultSchedule(allOffWork: true);
        var saturday = schedule.Single(day => day.DayOfWeek == DayOfWeek.Saturday);
        saturday.IsOffWork = false;
        saturday.StartTime = new TimeSpan(18, 0, 0);
        saturday.EndTime = new TimeSpan(9, 0, 0);
        var monday = schedule.Single(day => day.DayOfWeek == DayOfWeek.Monday);
        monday.IsOffWork = false;
        var now = new DateTimeOffset(2026, 3, 8, 3, 30, 0, TimeSpan.FromHours(-7));

        var window = scheduler.CalculateNextWindow(schedule, now);

        AssertEqual(true, scheduler.IsWithinWorkingHours(schedule, now), "DST overnight shift remains active");
        AssertEqual(new DateTimeOffset(2026, 3, 8, 9, 0, 0, TimeSpan.FromHours(-7)), window.Start, "DST overnight OOF start");
        AssertEqual(new DateTimeOffset(2026, 3, 9, 9, 0, 0, TimeSpan.FromHours(-7)), window.End, "DST overnight next start");
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

    public void LongLeaveUsesExplicitWindow()
    {
        var start = new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.FromHours(8));
        var end = new DateTimeOffset(2026, 8, 21, 9, 0, 0, TimeSpan.FromHours(8));

        var window = _scheduler.CalculateLongLeaveWindow(start, end);

        AssertEqual(start, window.Start, nameof(window.Start));
        AssertEqual(end, window.End, nameof(window.End));
        AssertEqual("Explicit long-leave interval.", window.Reason, nameof(window.Reason));
    }

    public void LongLeaveRejectsInvalidWindow()
    {
        var start = new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.FromHours(8));
        var rejected = false;
        try
        {
            _scheduler.CalculateLongLeaveWindow(start, start);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        AssertEqual(true, rejected, "Invalid explicit interval rejected");
    }

    public void LongLeavePreservesDstOffsets()
    {
        var start = new DateTimeOffset(2026, 3, 6, 18, 0, 0, TimeSpan.FromHours(-8));
        var end = new DateTimeOffset(2026, 3, 16, 9, 0, 0, TimeSpan.FromHours(-7));

        var window = _scheduler.CalculateLongLeaveWindow(start, end);

        AssertEqual(TimeSpan.FromHours(-8), window.Start.Offset, "Long leave start offset");
        AssertEqual(TimeSpan.FromHours(-7), window.End.Offset, "Long leave end offset");
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
        AssertEqual(true, names.Contains("Long Leave"), "Long Leave template");
    }

    public void DefaultSettingsIncludeValidLongLeaveDraft()
    {
        var state = new InMemorySettingsService().LoadAsync().GetAwaiter().GetResult();

        AssertEqual(3, state.SchemaVersion, "Default schema version");
        AssertEqual(true, state.LongLeave.Start > DateTimeOffset.Now.AddMinutes(-1), "Default leave starts in the future");
        AssertEqual(true, state.LongLeave.End > state.LongLeave.Start, "Default leave return follows start");
        AssertEqual("Long leave", state.LongLeave.Label, "Default leave label");
        AssertEqual(ScheduleSource.WeeklySchedule, state.Preferences.SelectedScheduleSource, "Default schedule source");
    }

    public void LocalSuggestionDatesAreAlwaysEnglish()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-TW");
            var window = new OofWindow(
                new DateTimeOffset(2026, 7, 17, 16, 21, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 7, 20, 7, 0, 0, TimeSpan.FromHours(8)),
                "test");
            var request = new OofTemplateRequest(
                window,
                AudienceScope.ContactsOnly,
                TemplateTargetProfile.Primary,
                window.Start);

            var result = new LocalOofTemplateGenerator().GenerateAsync(request).GetAwaiter().GetResult();

            AssertEqual(true, result.InternalTemplate.Contains("Fri, Jul 17 2026 4:21 PM +08:00"), "English suggestion start");
            AssertEqual(true, result.ExternalTemplate.Contains("Mon, Jul 20 2026 7:00 AM +08:00"), "English suggestion return");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    public void SavedTemplateDatesAreAlwaysEnglish()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-TW");
            var window = new OofWindow(
                new DateTimeOffset(2026, 7, 17, 16, 21, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 7, 20, 7, 0, 0, TimeSpan.FromHours(8)),
                "test");
            var template = new MessageTemplate
            {
                InternalTemplate = "{StartDate} {StartTime} until {ReturnDate} {ReturnTime}",
                ExternalTemplate = "Back {ReturnDate}"
            };

            var result = new MessageTemplateRenderer().Render(template, window, "Taylor", window.Start);

            AssertEqual(
                "Friday, July 17, 2026 4:21 PM until Monday, July 20, 2026 7:00 AM",
                result.InternalTemplate,
                "English saved-template dates");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
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
        var state = new AppState
        {
            SchemaVersion = 3,
            LongLeave = new LongLeaveSettings
            {
                Start = new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.FromHours(8)),
                End = new DateTimeOffset(2026, 8, 21, 9, 0, 0, TimeSpan.FromHours(8)),
                Label = "Sabbatical"
            }
        };
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
        AssertEqual("Sabbatical", roundTripped.LongLeave.Label, "Persisted long-leave label");
        AssertEqual(state.LongLeave.End, roundTripped.LongLeave.End, "Persisted long-leave end");
    }

    public void SchemaTwoSettingsMigrateWithoutChangingCustomTemplates()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"OOFSponderModern.Tests-{Guid.NewGuid():N}");
        var settingsPath = Path.Combine(directory, "usersettings.json");
        Directory.CreateDirectory(directory);
        try
        {
            var state = new AppState
            {
                SchemaVersion = 2,
                LongLeave = null!,
                Preferences = new UserPreferences
                {
                    AreDefaultMessageTemplatesInitialized = true,
                    SelectedScheduleSource = ScheduleSource.WeeklySchedule
                }
            };
            state.WeeklySchedule.Add(new ScheduleDay
            {
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(17, 0, 0)
            });
            state.MessageTemplates.Add(new MessageTemplate { Name = "My custom template" });
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(state, options));

            var service = new FileSettingsService(settingsPath);
            var migrated = service.LoadAsync().GetAwaiter().GetResult();

            AssertEqual(3, migrated.SchemaVersion, "Migrated schema version");
            AssertEqual(true, migrated.LongLeave.Start != default, "Migration creates long-leave draft");
            AssertEqual(true, migrated.LongLeave.End > migrated.LongLeave.Start, "Migrated leave interval valid");
            AssertEqual(1, migrated.MessageTemplates.Count, "Migration preserves customized template count");
            AssertEqual("My custom template", migrated.MessageTemplates[0].Name, "Migration preserves custom template");
            AssertEqual(new TimeSpan(8, 0, 0), migrated.WeeklySchedule[0].StartTime, "Migration preserves schedule");

            migrated.Preferences.SelectedScheduleSource = ScheduleSource.LongLeave;
            service.SaveAsync(migrated).GetAwaiter().GetResult();
            var reloaded = service.LoadAsync().GetAwaiter().GetResult();
            AssertEqual(ScheduleSource.LongLeave, reloaded.Preferences.SelectedScheduleSource, "Schedule source persists");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    public void WeeklyScheduleTimesPersistAcrossRestart()
    {
        RunOnStaThread(() =>
        {
            _ = System.Windows.Application.Current ?? new System.Windows.Application();
            var directory = Path.Combine(Path.GetTempPath(), $"OOFSponderModern.Tests-{Guid.NewGuid():N}");
            var settingsPath = Path.Combine(directory, "usersettings.json");
            try
            {
                var firstViewModel = CreateMainViewModel(new FileSettingsService(settingsPath));
                var monday = firstViewModel.ScheduleDays.Single(day => day.Model.DayOfWeek == DayOfWeek.Monday);

                monday.StartTimeText = "08:30";
                monday.EndTimeText = "17:30";

                var restartedViewModel = CreateMainViewModel(new FileSettingsService(settingsPath));
                var reloadedMonday = restartedViewModel.ScheduleDays.Single(day => day.Model.DayOfWeek == DayOfWeek.Monday);
                AssertEqual("08:30", reloadedMonday.StartTimeText, "Reloaded Monday start time");
                AssertEqual("17:30", reloadedMonday.EndTimeText, "Reloaded Monday end time");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
    }

    public void LongLeaveViewModelWorkflowValidatesAndPreservesProfileChoice()
    {
        RunOnStaThread(() =>
        {
            _ = System.Windows.Application.Current ?? new System.Windows.Application();
            var settings = new InMemorySettingsService();
            var state = settings.LoadAsync().GetAwaiter().GetResult();
            state.Preferences.SelectedTemplateTarget = TemplateTargetProfile.Primary;
            state.Preferences.SelectedApplyProfile = TemplateTargetProfile.Primary;
            state.Preferences.HasInitializedLongLeaveProfile = false;

            var viewModel = new OOFSponderModern.ViewModels.MainViewModel(
                settings,
                _scheduler,
                new StubMailboxSettingsClient(),
                new LocalOofTemplateGenerator(),
                new MessageTemplateRenderer(),
                new StubReleaseUpdateService(),
                new StubStartupService());

            var start = DateTime.Today.AddDays(10);
            viewModel.LongLeaveStartDate = start;
            viewModel.LongLeaveStartTimeText = "09:00";
            viewModel.LongLeaveEndDate = start.AddDays(14);
            viewModel.LongLeaveEndTimeText = "09:00";
            viewModel.LongLeaveLabel = "Sabbatical";
            viewModel.SelectedScheduleSourceDisplayName = "Long leave";

            AssertEqual(true, viewModel.IsLongLeaveMode, "Long-leave mode selected");
            AssertEqual(true, viewModel.IsActiveWindowValid, "Explicit interval valid");
            AssertEqual(TemplateTargetProfile.Extended, viewModel.SelectedTemplateTarget, "First switch defaults suggestion profile");
            AssertEqual(TemplateTargetProfile.Extended, viewModel.SelectedApplyProfile, "First switch defaults apply profile");
            AssertEqual(true, viewModel.PreviewText.Contains("Schedule source: Long leave"), "Preview identifies schedule source");
            AssertEqual(true, viewModel.PreviewText.Contains("Local label: Sabbatical"), "Preview includes local label");

            viewModel.GenerateTemplateCommand.Execute(null);
            AssertEqual(true, !string.IsNullOrWhiteSpace(viewModel.GeneratedInternalTemplate), "Long-leave suggestion generated");
            viewModel.LongLeaveEndDate = start.AddDays(15);
            AssertEqual(string.Empty, viewModel.GeneratedInternalTemplate, "Date change invalidates suggestion");

            viewModel.LongLeaveEndDate = viewModel.LongLeaveStartDate;
            viewModel.LongLeaveEndTimeText = viewModel.LongLeaveStartTimeText;
            AssertEqual(false, viewModel.IsActiveWindowValid, "Non-positive interval rejected");
            AssertEqual(false, viewModel.ApplyToM365Command.CanExecute(null), "Invalid interval blocks apply");
            AssertEqual(true, viewModel.LongLeaveStatus.Contains("later"), "Invalid interval explains correction");

            viewModel.LongLeaveEndDate = start.AddDays(14);
            viewModel.LongLeaveEndTimeText = "09:00";
            AssertEqual(true, viewModel.IsActiveWindowValid, "Corrected interval becomes valid");
            viewModel.LongLeaveStartTimeText = "not-a-time";
            AssertEqual(false, viewModel.IsActiveWindowValid, "Invalid start time rejected");
            AssertEqual(false, viewModel.PreviewCommand.CanExecute(null), "Invalid time blocks preview");
            viewModel.LongLeaveStartTimeText = "09:00";

            viewModel.LongLeaveEndTimeText = "not-a-time";
            AssertEqual(false, viewModel.IsActiveWindowValid, "Invalid return time rejected");
            AssertEqual(true, viewModel.LongLeaveStatus.Contains("return date and time"), "Invalid return time explained");
            viewModel.LongLeaveEndTimeText = "09:00";
            viewModel.LongLeaveStartDate = null;
            AssertEqual(false, viewModel.IsActiveWindowValid, "Missing start date rejected");
            viewModel.LongLeaveStartDate = start;

            viewModel.LongLeaveEndDate = start.AddDays(1);
            AssertEqual(true, viewModel.LongLeaveStatus.Contains("Weekly schedule mode"), "Short leave warning shown");
            viewModel.LongLeaveEndDate = start.AddDays(400);
            AssertEqual(true, viewModel.LongLeaveStatus.Contains("exceeds one year"), "Long leave warning shown");
            viewModel.LongLeaveStartDate = DateTime.Today.AddDays(-1);
            viewModel.LongLeaveEndDate = DateTime.Today.AddDays(5);
            AssertEqual(true, viewModel.LongLeaveStatus.Contains("past"), "Past start warning shown");
            viewModel.LongLeaveStartDate = start;
            viewModel.LongLeaveEndDate = start.AddDays(14);

            viewModel.SelectedScheduleSourceDisplayName = "Weekly schedule";
            viewModel.SelectedTemplateTarget = TemplateTargetProfile.Primary;
            viewModel.SelectedApplyProfile = TemplateTargetProfile.Primary;
            viewModel.SelectedScheduleSourceDisplayName = "Long leave";
            AssertEqual(TemplateTargetProfile.Primary, viewModel.SelectedTemplateTarget, "Later switch preserves suggestion profile");
            AssertEqual(TemplateTargetProfile.Primary, viewModel.SelectedApplyProfile, "Later switch preserves apply profile");
        });
    }

    public void StartupPreferenceTracksWindowsStartupState()
    {
        RunOnStaThread(() =>
        {
            _ = System.Windows.Application.Current ?? new System.Windows.Application();
            var settings = new InMemorySettingsService();
            var startup = new StubStartupService(isEnabled: true);
            var viewModel = new OOFSponderModern.ViewModels.MainViewModel(
                settings,
                _scheduler,
                new StubMailboxSettingsClient(),
                new LocalOofTemplateGenerator(),
                new MessageTemplateRenderer(),
                new StubReleaseUpdateService(),
                startup);

            AssertEqual(true, viewModel.StartWithWindows, "Startup state loaded from Windows");
            AssertEqual(true, settings.LoadAsync().Result.Preferences.StartWithWindows, "Startup preference synchronized on load");

            viewModel.StartWithWindows = false;

            AssertEqual(false, startup.IsEnabled, "Windows startup disabled");
            AssertEqual(1, startup.SetEnabledCallCount, "Startup service called once");
            AssertEqual(false, settings.LoadAsync().Result.Preferences.StartWithWindows, "Disabled startup preference stored");
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new InvalidOperationException("STA regression test failed.", failure);
        }
    }

    private MainViewModel CreateMainViewModel(ISettingsService settingsService) =>
        new(
            settingsService,
            _scheduler,
            new StubMailboxSettingsClient(),
            new LocalOofTemplateGenerator(),
            new MessageTemplateRenderer(),
            new StubReleaseUpdateService(),
            new StubStartupService());

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

internal sealed class StubMailboxSettingsClient : IMailboxSettingsClient
{
    public Task<string> ApplyAsync(MailboxSettingsPreview preview, CancellationToken cancellationToken = default) =>
        Task.FromResult("Applied by test stub.");

    public Task<CurrentMailboxSettingsSummary> LoadCurrentSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new CurrentMailboxSettingsSummary("test@example.com", "disabled", "none", "not scheduled", "not scheduled", false, false, 0, 0));
}

internal sealed class StubReleaseUpdateService : IReleaseUpdateService
{
    public Task<ReleaseInformation?> GetLatestReleaseAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<ReleaseInformation?>(null);
}

internal sealed class StubStartupService : IStartupService
{
    public StubStartupService(bool isEnabled = false) => IsEnabled = isEnabled;

    public bool IsEnabled { get; private set; }
    public int SetEnabledCallCount { get; private set; }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        SetEnabledCallCount++;
    }
}