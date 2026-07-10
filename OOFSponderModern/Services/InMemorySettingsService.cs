using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public sealed class InMemorySettingsService : ISettingsService
{
    private AppState? _state;

    public Task<AppState> LoadAsync(CancellationToken cancellationToken = default)
    {
        _state ??= CreateDefaultState();
        return Task.FromResult(_state);
    }

    public Task SaveAsync(AppState state, CancellationToken cancellationToken = default)
    {
        _state = state;
        return Task.CompletedTask;
    }

    internal static AppState CreateDefaultState()
    {
        var state = new AppState
        {
            SchemaVersion = 2,
            Messages = new MessageProfile
            {
                PrimaryInternalMessage = "I am outside normal working hours and will respond when I return.",
                PrimaryExternalMessage = "Thank you for your message. I am currently out of office and will reply when available.",
                ExtendedInternalMessage = "I am away for an extended period. Please contact my backup for urgent items.",
                ExtendedExternalMessage = "Thank you for your message. I am away for an extended period and will respond after I return.",
                AudienceScope = AudienceScope.ContactsOnly
            },
            Sync = new SyncState
            {
                AuthState = "Not connected",
                SyncStatus = "Ready to apply to Microsoft 365",
                IsMockMode = false
            }
        };

        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            state.WeeklySchedule.Add(new ScheduleDay
            {
                DayOfWeek = day,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(18, 0, 0),
                IsOffWork = day is DayOfWeek.Saturday or DayOfWeek.Sunday
            });
        }

        foreach (var template in CreateDefaultMessageTemplates())
        {
            state.MessageTemplates.Add(template);
        }
        state.Preferences.AreDefaultMessageTemplatesInitialized = true;

        state.Sync.RecentActivity.Add("Loaded in-memory sample settings. Future migration target: %AppData%\\OOFSponder\\usersettings.json.");
        state.Sync.RecentActivity.Add("Microsoft 365 apply mode enabled. Applying will update mailbox automatic replies.");
        return state;
    }

    internal static IReadOnlyList<MessageTemplate> CreateDefaultMessageTemplates() =>
    [
        new MessageTemplate
        {
            Name = "Vacation",
            InternalTemplate = "Hi, I am on vacation from {StartDate} until {ReturnDate}. I will respond after I return. For urgent matters, please contact the team.",
            ExternalTemplate = "Thank you for your message. I am on vacation until {ReturnDate} and will respond after I return."
        },
        new MessageTemplate
        {
            Name = "Weekend",
            InternalTemplate = "I am outside normal working hours and will return on {ReturnDate} at {ReturnTime}.",
            ExternalTemplate = "Thank you for your message. I am currently out of office and will return on {ReturnDate}."
        },
        new MessageTemplate
        {
            Name = "Holiday",
            InternalTemplate = "Our office is closed for a holiday. I will return on {ReturnDate} at {ReturnTime}.",
            ExternalTemplate = "Thank you for your message. Our office is closed for a holiday, and I will respond after {ReturnDate}."
        },
        new MessageTemplate
        {
            Name = "Business Travel",
            InternalTemplate = "Hi, I am traveling for business from {StartDate} to {ReturnDate}. Responses may be delayed during this {Duration} period.",
            ExternalTemplate = "Thank you for your message. I am traveling for business and may have limited availability until {ReturnDate}."
        }
    ];
}
