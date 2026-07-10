using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Media;
using OOFSponderModern.Models;
using OOFSponderModern.Services;

namespace OOFSponderModern.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ISchedulerService _scheduler;
    private readonly IMailboxSettingsClient _mailboxClient;
    private readonly IOofTemplateGenerator _templateGenerator;
    private readonly ISettingsService _settingsService;
    private readonly AppState _state;
    private CancellationTokenSource? _saveSettingsDebounce;
    private OofWindow _currentWindow;
    private GeneratedOofTemplateResult? _generatedTemplate;
    private string _currentOofMode = string.Empty;
    private string _nextOofWindowText = string.Empty;
    private string _syncStatus = string.Empty;
    private string _authState = string.Empty;
    private string _scheduleStatus = string.Empty;
    private AudienceScope _selectedAudienceScope;
    private TemplateTargetProfile _selectedTemplateTarget = TemplateTargetProfile.Primary;
    private TemplateTargetProfile _selectedApplyProfile = TemplateTargetProfile.Primary;
    private string _generatedInternalTemplate = string.Empty;
    private string _generatedExternalTemplate = string.Empty;
    private string _templateStatus = string.Empty;
    private string _primaryInternalMessage = string.Empty;
    private string _primaryExternalMessage = string.Empty;
    private string _extendedInternalMessage = string.Empty;
    private string _extendedExternalMessage = string.Empty;
    private string _previewText = string.Empty;
    private string _applyResultText = string.Empty;
    private string _currentM365SettingsText = "Current Microsoft 365 automatic reply settings have not been loaded yet.";
    private bool _isDarkMode;
    private bool _isLinkedTimeAdjustmentEnabled = true;
    private bool _isApplyResultVisible;
    private bool _isOnboardingVisible;
    private ThemePalette _selectedThemePalette = ThemePalette.ProductivityBlue;

    public MainViewModel(
        ISettingsService settingsService,
        ISchedulerService scheduler,
        IMailboxSettingsClient mailboxClient,
        IOofTemplateGenerator templateGenerator)
    {
        _settingsService = settingsService;
        _scheduler = scheduler;
        _mailboxClient = mailboxClient;
        _templateGenerator = templateGenerator;
        _state = settingsService.LoadAsync().GetAwaiter().GetResult();
        _currentWindow = new OofWindow(DateTimeOffset.Now, DateTimeOffset.Now, "Not calculated yet.");

        ScheduleDays = new ObservableCollection<ScheduleDayViewModel>(
            _state.WeeklySchedule
                .OrderBy(day => (int)day.DayOfWeek)
                .Select(day => new ScheduleDayViewModel(day, RecalculateWindow, () => IsLinkedTimeAdjustmentEnabled)));

        RecentActivity = new ObservableCollection<string>(_state.Sync.RecentActivity);
        AudienceScopes = Enum.GetValues<AudienceScope>();
        TemplateTargets = Enum.GetValues<TemplateTargetProfile>();
        ThemePalettes = Enum.GetValues<ThemePalette>();

        _selectedAudienceScope = _state.Messages.AudienceScope;
    _selectedTemplateTarget = _state.Preferences.SelectedTemplateTarget;
    _selectedApplyProfile = _state.Preferences.SelectedApplyProfile;
    _isDarkMode = _state.Preferences.IsDarkMode;
    _isLinkedTimeAdjustmentEnabled = _state.Preferences.IsLinkedTimeAdjustmentEnabled;
    _isOnboardingVisible = !_state.Preferences.IsOnboardingDismissed;
    _selectedThemePalette = _state.Preferences.ThemePalette;
        _primaryInternalMessage = _state.Messages.PrimaryInternalMessage;
        _primaryExternalMessage = _state.Messages.PrimaryExternalMessage;
        _extendedInternalMessage = _state.Messages.ExtendedInternalMessage;
        _extendedExternalMessage = _state.Messages.ExtendedExternalMessage;
        _syncStatus = _state.Sync.SyncStatus;
        _authState = _state.Sync.AuthState;
        _templateStatus = "Local message suggestion generator ready.";

        PreviewCommand = new RelayCommand(() =>
        {
            PreviewText = BuildPreviewText(BuildPreview());
            AddActivity("Generated local preview payload. Message bodies omitted from diagnostics.");
            return Task.CompletedTask;
        });
        ApplyToM365Command = new RelayCommand(ApplyToM365Async);
        LoadCurrentM365SettingsCommand = new RelayCommand(LoadCurrentM365SettingsAsync);
        DismissOnboardingCommand = new RelayCommand(DismissOnboardingAsync);
        ClearApplyResultCommand = new RelayCommand(ClearApplyResultAsync);
        GenerateTemplateCommand = new RelayCommand(GenerateTemplateAsync);
        ApplyGeneratedTemplateCommand = new RelayCommand(
            ApplyGeneratedTemplateAsync,
            () => _generatedTemplate is not null);
        ToggleThemeCommand = new RelayCommand(ToggleThemeAsync);
        SelectProductivityBlueCommand = new RelayCommand(() => SelectThemePaletteAsync(ThemePalette.ProductivityBlue));
        SelectTrustNavyCommand = new RelayCommand(() => SelectThemePaletteAsync(ThemePalette.TrustNavy));
        SelectTealMintCommand = new RelayCommand(() => SelectThemePaletteAsync(ThemePalette.TealMint));
        SelectPremiumGoldCommand = new RelayCommand(() => SelectThemePaletteAsync(ThemePalette.PremiumGold));

        RecalculateWindow();
        PreviewText = BuildPreviewText(BuildPreview());
        ApplyTheme(IsDarkMode, SelectedThemePalette);
        OnPropertyChanged(nameof(ThemeModeText));
        OnPropertyChanged(nameof(ToggleThemeText));
        OnPropertyChanged(nameof(LinkedTimeAdjustmentText));
        OnPropertyChanged(nameof(ThemePaletteText));
        OnPropertyChanged(nameof(ThemePaletteDescription));
        OnPalettePreviewChanged();
    }

    public ObservableCollection<ScheduleDayViewModel> ScheduleDays { get; }
    public ObservableCollection<string> RecentActivity { get; }
    public IReadOnlyList<AudienceScope> AudienceScopes { get; }
    public IReadOnlyList<TemplateTargetProfile> TemplateTargets { get; }
    public IReadOnlyList<ThemePalette> ThemePalettes { get; }
    public RelayCommand PreviewCommand { get; }
    public RelayCommand ApplyToM365Command { get; }
    public RelayCommand LoadCurrentM365SettingsCommand { get; }
    public RelayCommand DismissOnboardingCommand { get; }
    public RelayCommand ClearApplyResultCommand { get; }
    public RelayCommand GenerateTemplateCommand { get; }
    public RelayCommand ApplyGeneratedTemplateCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand SelectProductivityBlueCommand { get; }
    public RelayCommand SelectTrustNavyCommand { get; }
    public RelayCommand SelectTealMintCommand { get; }
    public RelayCommand SelectPremiumGoldCommand { get; }

    public string ThemeModeText => _isDarkMode ? "Dark mode" : "Light mode";
    public string ToggleThemeText => _isDarkMode ? "Use light mode" : "Use dark mode";
    public string ThemePaletteText => SelectedThemePalette switch
    {
        ThemePalette.ProductivityBlue => "Productivity Blue",
        ThemePalette.TrustNavy => "Trust Navy",
        ThemePalette.TealMint => "Teal Mint",
        ThemePalette.PremiumGold => "Premium Gold",
        _ => SelectedThemePalette.ToString()
    };

    public string ThemePaletteDescription => SelectedThemePalette switch
    {
        ThemePalette.ProductivityBlue => "Balanced blue palette for quiet productivity workflows.",
        ThemePalette.TrustNavy => "Navy-led palette for trust and operational clarity.",
        ThemePalette.TealMint => "Teal and mint palette for calm retention-focused workflows.",
        ThemePalette.PremiumGold => "Navy and gold palette for a more premium, formal feel.",
        _ => "Selected app color template."
    };

    public System.Windows.Media.Brush PalettePreviewBrush1 => CreateBrush(GetPaletteColors(IsDarkMode, SelectedThemePalette).Accent);
    public System.Windows.Media.Brush PalettePreviewBrush2 => CreateBrush(GetPaletteColors(IsDarkMode, SelectedThemePalette).Success);
    public System.Windows.Media.Brush PalettePreviewBrush3 => CreateBrush(GetPaletteColors(IsDarkMode, SelectedThemePalette).AccentSoft);
    public System.Windows.Media.Brush PalettePreviewBrush4 => CreateBrush(GetPaletteColors(IsDarkMode, SelectedThemePalette).PanelSubtle);
    public string LinkedTimeAdjustmentText => IsLinkedTimeAdjustmentEnabled
        ? "Linked time adjustment is on: changing start time shifts end time."
        : "Linked time adjustment is off: start and end time change independently.";

    public string CurrentOofMode
    {
        get => _currentOofMode;
        private set => SetProperty(ref _currentOofMode, value);
    }

    public string NextOofWindowText
    {
        get => _nextOofWindowText;
        private set => SetProperty(ref _nextOofWindowText, value);
    }

    public string SyncStatus
    {
        get => _syncStatus;
        private set => SetProperty(ref _syncStatus, value);
    }

    public string AuthState
    {
        get => _authState;
        private set => SetProperty(ref _authState, value);
    }

    public string ScheduleStatus
    {
        get => _scheduleStatus;
        private set => SetProperty(ref _scheduleStatus, value);
    }

    public string AudienceScopeText => SelectedAudienceScope.ToString();
    public string TemplateTargetText => SelectedTemplateTarget.ToString();
    public string ApplyProfileText => SelectedApplyProfile.ToString();
    public string AudienceScopeDescription => SelectedAudienceScope switch
    {
        AudienceScope.None => "Internal users receive the internal reply. No external automatic reply is sent.",
        AudienceScope.ContactsOnly => "Internal users receive the internal reply. External replies are sent only to people in your contacts.",
        AudienceScope.AllExternal => "Internal users receive the internal reply. External replies are sent to all external senders.",
        _ => "Internal users receive the internal reply. External reply behavior follows the selected audience scope."
    };

    public bool IsDarkMode
    {
        get => _isDarkMode;
        private set
        {
            if (!SetProperty(ref _isDarkMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ThemeModeText));
            OnPropertyChanged(nameof(ToggleThemeText));
        }
    }

    public bool IsLinkedTimeAdjustmentEnabled
    {
        get => _isLinkedTimeAdjustmentEnabled;
        set
        {
            if (!SetProperty(ref _isLinkedTimeAdjustmentEnabled, value))
            {
                return;
            }

            OnPropertyChanged(nameof(LinkedTimeAdjustmentText));
            _state.Preferences.IsLinkedTimeAdjustmentEnabled = value;
            QueueSaveSettings();
            AddActivity(value
                ? "Linked time adjustment enabled. Start time changes will shift end time."
                : "Linked time adjustment disabled. Start and end time can be adjusted independently.");
        }
    }

    public ThemePalette SelectedThemePalette
    {
        get => _selectedThemePalette;
        set
        {
            if (!SetProperty(ref _selectedThemePalette, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ThemePaletteText));
            OnPropertyChanged(nameof(ThemePaletteDescription));
            OnPalettePreviewChanged();
            _state.Preferences.ThemePalette = value;
            ApplyTheme(IsDarkMode, SelectedThemePalette);
            QueueSaveSettings();
            AddActivity($"Applied {ThemePaletteText} color template.");
        }
    }

    public AudienceScope SelectedAudienceScope
    {
        get => _selectedAudienceScope;
        set
        {
            if (!SetProperty(ref _selectedAudienceScope, value))
            {
                return;
            }

            _state.Messages.AudienceScope = value;
            OnPropertyChanged(nameof(AudienceScopeText));
            OnPropertyChanged(nameof(AudienceScopeDescription));
            PreviewText = BuildPreviewText(BuildPreview());
            QueueSaveSettings();
            MarkGeneratedTemplateStale("Audience scope changed; regenerate the template preview before applying.");
        }
    }

    public TemplateTargetProfile SelectedTemplateTarget
    {
        get => _selectedTemplateTarget;
        set
        {
            if (!SetProperty(ref _selectedTemplateTarget, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TemplateTargetText));
            _state.Preferences.SelectedTemplateTarget = value;
            QueueSaveSettings();
            MarkGeneratedTemplateStale("Target profile changed; regenerate the suggestion before applying.");
        }
    }

    public TemplateTargetProfile SelectedApplyProfile
    {
        get => _selectedApplyProfile;
        set
        {
            if (!SetProperty(ref _selectedApplyProfile, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ApplyProfileText));
            _state.Preferences.SelectedApplyProfile = value;
            PreviewText = BuildPreviewText(BuildPreview());
            QueueSaveSettings();
            AddActivity($"Selected {value} profile for Microsoft 365 apply. Message bodies omitted.");
        }
    }

    public string GeneratedInternalTemplate
    {
        get => _generatedInternalTemplate;
        private set => SetProperty(ref _generatedInternalTemplate, value);
    }

    public string GeneratedExternalTemplate
    {
        get => _generatedExternalTemplate;
        private set => SetProperty(ref _generatedExternalTemplate, value);
    }

    public string TemplateStatus
    {
        get => _templateStatus;
        private set => SetProperty(ref _templateStatus, value);
    }

    public string PrimaryInternalMessage
    {
        get => _primaryInternalMessage;
        set
        {
            if (SetProperty(ref _primaryInternalMessage, value))
            {
                _state.Messages.PrimaryInternalMessage = value;
                QueueSaveSettings();
            }
        }
    }

    public string PrimaryExternalMessage
    {
        get => _primaryExternalMessage;
        set
        {
            if (SetProperty(ref _primaryExternalMessage, value))
            {
                _state.Messages.PrimaryExternalMessage = value;
                QueueSaveSettings();
            }
        }
    }

    public string ExtendedInternalMessage
    {
        get => _extendedInternalMessage;
        set
        {
            if (SetProperty(ref _extendedInternalMessage, value))
            {
                _state.Messages.ExtendedInternalMessage = value;
                QueueSaveSettings();
            }
        }
    }

    public string ExtendedExternalMessage
    {
        get => _extendedExternalMessage;
        set
        {
            if (SetProperty(ref _extendedExternalMessage, value))
            {
                _state.Messages.ExtendedExternalMessage = value;
                QueueSaveSettings();
            }
        }
    }

    public string PreviewText
    {
        get => _previewText;
        private set => SetProperty(ref _previewText, value);
    }

    public string ApplyResultText
    {
        get => _applyResultText;
        private set => SetProperty(ref _applyResultText, value);
    }

    public bool IsApplyResultVisible
    {
        get => _isApplyResultVisible;
        private set => SetProperty(ref _isApplyResultVisible, value);
    }

    public string CurrentM365SettingsText
    {
        get => _currentM365SettingsText;
        private set => SetProperty(ref _currentM365SettingsText, value);
    }

    public bool IsOnboardingVisible
    {
        get => _isOnboardingVisible;
        private set => SetProperty(ref _isOnboardingVisible, value);
    }

    public void RestoreWindowPlacement(Window window)
    {
        var preferences = _state.Preferences;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (preferences.WindowWidth is null || preferences.WindowHeight is null)
        {
            return;
        }

        window.Width = Math.Max(window.MinWidth, preferences.WindowWidth.Value);
        window.Height = Math.Max(window.MinHeight, preferences.WindowHeight.Value);
    }

    public void SaveWindowPlacement(Window window)
    {
        var preferences = _state.Preferences;
        var restoreBounds = window.WindowState == WindowState.Maximized
            ? window.RestoreBounds
            : new Rect(window.Left, window.Top, window.Width, window.Height);

        preferences.WindowWidth = restoreBounds.Width;
        preferences.WindowHeight = restoreBounds.Height;

        SaveSettingsNow();
    }

    private void RecalculateWindow()
    {
        var now = DateTimeOffset.Now;
        _currentWindow = _scheduler.CalculateNextWindow(_state.WeeklySchedule.ToList(), now);
        var inWorkingHours = _scheduler.IsWithinWorkingHours(_state.WeeklySchedule.ToList(), now);
        CurrentOofMode = inWorkingHours ? "Working hours — scheduled OOF preview" : "OOF preview window active";
        NextOofWindowText = $"{_currentWindow.Start:g} → {_currentWindow.End:g}";
        ScheduleStatus = $"Schedule recalculated at {DateTimeOffset.Now:t}";
        OnPropertyChanged(nameof(AudienceScopeText));
        PreviewText = BuildPreviewText(BuildPreview());
        QueueSaveSettings();
        MarkGeneratedTemplateStale("Schedule changed; regenerate the template preview before applying.");
    }

    private async Task GenerateTemplateAsync()
    {
        var request = new OofTemplateRequest(
            _currentWindow,
            SelectedAudienceScope,
            SelectedTemplateTarget,
            DateTimeOffset.Now);

        TemplateStatus = "Generating local message suggestion...";
        _generatedTemplate = null;
        GeneratedInternalTemplate = string.Empty;
        GeneratedExternalTemplate = string.Empty;
        ApplyGeneratedTemplateCommand.RaiseCanExecuteChanged();

        try
        {
            var result = await _templateGenerator.GenerateAsync(request);

            _generatedTemplate = result;
            GeneratedInternalTemplate = result.InternalTemplate;
            GeneratedExternalTemplate = result.ExternalTemplate;
            TemplateStatus = result.IsLocalPreview
                ? $"{result.ProviderName}: local suggestion generated. No external service was called."
                : $"{result.ProviderName}: suggestion generated.";
            AddActivity($"Generated local message suggestion for {SelectedTemplateTarget} profile. Message bodies omitted.");
        }
        catch (Exception ex)
        {
            _generatedTemplate = null;
            TemplateStatus = $"Template generation failed ({ex.GetType().Name}). No message bodies were logged.";
            AddActivity("Template generation failed. Message bodies omitted.");
        }
        finally
        {
            ApplyGeneratedTemplateCommand.RaiseCanExecuteChanged();
        }
    }

    private Task ApplyGeneratedTemplateAsync()
    {
        if (_generatedTemplate is null)
        {
            return Task.CompletedTask;
        }

        if (SelectedTemplateTarget == TemplateTargetProfile.Primary)
        {
            PrimaryInternalMessage = _generatedTemplate.InternalTemplate;
            PrimaryExternalMessage = _generatedTemplate.ExternalTemplate;
        }
        else
        {
            ExtendedInternalMessage = _generatedTemplate.InternalTemplate;
            ExtendedExternalMessage = _generatedTemplate.ExternalTemplate;
        }

        PreviewText = BuildPreviewText(BuildPreview());
    QueueSaveSettings();
        AddActivity($"Applied generated message suggestion to {SelectedTemplateTarget} profile. Message bodies omitted.");
        return Task.CompletedTask;
    }

    private async Task ApplyToM365Async()
    {
        var preview = BuildPreview();
        var confirmation = System.Windows.MessageBox.Show(
            BuildApplyReviewText(preview),
            "Review Microsoft 365 update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            AddActivity("Microsoft 365 apply canceled before sign-in. Message bodies omitted.");
            ShowApplyResult("Apply canceled. No Microsoft 365 mailbox changes were sent.");
            return;
        }

        SyncStatus = "Applying to Microsoft 365";
        AuthState = "Connecting to Microsoft 365";
        ShowApplyResult("Applying automatic replies to Microsoft 365...");
        AddActivity("Started Microsoft 365 apply. Message bodies omitted from diagnostics.");

        try
        {
            var result = await _mailboxClient.PreviewApplyAsync(preview);
            SyncStatus = "Microsoft 365 apply complete";
            AuthState = "Connected";
            AddActivity(result);
            PreviewText = BuildPreviewText(preview);
            ShowApplyResult($"Applied to Microsoft 365. {preview.ActiveProfile} profile scheduled from {preview.Window.Start:g} to {preview.Window.End:g}.");
        }
        catch (Exception ex)
        {
            SyncStatus = "Microsoft 365 apply failed";
            AuthState = "Connection or Graph update failed";
            var safeError = ToUserSafeError(ex);
            AddActivity($"Microsoft 365 apply failed ({ex.GetType().Name}): {safeError} Message bodies omitted.");
            ShowApplyResult($"Microsoft 365 apply failed: {safeError}");
        }
    }

    private async Task LoadCurrentM365SettingsAsync()
    {
        SyncStatus = "Loading current Microsoft 365 automatic replies";
        AuthState = "Connecting to Microsoft 365";
        CurrentM365SettingsText = "Loading current mailbox settings from Microsoft Graph...";
        AddActivity("Started loading current Microsoft 365 automatic reply settings.");

        try
        {
            var summary = await _mailboxClient.LoadCurrentSettingsAsync();
            CurrentM365SettingsText = BuildCurrentSettingsText(summary);
            SyncStatus = "Current Microsoft 365 settings loaded";
            AuthState = "Connected";
            AddActivity($"Loaded current Microsoft 365 automatic reply settings for {summary.MailboxUser}. Message bodies omitted.");
        }
        catch (Exception ex)
        {
            var safeError = ToUserSafeError(ex);
            CurrentM365SettingsText = $"Could not load current Microsoft 365 automatic reply settings. {safeError}";
            SyncStatus = "Load current settings failed";
            AuthState = "Connection or Graph read failed";
            AddActivity($"Load current Microsoft 365 settings failed ({ex.GetType().Name}): {safeError} Message bodies omitted.");
        }
    }

    private Task DismissOnboardingAsync()
    {
        IsOnboardingVisible = false;
        _state.Preferences.IsOnboardingDismissed = true;
        QueueSaveSettings();
        AddActivity("First-run onboarding dismissed.");
        return Task.CompletedTask;
    }

    private Task ClearApplyResultAsync()
    {
        IsApplyResultVisible = false;
        ApplyResultText = string.Empty;
        return Task.CompletedTask;
    }

    private static string ToUserSafeError(Exception ex)
    {
        var message = ex.Message.Replace(Environment.NewLine, " ").Trim();
        if (message.Length > 180)
        {
            message = string.Concat(message.AsSpan(0, 177), "...");
        }

        return string.IsNullOrWhiteSpace(message) ? "No additional details." : message;
    }

    private MailboxSettingsPreview BuildPreview()
    {
        return new MailboxSettingsPreview(
            _currentWindow,
            SelectedAudienceScope,
            SelectedApplyProfile,
            PrimaryInternalMessage,
            PrimaryExternalMessage,
            ExtendedInternalMessage,
            ExtendedExternalMessage,
            !string.IsNullOrWhiteSpace(PrimaryInternalMessage),
            !string.IsNullOrWhiteSpace(PrimaryExternalMessage),
            !string.IsNullOrWhiteSpace(ExtendedInternalMessage),
            !string.IsNullOrWhiteSpace(ExtendedExternalMessage),
            PrimaryInternalMessage.Length,
            PrimaryExternalMessage.Length,
            ExtendedInternalMessage.Length,
            ExtendedExternalMessage.Length);
    }

    private static string BuildPreviewText(MailboxSettingsPreview preview)
    {
        var builder = new StringBuilder();
        builder.AppendLine("MICROSOFT 365 APPLY MODE — Apply will PATCH Microsoft Graph.");
        builder.AppendLine("Target: /me/mailboxSettings/automaticRepliesSetting");
        builder.AppendLine($"Active profile: {preview.ActiveProfile}");
        builder.AppendLine("Status: Scheduled");
        builder.AppendLine($"Start: {preview.Window.Start:yyyy-MM-dd HH:mm zzz}");
        builder.AppendLine($"End:   {preview.Window.End:yyyy-MM-dd HH:mm zzz}");
        builder.AppendLine($"Audience: {preview.AudienceScope}");
        builder.AppendLine($"Reason: {preview.Window.Reason}");
        builder.AppendLine();
        builder.AppendLine("Message body privacy: contents are not logged or included in diagnostics.");
        builder.AppendLine($"Applied internal: {(preview.HasActiveInternalMessage ? "present" : "missing")} ({preview.ActiveInternalLength} chars)");
        builder.AppendLine($"Applied external: {(preview.HasActiveExternalMessage ? "present" : "missing")} ({preview.ActiveExternalLength} chars)");
        builder.AppendLine();
        builder.AppendLine($"Primary internal: {(preview.HasPrimaryInternalMessage ? "present" : "missing")} ({preview.PrimaryInternalLength} chars)");
        builder.AppendLine($"Primary external: {(preview.HasPrimaryExternalMessage ? "present" : "missing")} ({preview.PrimaryExternalLength} chars)");
        builder.AppendLine($"Extended internal: {(preview.HasExtendedInternalMessage ? "present" : "missing")} ({preview.ExtendedInternalLength} chars)");
        builder.AppendLine($"Extended external: {(preview.HasExtendedExternalMessage ? "present" : "missing")} ({preview.ExtendedExternalLength} chars)");
        return builder.ToString();
    }

    private static string BuildApplyReviewText(MailboxSettingsPreview preview)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Review before applying to Microsoft 365");
        builder.AppendLine();
        builder.AppendLine($"Profile: {preview.ActiveProfile}");
        builder.AppendLine($"Schedule: {preview.Window.Start:g} to {preview.Window.End:g}");
        builder.AppendLine($"Audience: {preview.AudienceScope}");
        builder.AppendLine($"Reason: {preview.Window.Reason}");
        builder.AppendLine();
        builder.AppendLine($"Internal reply: {(preview.HasActiveInternalMessage ? "present" : "missing")} ({preview.ActiveInternalLength} chars)");
        builder.AppendLine($"External reply: {(preview.HasActiveExternalMessage ? "present" : "missing")} ({preview.ActiveExternalLength} chars)");
        builder.AppendLine();
        builder.AppendLine("Message bodies are not shown in this confirmation or diagnostics.");
        builder.AppendLine("Choose Yes to update /me/mailboxSettings in Microsoft Graph.");
        return builder.ToString();
    }

    private static string BuildCurrentSettingsText(CurrentMailboxSettingsSummary summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Mailbox: {summary.MailboxUser}");
        builder.AppendLine($"Status: {summary.Status}");
        builder.AppendLine($"External audience: {summary.ExternalAudience}");
        builder.AppendLine($"Scheduled start: {summary.ScheduledStart}");
        builder.AppendLine($"Scheduled end: {summary.ScheduledEnd}");
        builder.AppendLine($"Internal reply: {(summary.HasInternalReply ? "present" : "missing")} ({summary.InternalReplyLength} chars)");
        builder.AppendLine($"External reply: {(summary.HasExternalReply ? "present" : "missing")} ({summary.ExternalReplyLength} chars)");
        return builder.ToString();
    }

    private void ShowApplyResult(string message)
    {
        ApplyResultText = message;
        IsApplyResultVisible = true;
    }

    private void MarkGeneratedTemplateStale(string status)
    {
        if (_generatedTemplate is null)
        {
            return;
        }

        _generatedTemplate = null;
        GeneratedInternalTemplate = string.Empty;
        GeneratedExternalTemplate = string.Empty;
        ApplyGeneratedTemplateCommand.RaiseCanExecuteChanged();
        TemplateStatus = status;
    }

    private void AddActivity(string activity)
    {
        var entry = $"{DateTimeOffset.Now:t} — {activity}";
        RecentActivity.Insert(0, entry);
        while (RecentActivity.Count > 8)
        {
            RecentActivity.RemoveAt(RecentActivity.Count - 1);
        }

        _state.Sync.RecentActivity.Clear();
        foreach (var recentActivity in RecentActivity)
        {
            _state.Sync.RecentActivity.Add(recentActivity);
        }

        QueueSaveSettings();
    }

    private Task ToggleThemeAsync()
    {
        IsDarkMode = !IsDarkMode;
        _state.Preferences.IsDarkMode = IsDarkMode;
        ApplyTheme(IsDarkMode, SelectedThemePalette);
        OnPalettePreviewChanged();
        QueueSaveSettings();
        AddActivity($"Switched to {ThemeModeText.ToLowerInvariant()}.");
        return Task.CompletedTask;
    }

    private Task SelectThemePaletteAsync(ThemePalette palette)
    {
        SelectedThemePalette = palette;
        return Task.CompletedTask;
    }

    private void QueueSaveSettings()
    {
        _saveSettingsDebounce?.Cancel();
        _saveSettingsDebounce?.Dispose();
        _saveSettingsDebounce = new CancellationTokenSource();
        var token = _saveSettingsDebounce.Token;
        _ = SaveSettingsAfterDelayAsync(token);
    }

    private void SaveSettingsNow()
    {
        _saveSettingsDebounce?.Cancel();
        _saveSettingsDebounce?.Dispose();
        _saveSettingsDebounce = null;
        try
        {
            _settingsService.SaveAsync(_state).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            SyncStatus = $"Settings save failed ({ex.GetType().Name})";
        }
    }

    private async Task SaveSettingsAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(350, cancellationToken);
            await _settingsService.SaveAsync(_state, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SyncStatus = $"Settings save failed ({ex.GetType().Name})";
        }
    }

    private void OnPalettePreviewChanged()
    {
        OnPropertyChanged(nameof(PalettePreviewBrush1));
        OnPropertyChanged(nameof(PalettePreviewBrush2));
        OnPropertyChanged(nameof(PalettePreviewBrush3));
        OnPropertyChanged(nameof(PalettePreviewBrush4));
    }

    private static void ApplyTheme(bool isDarkMode, ThemePalette palette)
    {
        var colors = GetPaletteColors(isDarkMode, palette);
        SetBrush("AppBackgroundBrush", colors.AppBackground);
        SetBrush("PanelBrush", colors.Panel);
        SetBrush("PanelSubtleBrush", colors.PanelSubtle);
        SetBrush("AccentBrush", colors.Accent);
        SetBrush("SuccessBrush", colors.Success);
        SetBrush("AccentSoftBrush", colors.AccentSoft);
        SetBrush("AccentTextBrush", colors.AccentText);
        SetBrush("TextPrimaryBrush", colors.TextPrimary);
        SetBrush("TextSecondaryBrush", colors.TextSecondary);
        SetBrush("BorderSoftBrush", colors.Border);
        SetBrush("QuickTimeButtonBrush", colors.QuickButton);
        SetBrush("QuickTimeButtonTextBrush", colors.QuickButtonText);
        SetBrush("DisabledBrush", colors.Disabled);
        SetBrush("DisabledTextBrush", colors.DisabledText);
        SetSystemBrush(System.Windows.SystemColors.WindowBrushKey, colors.Panel);
        SetSystemBrush(System.Windows.SystemColors.WindowTextBrushKey, colors.TextPrimary);
        SetSystemBrush(System.Windows.SystemColors.ControlBrushKey, colors.PanelSubtle);
        SetSystemBrush(System.Windows.SystemColors.ControlTextBrushKey, colors.TextPrimary);
        SetSystemBrush(System.Windows.SystemColors.HighlightBrushKey, colors.AccentSoft);
        SetSystemBrush(System.Windows.SystemColors.HighlightTextBrushKey, colors.AccentText);
        SetSystemBrush(System.Windows.SystemColors.InactiveSelectionHighlightBrushKey, colors.PanelSubtle);
        SetSystemBrush(System.Windows.SystemColors.InactiveSelectionHighlightTextBrushKey, colors.TextPrimary);
    }

    private static ThemeColors GetPaletteColors(bool isDarkMode, ThemePalette palette) => (isDarkMode, palette) switch
    {
        (false, ThemePalette.ProductivityBlue) => new("#F6F8FB", "#FFFFFF", "#FAFBFC", "#2563EB", "#0F766E", "#DBEAFE", "#1E40AF", "#111827", "#6B7280", "#E5E7EB", "#E0E7FF", "#1E3A8A", "#E5E7EB", "#6B7280"),
        (true, ThemePalette.ProductivityBlue) => new("#0F172A", "#111827", "#1F2937", "#38BDF8", "#14B8A6", "#0C4A6E", "#E0F2FE", "#F8FAFC", "#CBD5E1", "#334155", "#164E63", "#ECFEFF", "#1E293B", "#64748B"),

        (false, ThemePalette.TrustNavy) => new("#F8FAFC", "#FFFFFF", "#F1F5F9", "#1D4ED8", "#0369A1", "#DBEAFE", "#1E3A8A", "#0F172A", "#64748B", "#CBD5E1", "#E0F2FE", "#075985", "#E2E8F0", "#64748B"),
        (true, ThemePalette.TrustNavy) => new("#020617", "#0F172A", "#1E293B", "#60A5FA", "#38BDF8", "#1E3A8A", "#DBEAFE", "#F8FAFC", "#CBD5E1", "#334155", "#0C4A6E", "#E0F2FE", "#1E293B", "#64748B"),

        (false, ThemePalette.TealMint) => new("#F0FDFA", "#FFFFFF", "#ECFDF5", "#0D9488", "#059669", "#CCFBF1", "#115E59", "#111827", "#64748B", "#99F6E4", "#CCFBF1", "#134E4A", "#E5E7EB", "#6B7280"),
        (true, ThemePalette.TealMint) => new("#042F2E", "#0F172A", "#134E4A", "#2DD4BF", "#34D399", "#115E59", "#CCFBF1", "#F0FDFA", "#A7F3D0", "#2D6A64", "#0F766E", "#ECFDF5", "#1E293B", "#64748B"),

        (false, ThemePalette.PremiumGold) => new("#F8F7F2", "#FFFFFF", "#F3F0E7", "#1E3A8A", "#B45309", "#FEF3C7", "#78350F", "#111827", "#6B7280", "#D6D3C8", "#FEF3C7", "#78350F", "#E5E7EB", "#6B7280"),
        (true, ThemePalette.PremiumGold) => new("#0B1120", "#111827", "#1F2937", "#D6B25E", "#F59E0B", "#3F2F12", "#FEF3C7", "#F8FAFC", "#D1D5DB", "#3F3F46", "#451A03", "#FEF3C7", "#1E293B", "#64748B"),

        _ => GetPaletteColors(isDarkMode, ThemePalette.ProductivityBlue)
    };

    private static SolidColorBrush CreateBrush(string color) => new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));

    private static void SetBrush(string resourceKey, string color)
    {
        System.Windows.Application.Current.Resources[resourceKey] = CreateBrush(color);
    }

    private static void SetSystemBrush(ResourceKey resourceKey, string color)
    {
        System.Windows.Application.Current.Resources[resourceKey] = CreateBrush(color);
    }

    private sealed record ThemeColors(
        string AppBackground,
        string Panel,
        string PanelSubtle,
        string Accent,
        string Success,
        string AccentSoft,
        string AccentText,
        string TextPrimary,
        string TextSecondary,
        string Border,
        string QuickButton,
        string QuickButtonText,
        string Disabled,
        string DisabledText);
}
