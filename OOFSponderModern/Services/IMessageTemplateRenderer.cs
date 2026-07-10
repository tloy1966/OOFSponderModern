using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public interface IMessageTemplateRenderer
{
    GeneratedOofTemplateResult Render(MessageTemplate template, OofWindow window, string userName, DateTimeOffset renderedAt);
}
