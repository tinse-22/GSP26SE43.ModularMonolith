using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class UpdateSrsDocumentCommandHandlerTests
{
    private readonly Mock<IRepository<SrsDocument, Guid>> _srsDocumentRepoMock;
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UpdateSrsDocumentCommandHandler _handler;

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid DocId = Guid.NewGuid();
    private static readonly Guid SuiteId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public UpdateSrsDocumentCommandHandlerTests()
    {
        _srsDocumentRepoMock = new Mock<IRepository<SrsDocument, Guid>>();
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _srsDocumentRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _srsDocumentRepoMock.Setup(x => x.UpdateAsync(It.IsAny<SrsDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _suiteRepoMock.Setup(x => x.UpdateAsync(It.IsAny<TestSuite>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new UpdateSrsDocumentCommandHandler(
            _srsDocumentRepoMock.Object,
            _suiteRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_DocumentNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _srsDocumentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsDocument>>()))
            .ReturnsAsync((SrsDocument)null);

        var command = new UpdateSrsDocumentCommand
        {
            ProjectId = ProjectId,
            SrsDocumentId = DocId,
            CurrentUserId = UserId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_LinkToSuiteInDifferentProject_ThrowsNotFoundException()
    {
        // Arrange: doc exists in ProjectId, but suite belongs to a different project
        var doc = new SrsDocument { Id = DocId, ProjectId = ProjectId, IsDeleted = false };
        _srsDocumentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsDocument>>()))
            .ReturnsAsync(doc);

        // Suite does NOT match ProjectId in Where clause — simulate by returning null
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync((TestSuite)null);

        var command = new UpdateSrsDocumentCommand
        {
            ProjectId = ProjectId,
            SrsDocumentId = DocId,
            CurrentUserId = UserId,
            TestSuiteId = SuiteId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>($"TestSuite {SuiteId} belongs to a different project");
    }

    [Fact]
    public async Task HandleAsync_ClearTestSuiteId_True_RemovesLink()
    {
        // Arrange
        var doc = new SrsDocument { Id = DocId, ProjectId = ProjectId, IsDeleted = false, TestSuiteId = SuiteId };
        _srsDocumentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsDocument>>()))
            .ReturnsAsync(doc);

        // Old suite exists and had back-reference
        var oldSuite = new TestSuite { Id = SuiteId, ProjectId = ProjectId, SrsDocumentId = DocId };
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(oldSuite);

        var command = new UpdateSrsDocumentCommand
        {
            ProjectId = ProjectId,
            SrsDocumentId = DocId,
            CurrentUserId = UserId,
            ClearTestSuiteId = true,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        doc.TestSuiteId.Should().BeNull("ClearTestSuiteId=true must remove the suite link");
        oldSuite.SrsDocumentId.Should().BeNull("reverse FK on old suite must be cleared");
    }

    [Fact]
    public async Task HandleAsync_LinkToNewSuite_SetsBothFKs()
    {
        // Arrange: doc has no suite initially
        var doc = new SrsDocument { Id = DocId, ProjectId = ProjectId, IsDeleted = false, TestSuiteId = null };
        _srsDocumentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsDocument>>()))
            .ReturnsAsync(doc);

        var newSuite = new TestSuite { Id = SuiteId, ProjectId = ProjectId, SrsDocumentId = null };
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(newSuite);

        var command = new UpdateSrsDocumentCommand
        {
            ProjectId = ProjectId,
            SrsDocumentId = DocId,
            CurrentUserId = UserId,
            TestSuiteId = SuiteId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        doc.TestSuiteId.Should().Be(SuiteId);
        newSuite.SrsDocumentId.Should().Be(DocId, "reverse FK on new suite must be set");
    }

    [Fact]
    public async Task HandleAsync_NoSuiteChange_SavesWithoutError()
    {
        // Arrange: doc has no suite and no suite provided
        var doc = new SrsDocument { Id = DocId, ProjectId = ProjectId, IsDeleted = false, TestSuiteId = null };
        _srsDocumentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsDocument>>()))
            .ReturnsAsync(doc);

        var command = new UpdateSrsDocumentCommand
        {
            ProjectId = ProjectId,
            SrsDocumentId = DocId,
            CurrentUserId = UserId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert: no exception, SaveChanges called
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
