namespace OOFSponderModern.Services;

public interface IStartupService
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}