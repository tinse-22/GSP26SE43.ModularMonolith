using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Identity;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Identity;

public class CustomIdentityStoreTests
{
    [Fact]
    public async Task UserStore_CreateAsync_ShouldAddUserAndPersist_WhenIdIsPreAssigned()
    {
        // Arrange
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock.SetupGet(x => x.UnitOfWork).Returns(unitOfWorkMock.Object);

        using var dbContext = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().Options);
        var userStore = new UserStore(userRepositoryMock.Object, dbContext);

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "new.user@example.com",
            Email = "new.user@example.com",
        };
        var cancellationToken = new CancellationTokenSource().Token;

        // Act
        var result = await userStore.CreateAsync(user, cancellationToken);

        // Assert
        result.Succeeded.Should().BeTrue();
        user.ConcurrencyStamp.Should().NotBeNullOrWhiteSpace();
        user.SecurityStamp.Should().NotBeNullOrWhiteSpace();
        user.Tokens.Should().NotBeNull();
        user.Claims.Should().NotBeNull();
        user.UserRoles.Should().NotBeNull();
        user.UserLogins.Should().NotBeNull();

        userRepositoryMock.Verify(
            x => x.AddAsync(user, cancellationToken),
            Times.Once);
        userRepositoryMock.Verify(
            x => x.AddOrUpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task RoleStore_CreateAsync_ShouldAddRoleAndPersist_WhenIdIsPreAssigned()
    {
        // Arrange
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var roleRepositoryMock = new Mock<IRoleRepository>();
        roleRepositoryMock.SetupGet(x => x.UnitOfWork).Returns(unitOfWorkMock.Object);

        var roleStore = new RoleStore(roleRepositoryMock.Object);
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = "Reviewer",
            NormalizedName = "REVIEWER",
        };
        var cancellationToken = new CancellationTokenSource().Token;

        // Act
        var result = await roleStore.CreateAsync(role, cancellationToken);

        // Assert
        result.Succeeded.Should().BeTrue();
        role.ConcurrencyStamp.Should().NotBeNullOrWhiteSpace();

        roleRepositoryMock.Verify(
            x => x.AddAsync(role, cancellationToken),
            Times.Once);
        roleRepositoryMock.Verify(
            x => x.AddOrUpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(cancellationToken),
            Times.Once);
    }
}
