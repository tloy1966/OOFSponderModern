namespace OOFSponderModern.Models;

public sealed record CurrentMailboxSettingsSummary(
    string MailboxUser,
    string Status,
    string ExternalAudience,
    string ScheduledStart,
    string ScheduledEnd,
    bool HasInternalReply,
    bool HasExternalReply,
    int InternalReplyLength,
    int ExternalReplyLength);