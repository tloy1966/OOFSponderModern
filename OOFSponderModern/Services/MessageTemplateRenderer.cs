using System.Globalization;
using System.Text.RegularExpressions;
using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public sealed partial class MessageTemplateRenderer : IMessageTemplateRenderer
{
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

    public GeneratedOofTemplateResult Render(
        MessageTemplate template,
        OofWindow window,
        string userName,
        DateTimeOffset renderedAt)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StartDate"] = window.Start.ToString("D", EnglishCulture),
            ["StartTime"] = window.Start.ToString("t", EnglishCulture),
            ["ReturnDate"] = window.End.ToString("D", EnglishCulture),
            ["ReturnTime"] = window.End.ToString("t", EnglishCulture),
            ["Duration"] = FormatDuration(window.Duration),
            ["UserName"] = string.IsNullOrWhiteSpace(userName) ? Environment.UserName : userName.Trim()
        };

        return new GeneratedOofTemplateResult(
            RenderText(template.InternalTemplate, values),
            RenderText(template.ExternalTemplate, values),
            $"Saved template: {template.Name}",
            true,
            renderedAt);
    }

    public static IReadOnlyList<string> FindUnknownVariables(MessageTemplate template)
    {
        var supportedVariables = new HashSet<string>(
            ["StartDate", "StartTime", "ReturnDate", "ReturnTime", "Duration", "UserName"],
            StringComparer.OrdinalIgnoreCase);
        return VariablePattern()
            .Matches($"{template.InternalTemplate}\n{template.ExternalTemplate}")
            .Select(match => match.Groups[1].Value)
            .Where(name => !supportedVariables.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string RenderText(string source, IReadOnlyDictionary<string, string> values)
    {
        return VariablePattern().Replace(source ?? string.Empty, match =>
            values.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            var days = (int)Math.Floor(duration.TotalDays);
            return duration.Hours > 0 ? $"{days}d {duration.Hours}h" : $"{days}d";
        }

        if (duration.TotalHours >= 1)
        {
            var hours = (int)Math.Floor(duration.TotalHours);
            return duration.Minutes > 0 ? $"{hours}h {duration.Minutes}m" : $"{hours}h";
        }

        return $"{Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes))}m";
    }

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9]*)\}")]
    private static partial Regex VariablePattern();
}
