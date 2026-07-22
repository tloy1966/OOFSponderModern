namespace OOFSponderModern.Services;

public sealed class AutomaticSyncAuthenticationRequiredException : InvalidOperationException
{
    public AutomaticSyncAuthenticationRequiredException()
        : base("Sign in with Apply to M365 before automatic sync can continue.")
    {
    }
}