namespace OOFSponderModern.Models;

public sealed record AutomaticSyncResult(
    bool WasApplied,
    string MailboxUser);