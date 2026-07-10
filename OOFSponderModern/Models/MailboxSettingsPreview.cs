namespace OOFSponderModern.Models;

public sealed record MailboxSettingsPreview(
    OofWindow Window,
    AudienceScope AudienceScope,
    TemplateTargetProfile ActiveProfile,
    string PrimaryInternalMessage,
    string PrimaryExternalMessage,
    string ExtendedInternalMessage,
    string ExtendedExternalMessage,
    bool HasPrimaryInternalMessage,
    bool HasPrimaryExternalMessage,
    bool HasExtendedInternalMessage,
    bool HasExtendedExternalMessage,
    int PrimaryInternalLength,
    int PrimaryExternalLength,
    int ExtendedInternalLength,
    int ExtendedExternalLength)
{
    public string ActiveInternalMessage => ActiveProfile == TemplateTargetProfile.Primary
        ? PrimaryInternalMessage
        : ExtendedInternalMessage;

    public string ActiveExternalMessage => ActiveProfile == TemplateTargetProfile.Primary
        ? PrimaryExternalMessage
        : ExtendedExternalMessage;

    public bool HasActiveInternalMessage => ActiveProfile == TemplateTargetProfile.Primary
        ? HasPrimaryInternalMessage
        : HasExtendedInternalMessage;

    public bool HasActiveExternalMessage => ActiveProfile == TemplateTargetProfile.Primary
        ? HasPrimaryExternalMessage
        : HasExtendedExternalMessage;

    public int ActiveInternalLength => ActiveProfile == TemplateTargetProfile.Primary
        ? PrimaryInternalLength
        : ExtendedInternalLength;

    public int ActiveExternalLength => ActiveProfile == TemplateTargetProfile.Primary
        ? PrimaryExternalLength
        : ExtendedExternalLength;
}
