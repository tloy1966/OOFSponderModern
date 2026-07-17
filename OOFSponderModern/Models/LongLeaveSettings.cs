namespace OOFSponderModern.Models;

public sealed class LongLeaveSettings
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string Label { get; set; } = string.Empty;
}