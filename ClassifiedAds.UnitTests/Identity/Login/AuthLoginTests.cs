using ClassifiedAds.Application;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Identity.Login;

public class AuthLoginTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly Mock<UserManager<User>> _userManagerMock;
    private readonly Mock<RoleManager<Role>> _roleManagerMock;
    private readonly Mock<SignInManager<User>> _signInManagerMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly AuthController _controller;

    public AuthLoginTests()
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

        var optionsSnapshotMock = new Mock<IOptionsSnapshot<IdentityModuleOptions>>();
        optionsSnapshotMock
            .Setup(x => x.Value)
            .Returns(new IdentityModuleOptions
            {
                IdentityServer = new IdentityServerOptions
                {
                    FrontendUrl = "http://localhost:5174",
                },
                Jwt = new JwtOptions
                {
                    RefreshTokenExpirationDays = 7,
                },
            });

        _controller = new AuthController(
            _userManagerMock.Object,
            _roleManagerMock.Object,
            _signInManagerMock.Object,
            _jwtTokenServiceMock.Object,
            new Mock<IEmailMessageService>().Object,
            new Mock<IEmailTemplateService>().Object,
            optionsSnapshotMock.Object,
            _dbContext,
            new Dispatcher(new Mock<IServiceProvider>().Object),
            new Mock<IFileStorageManager>().Object,
            new Mock<ITokenBlacklistService>().Object,
            new Mock<IServiceScopeFactory>().Object,
            new Mock<ILogger<AuthController>>().Object,
            new GoogleIdentityProvider(new GoogleOptions { ClientId = "test" }));

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        httpContext.Request.Headers.Origin = "http://localhost:3000";
        httpContext.Request.Headers.Referer = "http://localhost:3000/login";
        httpContext.Request.Headers.UserAgent = "unit-test";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };
    }

    [Fact]
    public async Task Login_Should_ReturnBadRequest_WhenModelStateIsInvalid()
    {
        var model = new LoginModel();
        _controller.ModelState.AddModelError("Email", "Email is required");

        var result = await _controller.Login(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_Should_ReturnUnauthorized_WhenUserDoesNotExist()
    {
        var model = new LoginModel
        {
            Email = "missing@example.com",
            Password = "Password123!",
        };

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync((User)null!);

        var result = await _controller.Login(model);

        var unauthorized = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetPropertyValue<string>(unauthorized.Value!, "Code").Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Login_Should_ReturnBadRequest_WhenEmailIsNotConfirmed()
    {
        var model = new LoginModel
        {
            Email = "pending@example.com",
            Password = "Password123!",
        };
        var user = CreateUser(model.Email, emailConfirmed: false);

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync(user);

        var result = await _controller.Login(model);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        GetPropertyValue<string>(badRequest.Value!, "Code").Should().Be("email_not_confirmed");
    }

    [Fact]
    public async Task Login_Should_ReturnBadRequest_WhenAccountIsLocked()
    {
        var model = new LoginModel
        {
            Email = "locked@example.com",
            Password = "Password123!",
        };
        var user = CreateUser(model.Email);

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(x => x.IsLockedOutAsync(user))
            .ReturnsAsync(true);

        var result = await _controller.Login(model);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        GetPropertyValue<string>(badRequest.Value!, "Code").Should().Be("account_locked");
    }

    [Fact]
    public async Task Login_Should_ReturnUnauthorized_WhenPasswordIsInvalid()
    {
        var model = new LoginModel
        {
            Email = "user@example.com",
            Password = "WrongPassword!",
        };
        var user = CreateUser(model.Email);

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(x => x.IsLockedOutAsync(user))
            .ReturnsAsync(false);
        _userManagerMock
            .Setup(x => x.GetAccessFailedCountAsync(user))
            .ReturnsAsync(2);
        _signInManagerMock
            .Setup(x => x.CheckPasswordSignInAsync(user, model.Password, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var result = await _controller.Login(model);

        var unauthorized = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetPropertyValue<string>(unauthorized.Value!, "Code").Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Login_Should_ReturnOkAndSetRefreshCookie_WhenCredentialsAreValid()
    {
        var model = new LoginModel
        {
            Email = "user@example.com",
            Password = "Password123!",
        };
        var user = CreateUser(model.Email);
        var roles = new List<string> { "User" };

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(x => x.IsLockedOutAsync(user))
            .ReturnsAsync(false);
        _userManagerMock
            .Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(roles);
        _signInManagerMock
            .Setup(x => x.CheckPasswordSignInAsync(user, model.Password, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _jwtTokenServiceMock
            .Setup(x => x.GenerateTokensAsync(user, It.Is<IList<string>>(r => r.Count == 1 && r[0] == "User")))
            .ReturnsAsync(("access-token", "refresh-token", 3600));

        var result = await _controller.Login(model);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<LoginResponseModel>().Subject;
        payload.AccessToken.Should().Be("access-token");
        payload.TokenType.Should().Be("Bearer");
        payload.ExpiresIn.Should().Be(3600);
        payload.User.Email.Should().Be(model.Email);
        payload.User.DisplayName.Should().Be("user");
        payload.User.Roles.Should().ContainSingle().Which.Should().Be("User");
        _controller.Response.Headers["Set-Cookie"].ToString().Should().Contain("ca_refresh_token=refresh-token");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private static User CreateUser(string email, bool emailConfirmed = true)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            EmailConfirmed = emailConfirmed,
            LockoutEnabled = true,
            CreatedDateTime = DateTimeOffset.UtcNow,
        };
    }

    private static T GetPropertyValue<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"property '{propertyName}' should exist on the response payload");
        return (T)property!.GetValue(instance)!;
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
