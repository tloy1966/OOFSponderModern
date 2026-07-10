namespace OOFSponderModern.Models;

public sealed record GeneratedOofTemplateResult(
    string InternalTemplate,
    string ExternalTemplate,
    string ProviderName,
    bool IsLocalPreview,
    DateTimeOffset GeneratedAt);
