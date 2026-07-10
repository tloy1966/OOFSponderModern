namespace OOFSponderModern.Models;

public sealed record OofWindow(DateTimeOffset Start, DateTimeOffset End, string Reason)
{
    public TimeSpan Duration => End - Start;
}
