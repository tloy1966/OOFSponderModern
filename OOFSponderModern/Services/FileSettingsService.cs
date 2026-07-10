using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public sealed class FileSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _settingsFilePath;

    public FileSettingsService()
    {
        var settingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OOFSponderModern");
        Directory.CreateDirectory(settingsFolder);
        _settingsFilePath = Path.Combine(settingsFolder, "usersettings.json");
    }

    public Task<AppState> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(_settingsFilePath))
        {
            var defaultState = InMemorySettingsService.CreateDefaultState();
            defaultState.Sync.RecentActivity.Clear();
            defaultState.Sync.RecentActivity.Add("Created default settings file.");
            SaveState(defaultState);
            return Task.FromResult(defaultState);
        }

        try
        {
            var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(_settingsFilePath), JsonOptions)
                ?? InMemorySettingsService.CreateDefaultState();
            EnsureState(state);
            state.Sync.RecentActivity.Clear();
            state.Sync.RecentActivity.Add("Loaded saved settings from AppData.");
            return Task.FromResult(state);
        }
        catch
        {
            var fallbackState = InMemorySettingsService.CreateDefaultState();
            fallbackState.Sync.RecentActivity.Clear();
            fallbackState.Sync.RecentActivity.Add("Settings file could not be loaded; default settings were used.");
            return Task.FromResult(fallbackState);
        }
    }

    public Task SaveAsync(AppState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveState(state);
        return Task.CompletedTask;
    }

    private void SaveState(AppState state)
    {
        EnsureState(state);
        File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(state, JsonOptions));
    }

    private static void EnsureState(AppState state)
    {
        state.Messages ??= new MessageProfile();
        state.Sync ??= new SyncState();
        state.Preferences ??= new UserPreferences();
        state.Updates ??= new UpdateState();
        state.WeeklySchedule ??= new List<ScheduleDay>();
        state.MessageTemplates ??= new List<MessageTemplate>();
        EnsureSchedule(state);
        MigrateState(state);
        EnsureDefaultMessageTemplates(state);
    }

    private static void MigrateState(AppState state)
    {
        if (state.SchemaVersion >= 2)
        {
            return;
        }

        foreach (var template in InMemorySettingsService.CreateDefaultMessageTemplates())
        {
            state.MessageTemplates.Add(template);
        }

        state.Preferences.TemplateDisplayName = string.IsNullOrWhiteSpace(state.Preferences.TemplateDisplayName)
            ? Environment.UserName
            : state.Preferences.TemplateDisplayName;
        state.SchemaVersion = 2;
    }

    private static void EnsureDefaultMessageTemplates(AppState state)
    {
        if (state.Preferences.AreDefaultMessageTemplatesInitialized)
        {
            return;
        }

        if (state.MessageTemplates.Count == 0)
        {
            foreach (var template in InMemorySettingsService.CreateDefaultMessageTemplates())
            {
                state.MessageTemplates.Add(template);
            }
        }

        state.Preferences.AreDefaultMessageTemplatesInitialized = true;
    }

    private static void EnsureSchedule(AppState state)
    {
        if (state.WeeklySchedule.Count > 0)
        {
            return;
        }

        foreach (var day in InMemorySettingsService.CreateDefaultState().WeeklySchedule)
        {
            state.WeeklySchedule.Add(day);
        }
    }
}
