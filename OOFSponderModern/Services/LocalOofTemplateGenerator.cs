using System.Globalization;
using System.Text;
using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public sealed class LocalOofTemplateGenerator : IOofTemplateGenerator
{
    public Task<GeneratedOofTemplateResult> GenerateAsync(
        OofTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startText = FormatDateTime(request.Window.Start);
        var returnText = FormatDateTime(request.Window.End);
        var daysText = request.CalendarDays == 1
            ? "1 calendar day"
            : $"{request.CalendarDays} calendar days";
        var durationText = FormatDuration(request.Window.Duration);
        var profileText = request.TargetProfile == TemplateTargetProfile.Extended
            ? "extended absence"
            : "standard out-of-office";
        var contextText = request.IsExtendedContext
            ? "extended away period"
            : "off-work window";
        var audienceText = request.AudienceScope switch
        {
            AudienceScope.None => "External automatic replies are currently disabled; keep this as a draft if you enable them later.",
            AudienceScope.ContactsOnly => "External reply scope: contacts only.",
            AudienceScope.AllExternal => "External reply scope: all external senders.",
            _ => "External reply scope follows the selected mailbox setting."
        };

        var internalBuilder = new StringBuilder();
        internalBuilder.AppendLine($"Hi, I am out of office from {startText} until {returnText}.");
        internalBuilder.AppendLine($"This is a {profileText} message for a schedule-derived {contextText} ({daysText}, about {durationText}).");
        internalBuilder.AppendLine("I will respond after I return. For urgent internal matters, please contact my backup or the team channel.");

        var externalBuilder = new StringBuilder();
        externalBuilder.AppendLine($"Thank you for your message. I am currently out of office from {startText} until {returnText}.");
        externalBuilder.AppendLine($"I will respond after I return. {audienceText}");
        externalBuilder.AppendLine("If this is urgent, please use your usual support or account contact path.");

        return Task.FromResult(new GeneratedOofTemplateResult(
            internalBuilder.ToString().TrimEnd(),
            externalBuilder.ToString().TrimEnd(),
            "Local preview generator",
            true,
            request.GeneratedAt));
    }

    private static string FormatDateTime(DateTimeOffset value) =>
        value.ToString("ddd, MMM d yyyy h:mm tt zzz", CultureInfo.CurrentCulture);

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            var days = (int)Math.Floor(duration.TotalDays);
            var hours = duration.Hours;
            return hours > 0 ? $"{days}d {hours}h" : $"{days}d";
        }

        if (duration.TotalHours >= 1)
        {
            var hours = (int)Math.Floor(duration.TotalHours);
            var minutes = duration.Minutes;
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }

        return $"{Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes))}m";
    }
}
