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
                EndTime = new TimeSpan(17, 0, 0),
                IsOffWork = day is DayOfWeek.Saturday or DayOfWeek.Sunday
            });
        }

        state.Sync.RecentActivity.Add("Loaded in-memory sample settings. Future migration target: %AppData%\\OOFSponder\\usersettings.json.");
        state.Sync.RecentActivity.Add("Microsoft 365 apply mode enabled. Applying will update mailbox automatic replies.");
        return state;
    }
}
