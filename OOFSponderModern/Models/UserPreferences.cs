namespace OOFSponderModern.Models;

public sealed class UserPreferences
{
    public bool IsDarkMode { get; set; }
    public bool StartWithWindows { get; set; }
    public bool IsAutomaticSyncEnabled { get; set; }
    public bool IsLinkedTimeAdjustmentEnabled { get; set; } = true;
    public ScheduleSource SelectedScheduleSource { get; set; } = ScheduleSource.WeeklySchedule;
    public bool HasInitializedLongLeaveProfile { get; set; }
    public ThemePalette ThemePalette { get; set; } = ThemePalette.ProductivityBlue;
    public TemplateTargetProfile SelectedTemplateTarget { get; set; } = TemplateTargetProfile.Primary;
    public TemplateTargetProfile SelectedApplyProfile { get; set; } = TemplateTargetProfile.Primary;
    public bool IsOnboardingDismissed { get; set; }
    public string TemplateDisplayName { get; set; } = Environment.UserName;
    public bool AreDefaultMessageTemplatesInitialized { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
}
