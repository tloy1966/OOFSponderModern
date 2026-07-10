namespace OOFSponderModern.Models;

public sealed class MessageTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string InternalTemplate { get; set; } = string.Empty;
    public string ExternalTemplate { get; set; } = string.Empty;
}
