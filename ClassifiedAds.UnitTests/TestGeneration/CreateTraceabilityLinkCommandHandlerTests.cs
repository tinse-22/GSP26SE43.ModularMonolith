using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.UnitTests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class CreateTraceabilityLinkCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestCase, Guid>> _testCaseRepoMock;
    private readonly Mock<IRepository<SrsRequirement, Guid>> _requirementRepoMock;
    private readonly Mock<IRepository<SrsDocument, Guid>> _srsDocumentRepoMock;
    private readonly Mock<IRepository<TestCaseRequirementLink, Guid>> _linkRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateTraceabilityLinkCommandHandler _handler;

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid SuiteId = Guid.NewGuid();
    private static readonly Guid TestCaseId = Guid.NewGuid();
    private static readonly Guid ReqId = Guid.NewGuid();
    private static readonly Guid DocId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public CreateTraceabilityLinkCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _testCaseRepoMock = new Mock<IRepository<TestCase, Guid>>();
        _requirementRepoMock = new Mock<IRepository<SrsRequirement, Guid>>();
        _srsDocumentRepoMock = new Mock<IRepository<SrsDocument, Guid>>();
        _linkRepoMock = new Mock<IRepository<TestCaseRequirementLink, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _linkRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _linkRepoMock.Setup(x => x.AddAsync(It.IsAny<TestCaseRequirementLink>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new CreateTraceabilityLinkCommandHandler(
            _suiteRepoMock.Object,
            _testCaseRepoMock.Object,
            _requirementRepoMock.Object,
            _srsDocumentRepoMock.Object,
            _linkRepoMock.Object);
    }

    private CreateTraceabilityLinkCommand ValidCommand() => new()
    {
        ProjectId = ProjectId,
        TestSuiteId = SuiteId,
        TestCaseId = TestCaseId,
        SrsRequirementId = ReqId,
        CurrentUserId = UserId,
    };

    private void SetupValidSuite()
    {
        var suite = new TestSuite { Id = SuiteId, ProjectId = ProjectId, SrsDocumentId = DocId };
        _suiteRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestSuite> { suite }.AsQueryable());
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
    }

    private void SetupValidTestCase()
    {
        var tc = new TestCase { Id = TestCaseId, TestSuiteId = SuiteId, Name = "TC-001" };
        _testCaseRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestCase> { tc }.AsQueryable());
        _testCaseRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync(tc);
    }

    private void SetupValidRequirement()
    {
        var req = new SrsRequirement { Id = ReqId, SrsDocumentId = DocId, RequirementCode = "REQ-001" };
        _requirementRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SrsRequirement> { req }.AsQueryable());
        _requirementRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync(req);
    }

    private void SetupNoDuplicateLink()
    {
        _linkRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new TestAsyncEnumerable<TestCaseRequirementLink>(new List<TestCaseRequirementLink>()));
    }

    [Fact]
    public async Task HandleAsync_SuiteNotInProject_ThrowsNotFoundException()
    {
        // Arrange: suite lookup returns null (wrong project)
        _suiteRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestSuite>().AsQueryable());
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync((TestSuite)null);

        // Act
        var act = () => _handler.HandleAsync(ValidCommand());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_TestCaseNotInSuite_ThrowsNotFoundException()
    {
        // Arrange
        SetupValidSuite();

        // Test case not in this suite
        _testCaseRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestCase>().AsQueryable());
        _testCaseRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync((TestCase)null);

        // Act
        var act = () => _handler.HandleAsync(ValidCommand());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_SuiteHasNoSrsDocument_ThrowsValidationException()
    {
        // Arrange: suite has no SrsDocumentId
        var suite = new TestSuite { Id = SuiteId, ProjectId = ProjectId, SrsDocumentId = null };
        _suiteRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestSuite> { suite }.AsQueryable());
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);

        SetupValidTestCase();

        // No linked doc via reverse lookup either
        _srsDocumentRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SrsDocument>().AsQueryable());
        _srsDocumentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsDocument>>()))
            .ReturnsAsync((SrsDocument)null);

        // Act
        var act = () => _handler.HandleAsync(ValidCommand());

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_RequirementDoesNotBelongToSrsDocument_ThrowsNotFoundException()
    {
        // Arrange
        SetupValidSuite();
        SetupValidTestCase();

        // Requirement not in this doc
        _requirementRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SrsRequirement>().AsQueryable());
        _requirementRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync((SrsRequirement)null);

        // Act
        var act = () => _handler.HandleAsync(ValidCommand());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_DuplicateLinkAlreadyExists_ThrowsValidationException()
    {
        // Arrange
        SetupValidSuite();
        SetupValidTestCase();
        SetupValidRequirement();

        // Duplicate link exists
        var existingLink = new TestCaseRequirementLink { TestCaseId = TestCaseId, SrsRequirementId = ReqId };
        _linkRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new TestAsyncEnumerable<TestCaseRequirementLink>(new List<TestCaseRequirementLink> { existingLink }));

        // Act
        var act = () => _handler.HandleAsync(ValidCommand());

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_ValidInput_CreatesLinkAndPopulatesResult()
    {
        // Arrange
        SetupValidSuite();
        SetupValidTestCase();
        SetupValidRequirement();
        SetupNoDuplicateLink();

        var command = ValidCommand();

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _linkRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseRequirementLink>(l =>
                l.TestCaseId == TestCaseId &&
                l.SrsRequirementId == ReqId),
            It.IsAny<CancellationToken>()), Times.Once);

        command.Result.Should().NotBeNull();
        command.Result.TestCaseId.Should().Be(TestCaseId);
        command.Result.SrsRequirementId.Should().Be(ReqId);
        command.Result.RequirementCode.Should().Be("REQ-001");
        command.Result.TestCaseName.Should().Be("TC-001");
    }
}
