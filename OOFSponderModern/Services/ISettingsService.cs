using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public interface ISettingsService
{
    Task<AppState> LoadAsync(CancellationToken cancellationToken = default);
}
