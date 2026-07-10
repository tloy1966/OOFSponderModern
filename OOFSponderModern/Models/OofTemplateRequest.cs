namespace OOFSponderModern.Models;

public sealed record OofTemplateRequest(
    OofWindow Window,
    AudienceScope AudienceScope,
    TemplateTargetProfile TargetProfile,
    DateTimeOffset GeneratedAt)
{
    public int CalendarDays =>
        Math.Max(1, (Window.End.Date - Window.Start.Date).Days + 1);

    public bool IsExtendedContext =>
        TargetProfile == TemplateTargetProfile.Extended || Window.Duration >= TimeSpan.FromDays(3);
}
