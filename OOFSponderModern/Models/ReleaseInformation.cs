namespace OOFSponderModern.Models;

public sealed record ReleaseInformation(
    string Version,
    string Name,
    string Notes,
    string Url,
    DateTimeOffset PublishedAt);
