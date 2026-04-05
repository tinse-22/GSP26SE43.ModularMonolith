using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Notification.DTOs;
using ClassifiedAds.Contracts.Notification.Services;
using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.Controllers;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Identity;

public class UsersControllerTests
{
    private readonly Mock<UserManager<User>> _userManagerMock;
    private readonly Mock<RoleManager<Role>> _roleManagerMock;
    private readonly Mock<IEmailMessageService> _emailMessageServiceMock;
    private readonly Mock<IEmailTemplateService> _emailTemplateServiceMock;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _userManagerMock = CreateUserManagerMock();
        _roleManagerMock = CreateRoleManagerMock();
        _emailMessageServiceMock = new Mock<IEmailMessageService>();
        _emailTemplateServiceMock = new Mock<IEmailTemplateService>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        var dispatcher = new Dispatcher(serviceProviderMock.Object);
        var loggerMock = new Mock<ILogger<UsersController>>();
        var dateTimeProviderMock = new Mock<IDateTimeProvider>();
        var moduleOptionsSnapshotMock = new Mock<IOptionsSnapshot<IdentityModuleOptions>>();
        moduleOptionsSnapshotMock
            .Setup(x => x.Value)
            .Returns(new IdentityModuleOptions
            {
                IdentityServer = new IdentityServerOptions
                {
                    FrontendUrl = "http://localhost:5174",
                },
            });

        _controller = new UsersController(
            dispatcher,
            _userManagerMock.Object,
            _roleManagerMock.Object,
            loggerMock.Object,
            dateTimeProviderMock.Object,
            _emailMessageServiceMock.Object,
            _emailTemplateServiceMock.Object,
            moduleOptionsSnapshotMock.Object);
    }

    [Fact]
    public async Task Post_Should_KeepRequestedRolesAndAutoConfirmEmail_WhenRequestContainsOnlyUserRole()
    {
        // Arrange
        var model = new CreateUserModel
        {
            UserName = "new.user",
            Email = "new.user@example.com",
            Password = "Password123!",
            Roles = new List<string> { "User" },
        };

        IEnumerable<string> assignedRoles = Array.Empty<string>();
        User createdUser = null!;

        _roleManagerMock
            .Setup(x => x.FindByNameAsync("User"))
            .ReturnsAsync(new Role { Name = "User", NormalizedName = "USER" });
        _roleManagerMock
            .Setup(x => x.FindByNameAsync("Admin"))
            .ReturnsAsync(new Role { Name = "Admin", NormalizedName = "ADMIN" });
        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync((User)null!);
        _userManagerMock
            .Setup(x => x.CreateAsync(It.IsAny<User>(), model.Password))
            .Callback<User, string>((user, _) =>
            {
                user.Id = Guid.NewGuid();
                createdUser = user;
            })
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(x => x.AddToRolesAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()))
            .Callback<User, IEnumerable<string>>((_, roles) => assignedRoles = roles)
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _controller.Post(model);

        // Assert
        createdUser.EmailConfirmed.Should().BeTrue();
        assignedRoles.Should().ContainSingle().Which.Should().Be("User");

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Value.Should().NotBeNull();

        GetPropertyValue<bool>(createdResult.Value!, "EmailConfirmationRequired").Should().BeFalse();
        GetPropertyValue<string>(createdResult.Value!, "Message")
            .Should().Be("Tạo người dùng thành công với vai trò: User. Email đã được xác nhận tự động.");
        GetPropertyValue<UserModel>(createdResult.Value!, "User").EmailConfirmed.Should().BeTrue();

        _userManagerMock.Verify(
            x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<User>()),
            Times.Never);
        _emailMessageServiceMock.Verify(
            x => x.CreateEmailMessageAsync(It.IsAny<EmailMessageDTO>()),
            Times.Never);
        _emailTemplateServiceMock.Verify(
            x => x.WelcomeConfirmEmail(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_Should_AssignDefaultAdminRole_AndKeepEmailConfirmed_WhenRolesAreEmpty()
    {
        // Arrange
        var model = new CreateUserModel
        {
            UserName = "another.user",
            Email = "another.user@example.com",
            Password = "Password123!",
            Roles = new List<string>(),
        };

        IEnumerable<string> assignedRoles = Array.Empty<string>();
        User createdUser = null!;

        _roleManagerMock
            .Setup(x => x.FindByNameAsync("Admin"))
            .ReturnsAsync(new Role { Name = "Admin", NormalizedName = "ADMIN" });
        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync((User)null!);
        _userManagerMock
            .Setup(x => x.CreateAsync(It.IsAny<User>(), model.Password))
            .Callback<User, string>((user, _) =>
            {
                user.Id = Guid.NewGuid();
                createdUser = user;
            })
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(x => x.AddToRolesAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()))
            .Callback<User, IEnumerable<string>>((_, roles) => assignedRoles = roles)
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _controller.Post(model);

        // Assert
        createdUser.EmailConfirmed.Should().BeTrue();
        assignedRoles.Should().ContainSingle().Which.Should().Be("Admin");

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        GetPropertyValue<bool>(createdResult.Value!, "EmailConfirmationRequired").Should().BeFalse();

        _userManagerMock.Verify(
            x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<User>()),
            Times.Never);
        _emailMessageServiceMock.Verify(
            x => x.CreateEmailMessageAsync(It.IsAny<EmailMessageDTO>()),
            Times.Never);
    }

    private static Mock<UserManager<User>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<User>>();
        return new Mock<UserManager<User>>(
            store.Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<User>>>().Object);
    }

    private static Mock<RoleManager<Role>> CreateRoleManagerMock()
    {
        var store = new Mock<IRoleStore<Role>>();
        return new Mock<RoleManager<Role>>(
            store.Object,
            Array.Empty<IRoleValidator<Role>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new Mock<ILogger<RoleManager<Role>>>().Object);
    }

    private static T GetPropertyValue<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"property '{propertyName}' should exist on the response payload");
        return (T)property!.GetValue(instance)!;
    }
}
