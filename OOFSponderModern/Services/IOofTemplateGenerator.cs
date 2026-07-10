using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public interface IOofTemplateGenerator
{
    Task<GeneratedOofTemplateResult> GenerateAsync(
        OofTemplateRequest request,
        CancellationToken cancellationToken = default);
}
