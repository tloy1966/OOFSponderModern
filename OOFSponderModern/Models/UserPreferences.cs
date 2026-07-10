namespace OOFSponderModern.Models;

public sealed class UserPreferences
{
    public bool IsDarkMode { get; set; }
    public bool IsLinkedTimeAdjustmentEnabled { get; set; } = true;
    public ThemePalette ThemePalette { get; set; } = ThemePalette.ProductivityBlue;
    public TemplateTargetProfile SelectedTemplateTarget { get; set; } = TemplateTargetProfile.Primary;
    public TemplateTargetProfile SelectedApplyProfile { get; set; } = TemplateTargetProfile.Primary;
    public string? WindowMonitorDeviceName { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool IsWindowMaximized { get; set; }
}