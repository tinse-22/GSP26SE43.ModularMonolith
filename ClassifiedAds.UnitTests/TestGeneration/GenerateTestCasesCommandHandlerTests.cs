using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.MessageBusMessages;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class GenerateTestCasesCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepositoryMock;
    private readonly Mock<IRepository<TestOrderProposal, Guid>> _proposalRepositoryMock;
    private readonly Mock<IRepository<TestGenerationJob, Guid>> _jobRepositoryMock;
    private readonly Mock<ITestGenerationPayloadBuilder> _payloadBuilderMock;
    private readonly Mock<IMessageBus> _messageBusMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly GenerateTestCasesCommandHandler _handler;
    private TestGenerationJob _capturedJob;
    private byte[] _rowVersionAtInsert;

    public GenerateTestCasesCommandHandlerTests()
    {
        _suiteRepositoryMock = new Mock<IRepository<TestSuite, Guid>>();
        _proposalRepositoryMock = new Mock<IRepository<TestOrderProposal, Guid>>();
        _jobRepositoryMock = new Mock<IRepository<TestGenerationJob, Guid>>();
        _payloadBuilderMock = new Mock<ITestGenerationPayloadBuilder>();
        _messageBusMock = new Mock<IMessageBus>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _jobRepositoryMock.SetupGet(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _payloadBuilderMock.SetupGet(x => x.WebhookName).Returns("generate-test-cases-unified");

        _jobRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<TestGenerationJob>(), It.IsAny<CancellationToken>()))
            .Callback<TestGenerationJob, CancellationToken>((job, _) =>
            {
                job.Id = Guid.NewGuid();
                _rowVersionAtInsert = job.RowVersion?.ToArray();
                _capturedJob = job;
            })
            .Returns(Task.CompletedTask);

        _handler = new GenerateTestCasesCommandHandler(
            _suiteRepositoryMock.Object,
            _proposalRepositoryMock.Object,
            _jobRepositoryMock.Object,
            _payloadBuilderMock.Object,
            _messageBusMock.Object,
            new Mock<ILogger<GenerateTestCasesCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Should_QueueTriggerMessage_And_KeepJobQueued()
    {
        var suiteId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var suite = new TestSuite
        {
            Id = suiteId,
            CreatedById = userId,
            Name = "Suite A",
        };
        var proposal = new TestOrderProposal
        {
            Id = proposalId,
            TestSuiteId = suiteId,
            Status = ProposalStatus.Approved,
            ProposalNumber = 2,
        };
        TriggerTestGenerationMessage sentMessage = null;
        MetaData sentMetaData = null;

        SetupSuite(suite);
        SetupProposal(proposal);

        _messageBusMock
            .Setup(x => x.SendAsync(
                It.IsAny<TriggerTestGenerationMessage>(),
                It.IsAny<MetaData>(),
                It.IsAny<CancellationToken>()))
            .Callback<TriggerTestGenerationMessage, MetaData, CancellationToken>((message, metadata, _) =>
            {
                sentMessage = message;
                sentMetaData = metadata;
            })
            .Returns(Task.CompletedTask);

        var command = new GenerateTestCasesCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = userId,
        };

        await _handler.HandleAsync(command);

        command.JobId.Should().Be(_capturedJob.Id);
        _capturedJob.Status.Should().Be(GenerationJobStatus.Queued);
        _capturedJob.CompletedAt.Should().BeNull();
        _capturedJob.WebhookName.Should().Be("generate-test-cases-unified");
        _capturedJob.RowVersion.Should().NotBeNullOrEmpty();
        sentMessage.Should().NotBeNull();
        sentMessage!.JobId.Should().Be(command.JobId);
        sentMessage.TestSuiteId.Should().Be(suiteId);
        sentMessage.ProposalId.Should().Be(proposalId);
        sentMessage.TriggeredById.Should().Be(userId);
        sentMetaData.Should().NotBeNull();
        sentMetaData!.MessageId.Should().Be(command.JobId.ToString());

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenQueuePublishFails_Should_MarkJobFailed_AndThrowValidation()
    {
        var suiteId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();

        SetupSuite(new TestSuite
        {
            Id = suiteId,
            CreatedById = userId,
            Name = "Suite B",
        });
        SetupProposal(new TestOrderProposal
        {
            Id = proposalId,
            TestSuiteId = suiteId,
            Status = ProposalStatus.Approved,
            ProposalNumber = 1,
        });

        _messageBusMock
            .Setup(x => x.SendAsync(
                It.IsAny<TriggerTestGenerationMessage>(),
                It.IsAny<MetaData>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue unavailable"));

        var command = new GenerateTestCasesCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = userId,
        };

        var act = () => _handler.HandleAsync(command);

        var exception = await act.Should().ThrowAsync<ValidationException>();

        exception.Which.Message.Should().Contain("hàng đợi");
        command.JobId.Should().Be(_capturedJob.Id);
        _capturedJob.Status.Should().Be(GenerationJobStatus.Failed);
        _capturedJob.CompletedAt.Should().NotBeNull();
        _capturedJob.ErrorMessage.Should().Contain("queue unavailable");
        _capturedJob.RowVersion.Should().NotBeNullOrEmpty();
        _capturedJob.RowVersion.SequenceEqual(_rowVersionAtInsert).Should().BeFalse();

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private void SetupSuite(TestSuite suite)
    {
        _suiteRepositoryMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestSuite> { suite }.AsQueryable());
        _suiteRepositoryMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>())).ReturnsAsync(suite);
    }

    private void SetupProposal(TestOrderProposal proposal)
    {
        _proposalRepositoryMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestOrderProposal> { proposal }.AsQueryable());
        _proposalRepositoryMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestOrderProposal>>())).ReturnsAsync(proposal);
    }
}
