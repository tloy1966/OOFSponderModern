using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public interface IMailboxSettingsClient
{
    Task<string> ApplyAsync(MailboxSettingsPreview preview, CancellationToken cancellationToken = default);
    Task<AutomaticSyncResult> SyncIfChangedAsync(MailboxSettingsPreview preview, CancellationToken cancellationToken = default);
    Task<CurrentMailboxSettingsSummary> LoadCurrentSettingsAsync(CancellationToken cancellationToken = default);
}
