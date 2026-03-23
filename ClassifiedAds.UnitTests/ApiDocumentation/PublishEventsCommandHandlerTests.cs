using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class PublishEventsCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenOutboxQueryTimesOut_Should_ReturnWithoutPublishing()
    {
        var loggerMock = new Mock<ILogger<PublishEventsCommandHandler>>();
        var dateTimeProviderMock = new Mock<IDateTimeProvider>();
        var outboxRepositoryMock = new Mock<IRepository<OutboxMessage, Guid>>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var messageBusMock = new Mock<IMessageBus>();

        dateTimeProviderMock.SetupGet(x => x.OffsetUtcNow).Returns(DateTimeOffset.UtcNow);
        outboxRepositoryMock.SetupGet(x => x.UnitOfWork).Returns(unitOfWorkMock.Object);
        outboxRepositoryMock.Setup(x => x.GetQueryableSet())
            .Returns(new ThrowingAsyncEnumerable<OutboxMessage>(CreateDatabaseTimeoutException()));

        var handler = new PublishEventsCommandHandler(
            loggerMock.Object,
            dateTimeProviderMock.Object,
            outboxRepositoryMock.Object,
            messageBusMock.Object);
        var command = new PublishEventsCommand();

        await handler.HandleAsync(command);

        command.SentEventsCount.Should().Be(0);
        messageBusMock.Verify(
            x => x.SendAsync(It.IsAny<PublishingOutboxMessage>(), It.IsAny<CancellationToken>()),
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
