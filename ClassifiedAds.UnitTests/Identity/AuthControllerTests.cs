using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Contracts.Notification.DTOs;
using ClassifiedAds.Contracts.Notification.Services;
using ClassifiedAds.Infrastructure.Storages;
using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.Controllers;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.IdentityProviders.Google;
using ClassifiedAds.Modules.Identity.Models;
using ClassifiedAds.Modules.Identity.Persistence;
using ClassifiedAds.Modules.Identity.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Identity;

public class AuthControllerTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly Mock<UserManager<User>> _userManagerMock;
    private readonly Mock<RoleManager<Role>> _roleManagerMock;
    private readonly Mock<SignInManager<User>> _signInManagerMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly Mock<IEmailMessageService> _emailMessageServiceMock;
    private readonly Mock<IEmailTemplateService> _emailTemplateServiceMock;
    private readonly Mock<IFileStorageManager> _fileStorageManagerMock;
    private readonly Mock<ITokenBlacklistService> _tokenBlacklistServiceMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _dbContext = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);

        _userManagerMock = CreateUserManagerMock();
        _roleManagerMock = CreateRoleManagerMock();
        _signInManagerMock = CreateSignInManagerMock(_userManagerMock.Object);
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        _emailMessageServiceMock = new Mock<IEmailMessageService>();
        _emailTemplateServiceMock = new Mock<IEmailTemplateService>();
        _fileStorageManagerMock = new Mock<IFileStorageManager>();
        _tokenBlacklistServiceMock = new Mock<ITokenBlacklistService>();

        _emailTemplateServiceMock
            .Setup(x => x.WelcomeConfirmEmail(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("welcome-email");

        var optionsSnapshotMock = new Mock<IOptionsSnapshot<IdentityModuleOptions>>();
        optionsSnapshotMock
            .Setup(x => x.Value)
            .Returns(new IdentityModuleOptions
            {
                IdentityServer = new IdentityServerOptions
                {
                    FrontendUrl = "http://localhost:5174",
                },
            });

        var dispatcher = new Dispatcher(new Mock<IServiceProvider>().Object);

        _controller = new AuthController(
            _userManagerMock.Object,
            _roleManagerMock.Object,
            _signInManagerMock.Object,
            _jwtTokenServiceMock.Object,
            _emailMessageServiceMock.Object,
            _emailTemplateServiceMock.Object,
            optionsSnapshotMock.Object,
            _dbContext,
            dispatcher,
            _fileStorageManagerMock.Object,
            _tokenBlacklistServiceMock.Object,
            new Mock<IServiceScopeFactory>().Object,
            new Mock<ILogger<AuthController>>().Object,
            new GoogleIdentityProvider(new GoogleOptions { ClientId = "test" }));
    }

    [Fact]
    public async Task Register_Should_AssignOnlyUserRole_WhenSelfRegistering()
    {
        var model = new RegisterModel
        {
            Email = "self.registered@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
        };

        User createdUser = null!;
        IEnumerable<string> assignedRoles = Array.Empty<string>();

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
            .Callback<User, IEnumerable<string>>((_, roles) => assignedRoles = roles.ToArray())
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<User>()))
            .ReturnsAsync("confirm-token");

        _roleManagerMock
            .Setup(x => x.FindByNameAsync("User"))
            .ReturnsAsync(new Role { Name = "User", NormalizedName = "USER" });

        var result = await _controller.Register(model);

        createdUser.Should().NotBeNull();
        createdUser.EmailConfirmed.Should().BeFalse();
        assignedRoles.Should().BeEquivalentTo(new[] { "User" });

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        var payload = createdResult.Value.Should().BeOfType<RegisterResponseModel>().Subject;
        payload.Email.Should().Be(model.Email);
        payload.EmailConfirmationRequired.Should().BeTrue();

        var profile = await _dbContext.UserProfiles.SingleAsync(x => x.UserId == createdUser.Id);
        profile.DisplayName.Should().Be("self.registered");

        _emailMessageServiceMock.Verify(
            x => x.CreateEmailMessageAsync(It.Is<EmailMessageDTO>(email =>
                email.Tos == model.Email &&
                email.Body == "welcome-email")),
            Times.Once);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
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

    private static Mock<SignInManager<User>> CreateSignInManagerMock(UserManager<User> userManager)
    {
        return new Mock<SignInManager<User>>(
            userManager,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<User>>().Object,
            Options.Create(new IdentityOptions()),
            new Mock<ILogger<SignInManager<User>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<User>>().Object);
    }
}
