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

public class UpdateSrsRequirementCommandHandlerTests
{
    private readonly Mock<IRepository<SrsRequirement, Guid>> _requirementRepoMock;
    private readonly Mock<IRepository<SrsDocument, Guid>> _srsDocumentRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UpdateSrsRequirementCommandHandler _handler;

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid DocId = Guid.NewGuid();
    private static readonly Guid ReqId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public UpdateSrsRequirementCommandHandlerTests()
    {
        _requirementRepoMock = new Mock<IRepository<SrsRequirement, Guid>>();
        _srsDocumentRepoMock = new Mock<IRepository<SrsDocument, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _requirementRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _requirementRepoMock.Setup(x => x.UpdateAsync(It.IsAny<SrsRequirement>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new UpdateSrsRequirementCommandHandler(
            _requirementRepoMock.Object,
            _srsDocumentRepoMock.Object);
    }

    private void SetupDocumentExists(Guid projectId, Guid docId)
    {
        var doc = new SrsDocument { Id = docId, ProjectId = projectId, IsDeleted = false };
        _srsDocumentRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new TestAsyncEnumerable<SrsDocument>(new List<SrsDocument> { doc }));
    }

    private void SetupDocumentNotFound()
    {
        _srsDocumentRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new TestAsyncEnumerable<SrsDocument>(new List<SrsDocument>()));
    }

    private SrsRequirement BuildRequirement(Guid? endpointId = null) => new()
    {
        Id = ReqId,
        SrsDocumentId = DocId,
        Title = "Original title",
        EndpointId = endpointId,
        IsReviewed = false,
    };

    [Fact]
    public async Task HandleAsync_DocumentBelongsToDifferentProject_ThrowsNotFoundException()
    {
        // Arrange
        SetupDocumentNotFound(); // doc not found for this ProjectId
        var command = new UpdateSrsRequirementCommand
        {
            ProjectId = Guid.NewGuid(), // different project
            SrsDocumentId = DocId,
            RequirementId = ReqId,
            CurrentUserId = UserId,
            Title = "New title",
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_DeletedDocument_ThrowsNotFoundException()
    {
        // Arrange: doc exists but is deleted
        var deletedDoc = new SrsDocument { Id = DocId, ProjectId = ProjectId, IsDeleted = true };
        _srsDocumentRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new TestAsyncEnumerable<SrsDocument>(new List<SrsDocument> { deletedDoc }));

        _requirementRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync((SrsRequirement)null);

        var command = new UpdateSrsRequirementCommand
        {
            ProjectId = ProjectId,
            SrsDocumentId = DocId,
            RequirementId = ReqId,
            CurrentUserId = UserId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_RequirementNotInDocument_ThrowsNotFoundException()
    {
        // Arrange
        SetupDocumentExists(ProjectId, DocId);
        _requirementRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync((SrsRequirement)null);

        var command = new UpdateSrsRequirementCommand
        {
            ProjectId = ProjectId,
            SrsDocumentId = DocId,
            RequirementId = ReqId,
            CurrentUserId = UserId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_ClearEndpointId_True_SetsEndpointIdToNull()
    {
        // Arrange
        var existingEndpointId = Guid.NewGuid();
        var req = BuildRequirement(endpointId: existingEndpointId);

        SetupDocumentExists(ProjectId, DocId);
        _requirementRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SrsRequirement> { req }.AsQueryable());
        _requirementRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync(req);

        var command = new UpdateSrsRequirementCommand
        {
            ProjectId = ProjectId,
            SrsDocumentId = DocId,
            RequirementId = ReqId,
            CurrentUserId = UserId,
            ClearEndpointId = true,
            EndpointId = existingEndpointId, // even if non-null, clear wins
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        req.EndpointId.Should().BeNull("ClearEndpointId=true must remove the endpoint mapping");
    }

    [Fact]
    public async Task HandleAsync_ClearEndpointId_False_WithEndpointId_SetsEndpointId()
    {
        // Arrange
        var req = BuildRequirement(endpointId: null);
        var newEndpointId = Guid.NewGuid();

        SetupDocumentExists(ProjectId, DocId);
        _requirementRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SrsRequirement> { req }.AsQueryable());
        _requirementRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync(req);

        var command = new UpdateSrsRequirementCommand
        {
            ProjectId = ProjectId,
            SrsDocumentId = DocId,
            RequirementId = ReqId,
            CurrentUserId = UserId,
            ClearEndpointId = false,
            EndpointId = newEndpointId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        req.EndpointId.Should().Be(newEndpointId);
    }

    [Fact]
    public async Task HandleAsync_ClearEndpointId_False_WithoutEndpointId_PreservesExistingMapping()
    {
        // Arrange
        var existingEndpointId = Guid.NewGuid();
        var req = BuildRequirement(endpointId: existingEndpointId);

        SetupDocumentExists(ProjectId, DocId);
        _requirementRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SrsRequirement> { req }.AsQueryable());
        _requirementRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync(req);

        var command = new UpdateSrsRequirementCommand
        {
            ProjectId = ProjectId,
            SrsDocumentId = DocId,
            RequirementId = ReqId,
            CurrentUserId = UserId,
            ClearEndpointId = false,
            // EndpointId not set
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        req.EndpointId.Should().Be(existingEndpointId, "no clear flag and no new value means preserve existing");
    }

    [Fact]
    public async Task HandleAsync_UpdateTitle_SetsTitle()
    {
        // Arrange
        var req = BuildRequirement();
        SetupDocumentExists(ProjectId, DocId);
        _requirementRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SrsRequirement> { req }.AsQueryable());
        _requirementRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync(req);

        var command = new UpdateSrsRequirementCommand
        {
            ProjectId = ProjectId,
            SrsDocumentId = DocId,
            RequirementId = ReqId,
            CurrentUserId = UserId,
            Title = "Updated requirement title",
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        req.Title.Should().Be("Updated requirement title");
    }

    [Fact]
    public async Task HandleAsync_UpdateIsReviewed_SetsReviewedFields()
    {
        // Arrange
        var req = BuildRequirement();
        SetupDocumentExists(ProjectId, DocId);
        _requirementRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SrsRequirement> { req }.AsQueryable());
        _requirementRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsRequirement>>()))
            .ReturnsAsync(req);

        var command = new UpdateSrsRequirementCommand
        {
            ProjectId = ProjectId,
            SrsDocumentId = DocId,
            RequirementId = ReqId,
            CurrentUserId = UserId,
            IsReviewed = true,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        req.IsReviewed.Should().BeTrue();
        req.ReviewedById.Should().Be(UserId);
        req.ReviewedAt.Should().NotBeNull();
    }
}
