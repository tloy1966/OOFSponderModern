using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public interface IReleaseUpdateService
{
    Task<ReleaseInformation?> GetLatestReleaseAsync(CancellationToken cancellationToken = default);
}
