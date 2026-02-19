using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class ArchiveTestSuiteScopeCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ArchiveTestSuiteScopeCommandHandler _handler;

    public ArchiveTestSuiteScopeCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _suiteRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _suiteRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestSuite>().AsQueryable());

        _handler = new ArchiveTestSuiteScopeCommandHandler(
            _suiteRepoMock.Object,
            new Mock<ILogger<ArchiveTestSuiteScopeCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Should_SetStatusToArchived()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var suite = new TestSuite
        {
            Id = suiteId,
            ProjectId = projectId,
            CreatedById = userId,
            Name = "Suite",
            Status = TestSuiteStatus.Draft,
        };

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);

        var command = new ArchiveTestSuiteScopeCommand
        {
            SuiteId = suiteId,
            ProjectId = projectId,
            CurrentUserId = userId,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        suite.Status.Should().Be(TestSuiteStatus.Archived);
        suite.LastModifiedById.Should().Be(userId);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFoundException_WhenSuiteNotFound()
    {
        // Arrange
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync((TestSuite)null);

        var command = new ArchiveTestSuiteScopeCommand
        {
            SuiteId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenNotOwner()
    {
        // Arrange
        var suite = new TestSuite
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid(),
            Name = "Suite",
        };

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);

        var command = new ArchiveTestSuiteScopeCommand
        {
            SuiteId = suite.Id,
            ProjectId = suite.ProjectId,
            CurrentUserId = Guid.NewGuid(), // different user
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
