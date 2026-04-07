using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestGenerationHttpMethod = ClassifiedAds.Modules.TestGeneration.Entities.HttpMethod;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class SaveAiGeneratedTestCasesCommandHandlerTests
{
    private static readonly Guid DefaultSuiteId = Guid.NewGuid();
    private static readonly Guid DefaultUserId = Guid.NewGuid();

    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestCase, Guid>> _testCaseRepoMock;
    private readonly Mock<IRepository<TestCaseRequest, Guid>> _requestRepoMock;
    private readonly Mock<IRepository<TestCaseExpectation, Guid>> _expectationRepoMock;
    private readonly Mock<IRepository<TestSuiteVersion, Guid>> _versionRepoMock;
    private readonly Mock<IRepository<TestGenerationJob, Guid>> _jobRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly SaveAiGeneratedTestCasesCommandHandler _handler;

    public SaveAiGeneratedTestCasesCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _testCaseRepoMock = new Mock<IRepository<TestCase, Guid>>();
        _requestRepoMock = new Mock<IRepository<TestCaseRequest, Guid>>();
        _expectationRepoMock = new Mock<IRepository<TestCaseExpectation, Guid>>();
        _versionRepoMock = new Mock<IRepository<TestSuiteVersion, Guid>>();
        _jobRepoMock = new Mock<IRepository<TestGenerationJob, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _testCaseRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _testCaseRepoMock.Setup(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _requestRepoMock.Setup(x => x.AddAsync(It.IsAny<TestCaseRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _expectationRepoMock.Setup(x => x.AddAsync(It.IsAny<TestCaseExpectation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _versionRepoMock.Setup(x => x.AddAsync(It.IsAny<TestSuiteVersion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _suiteRepoMock.Setup(x => x.UpdateAsync(It.IsAny<TestSuite>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _jobRepoMock.Setup(x => x.UpdateAsync(It.IsAny<TestGenerationJob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _jobRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestGenerationJob>().AsQueryable());
        _jobRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestGenerationJob>>()))
            .ReturnsAsync((TestGenerationJob)null);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new SaveAiGeneratedTestCasesCommandHandler(
            _suiteRepoMock.Object,
            _testCaseRepoMock.Object,
            _requestRepoMock.Object,
            _expectationRepoMock.Object,
            _versionRepoMock.Object,
            _jobRepoMock.Object,
            new Mock<ILogger<SaveAiGeneratedTestCasesCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Should_MarkSuiteReadyAndCreateVersion_WhenCallbackSucceeds()
    {
        var actorUserId = Guid.NewGuid();
        var suite = CreateSuite();
        suite.LastModifiedById = actorUserId;
        suite.Status = TestSuiteStatus.Draft;
        suite.Version = 2;

        SetupSuiteFound(suite);
        SetupExistingTestCases(1);

        var command = CreateValidCommand();

        await _handler.HandleAsync(command);

        _testCaseRepoMock.Verify(x => x.BulkDeleteAsync(
            It.Is<IReadOnlyCollection<TestCase>>(cases => cases.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);

        _testCaseRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCase>(tc =>
                tc.TestSuiteId == suite.Id &&
                tc.OrderIndex == 0 &&
                tc.LastModifiedById == actorUserId &&
                tc.CreatedDateTime > DateTimeOffset.MinValue),
            It.IsAny<CancellationToken>()), Times.Once);

        _requestRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCaseRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _expectationRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCaseExpectation>(), It.IsAny<CancellationToken>()), Times.Once);

        _versionRepoMock.Verify(x => x.AddAsync(
            It.Is<TestSuiteVersion>(v =>
                v.TestSuiteId == suite.Id &&
                v.VersionNumber == 3 &&
                v.ChangedById == actorUserId &&
                v.ChangeType == VersionChangeType.TestCasesModified &&
                v.ChangeDescription.Contains("AI-generated test case") &&
                v.TestCaseOrderSnapshot.Contains("\"orderIndex\":0")),
            It.IsAny<CancellationToken>()), Times.Once);

        _suiteRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestSuite>(s =>
                s.Status == TestSuiteStatus.Ready &&
                s.Version == 3 &&
                s.LastModifiedById == actorUserId &&
                s.UpdatedDateTime > DateTimeOffset.MinValue &&
                s.RowVersion != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_ParseHttpMethod_FromMethodAndPathFormat()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupExistingTestCases(1);

        var command = CreateValidCommand();
        command.TestCases[0].Request.HttpMethod = "POST /api/categories";

        await _handler.HandleAsync(command);

        _requestRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseRequest>(r => r.HttpMethod == TestGenerationHttpMethod.POST),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_CompleteLatestGenerationJob_WithNewRowVersion()
    {
        var suite = CreateSuite();
        var originalRowVersion = new byte[] { 1, 2, 3, 4 };
        var waitingJob = new TestGenerationJob
        {
            Id = Guid.NewGuid(),
            TestSuiteId = suite.Id,
            Status = GenerationJobStatus.WaitingForCallback,
            QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            RowVersion = originalRowVersion,
        };

        SetupSuiteFound(suite);
        SetupExistingTestCases(0);
        _jobRepoMock.Setup(x => x.GetQueryableSet()).Returns(new[] { waitingJob }.AsQueryable());
        _jobRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestGenerationJob>>()))
            .ReturnsAsync(waitingJob);

        await _handler.HandleAsync(CreateValidCommand());

        _jobRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestGenerationJob>(job =>
                job.Id == waitingJob.Id &&
                job.Status == GenerationJobStatus.Completed &&
                job.TestCasesGenerated == 1 &&
                job.CompletedAt.HasValue &&
                job.RowVersion != null &&
                !job.RowVersion.SequenceEqual(originalRowVersion)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_FallbackToName_WhenRequestHttpMethodIsUnparseable()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupExistingTestCases(1);

        var command = CreateValidCommand();
        command.TestCases[0].Request.HttpMethod = "Create Category Happy Path";
        command.TestCases[0].Name = "POST /api/categories - Create Category Happy Path";

        await _handler.HandleAsync(command);

        _requestRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseRequest>(r => r.HttpMethod == TestGenerationHttpMethod.POST),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenSuiteIsArchived()
    {
        var suite = CreateSuite();
        suite.Status = TestSuiteStatus.Archived;
        SetupSuiteFound(suite);

        var act = () => _handler.HandleAsync(CreateValidCommand());

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*archived*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNoTestCasesProvided()
    {
        var command = new SaveAiGeneratedTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            TestCases = new List<AiGeneratedTestCaseDto>(),
        };

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*At least one*");
    }

    private static TestSuite CreateSuite()
    {
        return new TestSuite
        {
            Id = DefaultSuiteId,
            CreatedById = DefaultUserId,
            Name = "AI Callback Suite",
            Status = TestSuiteStatus.Draft,
            ApprovalStatus = ApprovalStatus.Approved,
            Version = 1,
            EndpointBusinessContexts = new Dictionary<Guid, string>(),
        };
    }

    private static SaveAiGeneratedTestCasesCommand CreateValidCommand()
    {
        return new SaveAiGeneratedTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            TestCases = new List<AiGeneratedTestCaseDto>
            {
                new()
                {
                    EndpointId = Guid.NewGuid(),
                    Name = "Create resource",
                    Description = "Generated by callback",
                    TestType = "HappyPath",
                    Priority = "High",
                    OrderIndex = 0,
                    Request = new AiTestCaseRequestDto
                    {
                        HttpMethod = "POST",
                        Url = "/api/resources",
                        BodyType = "json",
                        Body = "{\"name\":\"demo\"}",
                    },
                    Expectation = new AiTestCaseExpectationDto
                    {
                        ExpectedStatus = "[201]",
                    },
                },
            },
        };
    }

    private void SetupSuiteFound(TestSuite suite)
    {
        _suiteRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new[] { suite }.AsQueryable());
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
    }

    private void SetupExistingTestCases(int count)
    {
        var existingCases = Enumerable.Range(0, count)
            .Select(i => new TestCase
            {
                Id = Guid.NewGuid(),
                TestSuiteId = DefaultSuiteId,
                Name = $"Existing {i}",
                OrderIndex = i,
            })
            .ToList();

        _testCaseRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(existingCases.AsQueryable());
        _testCaseRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync(existingCases);
        _testCaseRepoMock.Setup(x => x.BulkDeleteAsync(It.IsAny<IReadOnlyCollection<TestCase>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }
}
