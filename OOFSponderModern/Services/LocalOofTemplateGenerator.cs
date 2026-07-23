using System.Globalization;
using System.Text;
using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public sealed class LocalOofTemplateGenerator : IOofTemplateGenerator
{
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

    public Task<GeneratedOofTemplateResult> GenerateAsync(
        OofTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isCurrentlyOutOfOffice = request.GeneratedAt >= request.Window.Start &&
                                     request.GeneratedAt < request.Window.End;
        var startText = FormatDateTime(request.Window.Start, request.GeneratedAt);
        var returnText = FormatDateTime(request.Window.End, request.GeneratedAt);
        var internalBuilder = new StringBuilder();
        internalBuilder.AppendLine(isCurrentlyOutOfOffice
            ? $"Hi, I am currently out of office and will return on {returnText}."
            : $"Hi, I will be out of office from {startText} until {returnText}.");
        internalBuilder.AppendLine("I will respond after I return. For urgent internal matters, please contact my backup or the team channel.");

        var externalBuilder = new StringBuilder();
        externalBuilder.AppendLine(isCurrentlyOutOfOffice
            ? $"Thank you for your message. I am currently out of office and will return on {returnText}."
            : $"Thank you for your message. I will be out of office from {startText} until {returnText}.");
        externalBuilder.AppendLine("I will respond after I return.");
        externalBuilder.AppendLine("If this is urgent, please use your usual support or account contact path.");

        return Task.FromResult(new GeneratedOofTemplateResult(
            internalBuilder.ToString().TrimEnd(),
            externalBuilder.ToString().TrimEnd(),
            "Local preview generator",
            true,
            request.GeneratedAt));
    }

    private static string FormatDateTime(DateTimeOffset value, DateTimeOffset generatedAt)
    {
        var format = value.Year == generatedAt.Year
            ? "dddd, MMMM d 'at' h:mm tt"
            : "dddd, MMMM d, yyyy 'at' h:mm tt";
        return value.ToString(format, EnglishCulture);
    }
}
