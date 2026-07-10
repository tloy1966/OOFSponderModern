namespace OOFSponderModern.Models;

public sealed class MessageProfile
{
    public string PrimaryInternalMessage { get; set; } = string.Empty;
    public string PrimaryExternalMessage { get; set; } = string.Empty;
    public string ExtendedInternalMessage { get; set; } = string.Empty;
    public string ExtendedExternalMessage { get; set; } = string.Empty;
    public AudienceScope AudienceScope { get; set; } = AudienceScope.ContactsOnly;
}
