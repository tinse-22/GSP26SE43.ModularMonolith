using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
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
    private readonly Mock<IRepository<TestCaseDependency, Guid>> _dependencyRepoMock;
    private readonly Mock<IRepository<TestCaseRequest, Guid>> _requestRepoMock;
    private readonly Mock<IRepository<TestCaseExpectation, Guid>> _expectationRepoMock;
    private readonly Mock<IRepository<TestCaseVariable, Guid>> _variableRepoMock;
    private readonly Mock<IRepository<TestSuiteVersion, Guid>> _versionRepoMock;
    private readonly Mock<IRepository<TestGenerationJob, Guid>> _jobRepoMock;
    private readonly Mock<IRepository<TestCaseRequirementLink, Guid>> _linkRepoMock;
    private readonly Mock<IRepository<SrsRequirement, Guid>> _srsRequirementRepoMock;
    private readonly Mock<IRepository<TestOrderProposal, Guid>> _proposalRepoMock;
    private readonly Mock<IApiTestOrderService> _apiTestOrderServiceMock;
    private readonly Mock<IApiEndpointMetadataService> _endpointMetadataServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<SaveAiGeneratedTestCasesCommandHandler>> _loggerMock;
    private readonly SaveAiGeneratedTestCasesCommandHandler _handler;

    public SaveAiGeneratedTestCasesCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _testCaseRepoMock = new Mock<IRepository<TestCase, Guid>>();
        _dependencyRepoMock = new Mock<IRepository<TestCaseDependency, Guid>>();
        _requestRepoMock = new Mock<IRepository<TestCaseRequest, Guid>>();
        _expectationRepoMock = new Mock<IRepository<TestCaseExpectation, Guid>>();
        _variableRepoMock = new Mock<IRepository<TestCaseVariable, Guid>>();
        _versionRepoMock = new Mock<IRepository<TestSuiteVersion, Guid>>();
        _jobRepoMock = new Mock<IRepository<TestGenerationJob, Guid>>();
        _linkRepoMock = new Mock<IRepository<TestCaseRequirementLink, Guid>>();
        _srsRequirementRepoMock = new Mock<IRepository<SrsRequirement, Guid>>();
        _proposalRepoMock = new Mock<IRepository<TestOrderProposal, Guid>>();
        _apiTestOrderServiceMock = new Mock<IApiTestOrderService>();
        _endpointMetadataServiceMock = new Mock<IApiEndpointMetadataService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<SaveAiGeneratedTestCasesCommandHandler>>();

        _testCaseRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _testCaseRepoMock.Setup(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _dependencyRepoMock.Setup(x => x.AddAsync(It.IsAny<TestCaseDependency>(), It.IsAny<CancellationToken>()))
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
        _linkRepoMock.Setup(x => x.AddAsync(It.IsAny<TestCaseRequirementLink>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _linkRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestCaseRequirementLink>().AsQueryable());
        _linkRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseRequirementLink>>()))
            .ReturnsAsync(new List<TestCaseRequirementLink>());
        _linkRepoMock.Setup(x => x.BulkDeleteAsync(It.IsAny<IReadOnlyCollection<TestCaseRequirementLink>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // Default: no SRS requirements (suite has no SrsDocumentId)
        _srsRequirementRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SrsRequirement>().AsQueryable());
        _srsRequirementRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync(new List<SrsRequirement>());
        _srsRequirementRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<Guid>>()))
            .ReturnsAsync(new List<Guid>());
        _proposalRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestOrderProposal>().AsQueryable());
        _proposalRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync((TestOrderProposal)null);
        _apiTestOrderServiceMock.Setup(x => x.DeserializeOrderJson(It.IsAny<string>()))
            .Returns(new List<ApiOrderItemModel>());

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _variableRepoMock.Setup(x => x.AddAsync(It.IsAny<TestCaseVariable>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new SaveAiGeneratedTestCasesCommandHandler(
            _suiteRepoMock.Object,
            _testCaseRepoMock.Object,
            _dependencyRepoMock.Object,
            _requestRepoMock.Object,
            _expectationRepoMock.Object,
            _variableRepoMock.Object,
            _versionRepoMock.Object,
            _jobRepoMock.Object,
            _linkRepoMock.Object,
            _srsRequirementRepoMock.Object,
            _proposalRepoMock.Object,
            _apiTestOrderServiceMock.Object,
            _endpointMetadataServiceMock.Object,
            new EndpointRequirementMapper(),
            _loggerMock.Object);
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
    public async Task HandleAsync_Should_PersistVariablesFromDto()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupExistingTestCases(0);

        var command = new SaveAiGeneratedTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            TestCases = new List<AiGeneratedTestCaseDto>
            {
                new()
                {
                    EndpointId = Guid.NewGuid(),
                    Name = "Login",
                    Request = new AiTestCaseRequestDto { HttpMethod = "POST", Url = "/api/auth/login" },
                    Variables = new List<AiTestCaseVariableDto>
                    {
                        new()
                        {
                            VariableName = "accessToken",
                            ExtractFrom = "ResponseBody",
                            JsonPath = "$.data.accessToken",
                        },
                        new()
                        {
                            VariableName = "userId",
                            ExtractFrom = "body",
                            JsonPath = "$.data.userId",
                            DefaultValue = "fallback-id",
                        },
                    },
                },
            },
        };

        await _handler.HandleAsync(command);

        _variableRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseVariable>(v =>
                v.VariableName == "accessToken" &&
                v.ExtractFrom == ExtractFrom.ResponseBody &&
                v.JsonPath == "$.data.accessToken"),
            It.IsAny<CancellationToken>()), Times.Once);

        _variableRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseVariable>(v =>
                v.VariableName == "userId" &&
                v.ExtractFrom == ExtractFrom.ResponseBody &&
                v.DefaultValue == "fallback-id"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_ParseExtractFrom_HeaderAndStatus()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupExistingTestCases(0);

        var command = new SaveAiGeneratedTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            TestCases = new List<AiGeneratedTestCaseDto>
            {
                new()
                {
                    EndpointId = Guid.NewGuid(),
                    Name = "Get with headers",
                    Request = new AiTestCaseRequestDto { HttpMethod = "GET", Url = "/api/test" },
                    Variables = new List<AiTestCaseVariableDto>
                    {
                        new() { VariableName = "reqId", ExtractFrom = "ResponseHeader", HeaderName = "X-Request-Id" },
                        new() { VariableName = "headerVar", ExtractFrom = "header", HeaderName = "X-Custom" },
                        new() { VariableName = "status", ExtractFrom = "Status" },
                    },
                },
            },
        };

        await _handler.HandleAsync(command);

        _variableRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseVariable>(v => v.VariableName == "reqId" && v.ExtractFrom == ExtractFrom.ResponseHeader),
            It.IsAny<CancellationToken>()), Times.Once);

        _variableRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseVariable>(v => v.VariableName == "headerVar" && v.ExtractFrom == ExtractFrom.ResponseHeader),
            It.IsAny<CancellationToken>()), Times.Once);

        _variableRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseVariable>(v => v.VariableName == "status" && v.ExtractFrom == ExtractFrom.Status),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_SkipVariables_WhenNameOrExtractFromBlank()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupExistingTestCases(0);

        var command = new SaveAiGeneratedTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            TestCases = new List<AiGeneratedTestCaseDto>
            {
                new()
                {
                    EndpointId = Guid.NewGuid(),
                    Name = "Test",
                    Request = new AiTestCaseRequestDto { HttpMethod = "GET", Url = "/api/test" },
                    Variables = new List<AiTestCaseVariableDto>
                    {
                        new() { VariableName = "", ExtractFrom = "ResponseBody", JsonPath = "$.id" },
                        new() { VariableName = "token", ExtractFrom = "", JsonPath = "$.token" },
                        new() { VariableName = null, ExtractFrom = "body", JsonPath = "$.x" },
                        new() { VariableName = "valid", ExtractFrom = "body", JsonPath = "$.val" },
                    },
                },
            },
        };

        await _handler.HandleAsync(command);

        // Only the "valid" variable should be persisted
        _variableRepoMock.Verify(x => x.AddAsync(
            It.IsAny<TestCaseVariable>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _variableRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseVariable>(v => v.VariableName == "valid"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_NotCallVariableRepo_WhenNoVariablesProvided()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupExistingTestCases(0);

        var command = CreateValidCommand(); // default: no variables

        await _handler.HandleAsync(command);

        _variableRepoMock.Verify(x => x.AddAsync(
            It.IsAny<TestCaseVariable>(),
            It.IsAny<CancellationToken>()), Times.Never);
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

    [Fact]
    public async Task HandleAsync_Should_SetPrimaryRequirementId_WhenValidCoveredRequirementIds()
    {
        var srsDocId = Guid.NewGuid();
        var reqId1 = Guid.NewGuid();
        var reqId2 = Guid.NewGuid();

        var suite = CreateSuite();
        suite.SrsDocumentId = srsDocId;
        SetupSuiteFound(suite);
        SetupExistingTestCases(0);

        // Requirement repo returns both valid IDs for this SRS document
        _srsRequirementRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SrsRequirement>
            {
                new() { Id = reqId1, SrsDocumentId = srsDocId },
                new() { Id = reqId2, SrsDocumentId = srsDocId },
            }.AsQueryable());
        _srsRequirementRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync(new List<SrsRequirement>
            {
                new() { Id = reqId1, SrsDocumentId = srsDocId },
                new() { Id = reqId2, SrsDocumentId = srsDocId },
            });
        _srsRequirementRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<Guid>>()))
            .ReturnsAsync(new List<Guid> { reqId1, reqId2 });

        TestCase capturedTestCase = null;
        _testCaseRepoMock.Setup(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()))
            .Callback<TestCase, CancellationToken>((tc, _) => capturedTestCase = tc)
            .Returns(Task.CompletedTask);
        _testCaseRepoMock.Setup(x => x.UpdateAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = CreateValidCommand();
        command.TestCases[0].CoveredRequirementIds = new List<Guid> { reqId1, reqId2 };
        command.TestCases[0].TraceabilityScore = 0.95f;
        command.TestCases[0].MappingRationale = "LLM confirmed strong match.";

        await _handler.HandleAsync(command);

        // Two links created — one per valid requirement
        _linkRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseRequirementLink>(l => l.SrsRequirementId == reqId1
                && l.TraceabilityScore == 0.95f
                && l.MappingRationale == "LLM confirmed strong match."),
            It.IsAny<CancellationToken>()), Times.Once);

        _linkRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseRequirementLink>(l => l.SrsRequirementId == reqId2),
            It.IsAny<CancellationToken>()), Times.Once);

        // PrimaryRequirementId set on the test case update
        _testCaseRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestCase>(tc => tc.PrimaryRequirementId == reqId1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_DropEndpointIrrelevantRequirementIds_WhenMetadataIsAvailable()
    {
        var srsDocId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var registerReqId = Guid.NewGuid();
        var loginReqId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        var suite = CreateSuite();
        suite.SrsDocumentId = srsDocId;
        suite.ApiSpecId = specId;
        SetupSuiteFound(suite);
        SetupExistingTestCases(0);

        var requirements = new List<SrsRequirement>
        {
            new()
            {
                Id = registerReqId,
                SrsDocumentId = srsDocId,
                RequirementCode = "REQ-002",
                Title = "Registration",
                Description = "A user can register with a valid unique email and password.",
                RequirementType = SrsRequirementType.Functional,
            },
            new()
            {
                Id = loginReqId,
                SrsDocumentId = srsDocId,
                RequirementCode = "REQ-003",
                Title = "Login",
                Description = "A user can login with the correct email and password and receives a token.",
                RequirementType = SrsRequirementType.Functional,
            },
        };

        _srsRequirementRepoMock.Setup(x => x.GetQueryableSet()).Returns(requirements.AsQueryable());
        _srsRequirementRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync(requirements);
        _endpointMetadataServiceMock
            .Setup(x => x.GetEndpointMetadataAsync(
                specId,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(endpointId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiEndpointMetadataDto>
            {
                new()
                {
                    EndpointId = endpointId,
                    HttpMethod = "POST",
                    Path = "/api/auth/login",
                    OperationId = "login",
                    Parameters = new List<ApiEndpointParameterDescriptorDto>
                    {
                        new()
                        {
                            Name = "body",
                            Location = "Body",
                            Schema = """
                            {
                              "type": "object",
                              "properties": {
                                "email": { "type": "string", "format": "email" },
                                "password": { "type": "string", "minLength": 6 }
                              }
                            }
                            """,
                        },
                    },
                },
            });

        _testCaseRepoMock.Setup(x => x.UpdateAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = CreateValidCommand();
        command.TestCases[0].EndpointId = endpointId;
        command.TestCases[0].CoveredRequirementIds = new List<Guid> { registerReqId, loginReqId };

        await _handler.HandleAsync(command);

        _linkRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseRequirementLink>(l => l.SrsRequirementId == loginReqId),
            It.IsAny<CancellationToken>()), Times.Once);
        _linkRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseRequirementLink>(l => l.SrsRequirementId == registerReqId),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_RejectInvalidRequirementIds_WhenSuiteHasSrsDoc()
    {
        var srsDocId = Guid.NewGuid();
        var validReqId = Guid.NewGuid();
        var invalidReqId = Guid.NewGuid(); // does not belong to srsDocId

        var suite = CreateSuite();
        suite.SrsDocumentId = srsDocId;
        SetupSuiteFound(suite);
        SetupExistingTestCases(0);

        _srsRequirementRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SrsRequirement>
            {
                new() { Id = validReqId, SrsDocumentId = srsDocId },
            }.AsQueryable());
        _srsRequirementRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync(new List<SrsRequirement>
            {
                new() { Id = validReqId, SrsDocumentId = srsDocId },
            });
        _srsRequirementRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<Guid>>()))
            .ReturnsAsync(new List<Guid> { validReqId });

        _testCaseRepoMock.Setup(x => x.UpdateAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = CreateValidCommand();
        command.TestCases[0].CoveredRequirementIds = new List<Guid> { validReqId, invalidReqId };

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*coveredRequirementId*does not belong*");

        _linkRepoMock.Verify(x => x.AddAsync(
            It.IsAny<TestCaseRequirementLink>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_NotRejectExpectedStatus_WhenSrsStatusDiffers()
    {
        var srsDocId = Guid.NewGuid();
        var reqId = Guid.NewGuid();

        var suite = CreateSuite();
        suite.SrsDocumentId = srsDocId;
        SetupSuiteFound(suite);
        SetupExistingTestCases(0);

        _srsRequirementRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SrsRequirement>
            {
                new()
                {
                    Id = reqId,
                    SrsDocumentId = srsDocId,
                    RequirementCode = "REQ-400",
                    TestableConstraints = """[{ "constraint": "invalid email -> 400", "expectedOutcome": "400 Bad Request", "priority": "High" }]""",
                },
            }.AsQueryable());
        _srsRequirementRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync(new List<SrsRequirement>
            {
                new()
                {
                    Id = reqId,
                    SrsDocumentId = srsDocId,
                    RequirementCode = "REQ-400",
                    TestableConstraints = """[{ "constraint": "invalid email -> 400", "expectedOutcome": "400 Bad Request", "priority": "High" }]""",
                },
            });

        var command = CreateValidCommand();
        command.TestCases[0].Name = "Invalid email should fail";
        command.TestCases[0].TestType = "Negative";
        command.TestCases[0].CoveredRequirementIds = new List<Guid> { reqId };
        command.TestCases[0].Expectation = new AiTestCaseExpectationDto
        {
            ExpectedStatus = "[200]",
        };

        var act = () => _handler.HandleAsync(command);

        await act.Should().NotThrowAsync();

        _expectationRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseExpectation>(e => e.ExpectedStatus == "[200]"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_LogWarning_WhenNoCoveredRequirementIds_ButSuiteHasSrsDoc()
    {
        var srsDocId = Guid.NewGuid();

        var suite = CreateSuite();
        suite.SrsDocumentId = srsDocId;
        SetupSuiteFound(suite);
        SetupExistingTestCases(0);

        _srsRequirementRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SrsRequirement>().AsQueryable());
        _srsRequirementRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<Guid>>()))
            .ReturnsAsync(new List<Guid>());

        var command = CreateValidCommand(); // no coveredRequirementIds

        await _handler.HandleAsync(command);

        // No links created
        _linkRepoMock.Verify(x => x.AddAsync(
            It.IsAny<TestCaseRequirementLink>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString().Contains("coveredRequirementIds")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce);
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
