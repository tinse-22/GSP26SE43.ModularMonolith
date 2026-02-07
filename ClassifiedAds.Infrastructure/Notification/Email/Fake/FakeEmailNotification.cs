using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Infrastructure.Notification.Email.Fake;

public class FakeEmailNotification : IEmailNotification
{
    private readonly ILogger<FakeEmailNotification> _logger;

    public FakeEmailNotification()
    {
        _logger = null;
    }

    public FakeEmailNotification(ILogger<FakeEmailNotification> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(IEmailMessage emailMessage, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation(
            "[FakeEmail] To={To}, Subject={Subject}, BodyLength={Len}",
            emailMessage.Tos,
            emailMessage.Subject,
            emailMessage.Body?.Length ?? 0);

        return Task.CompletedTask;
    }
}
