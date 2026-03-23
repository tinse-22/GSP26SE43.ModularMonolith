using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Infrastructure.Notification.Sms;
using ClassifiedAds.Modules.Notification.Commands;
using ClassifiedAds.Modules.Notification.Entities;
using ClassifiedAds.Modules.Notification.Persistence;
using ClassifiedAds.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Notification;

public class SendSmsMessagesCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenSmsQueryTimesOut_Should_ReturnWithoutSending()
    {
        var loggerMock = new Mock<ILogger<SendSmsMessagesCommandHandler>>();
        var repositoryMock = new Mock<ISmsMessageRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var smsNotificationMock = new Mock<ISmsNotification>();
        var dateTimeProviderMock = new Mock<IDateTimeProvider>();

        dateTimeProviderMock.SetupGet(x => x.OffsetUtcNow).Returns(DateTimeOffset.UtcNow);
        repositoryMock.SetupGet(x => x.UnitOfWork).Returns(unitOfWorkMock.Object);
        repositoryMock.Setup(x => x.GetQueryableSet())
            .Returns(new ThrowingAsyncEnumerable<SmsMessage>(CreateDatabaseTimeoutException()));

        var handler = new SendSmsMessagesCommandHandler(
            loggerMock.Object,
            repositoryMock.Object,
            smsNotificationMock.Object,
            dateTimeProviderMock.Object);
        var command = new SendSmsMessagesCommand();

        await handler.HandleAsync(command);

        command.SentMessagesCount.Should().Be(0);
        smsNotificationMock.Verify(
            x => x.SendAsync(It.IsAny<ClassifiedAds.Modules.Notification.DTOs.SmsMessageDTO>(), It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static InvalidOperationException CreateDatabaseTimeoutException()
    {
        return new InvalidOperationException(
            "A transient failure has occurred.",
            new NpgsqlException(
                "Exception while reading from stream",
                new TimeoutException("Timeout during reading attempt")));
    }
}
