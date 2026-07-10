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

    public async Task<AppState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFilePath))
        {
            var defaultState = InMemorySettingsService.CreateDefaultState();
            defaultState.Sync.RecentActivity.Clear();
            defaultState.Sync.RecentActivity.Add("Created default settings file.");
            await SaveAsync(defaultState, cancellationToken);
            return defaultState;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsFilePath);
            var state = await JsonSerializer.DeserializeAsync<AppState>(stream, JsonOptions, cancellationToken)
                ?? InMemorySettingsService.CreateDefaultState();
            EnsureState(state);
            state.Sync.RecentActivity.Clear();
            state.Sync.RecentActivity.Add("Loaded saved settings from AppData.");
            return state;
        }
        catch
        {
            var fallbackState = InMemorySettingsService.CreateDefaultState();
            fallbackState.Sync.RecentActivity.Clear();
            fallbackState.Sync.RecentActivity.Add("Settings file could not be loaded; default settings were used.");
            return fallbackState;
        }
    }

    public async Task SaveAsync(AppState state, CancellationToken cancellationToken = default)
    {
        EnsureState(state);
        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
    }

    private static void EnsureState(AppState state)
    {
        state.Messages ??= new MessageProfile();
        state.Sync ??= new SyncState();
        state.Preferences ??= new UserPreferences();
        EnsureSchedule(state);
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
