using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public interface IMailboxSettingsClient
{
    Task<string> PreviewApplyAsync(MailboxSettingsPreview preview, CancellationToken cancellationToken = default);
    Task<CurrentMailboxSettingsSummary> LoadCurrentSettingsAsync(CancellationToken cancellationToken = default);
}
