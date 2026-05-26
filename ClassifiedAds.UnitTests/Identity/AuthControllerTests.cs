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
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly Mock<IServiceProvider> _scopedServiceProviderMock;
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
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceScopeMock = new Mock<IServiceScope>();
        _scopedServiceProviderMock = new Mock<IServiceProvider>();

        _emailTemplateServiceMock
            .Setup(x => x.WelcomeConfirmEmail(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("welcome-email");
        _emailTemplateServiceMock
            .Setup(x => x.ForgotPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("forgot-password-email");
        _emailTemplateServiceMock
            .Setup(x => x.PasswordChanged(It.IsAny<string>()))
            .Returns("password-changed-email");
        _emailTemplateServiceMock
            .Setup(x => x.ResendConfirmEmail(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("resend-confirm-email");
        _emailMessageServiceMock
            .Setup(x => x.CreateEmailMessageAsync(It.IsAny<EmailMessageDTO>()))
            .Returns(Task.CompletedTask);

        var optionsSnapshotMock = new Mock<IOptionsSnapshot<IdentityModuleOptions>>();
        optionsSnapshotMock
            .Setup(x => x.Value)
            .Returns(new IdentityModuleOptions
            {
                IdentityServer = new IdentityServerOptions
                {
                    FrontendUrl = "http://localhost:5174",
                    Authority = "https://localhost:44367",
                },
                Jwt = new JwtOptions
                {
                    RefreshTokenExpirationDays = 7,
                },
            });

        var dispatcher = new Dispatcher(new Mock<IServiceProvider>().Object);

        _scopedServiceProviderMock
            .Setup(x => x.GetService(typeof(IdentityDbContext)))
            .Returns(_dbContext);
        _scopedServiceProviderMock
            .Setup(x => x.GetService(typeof(UserManager<User>)))
            .Returns(_userManagerMock.Object);
        _serviceScopeMock
            .SetupGet(x => x.ServiceProvider)
            .Returns(_scopedServiceProviderMock.Object);
        _scopeFactoryMock
            .Setup(x => x.CreateScope())
            .Returns(_serviceScopeMock.Object);

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
            _scopeFactoryMock.Object,
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

    [Fact]
    public async Task ConfirmEmail_Should_ReturnOk_WhenTokenIsValid()
    {
        var model = new ConfirmEmailModel
        {
            Email = "confirmed@example.com",
            Token = "valid-token",
        };
        var user = CreateUser(model.Email, emailConfirmed: false);

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(x => x.ConfirmEmailAsync(user, It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.ConfirmEmail(model);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        GetPropertyValue<string>(ok.Value!, "Message").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ConfirmEmail_Should_ReturnBadRequest_WhenTokenIsInvalid()
    {
        var model = new ConfirmEmailModel
        {
            Email = "invalid@example.com",
            Token = "invalid-token",
        };
        var user = CreateUser(model.Email, emailConfirmed: false);

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(x => x.ConfirmEmailAsync(user, It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token" }));

        var result = await _controller.ConfirmEmail(model);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ResendConfirmationEmail_Should_ReturnOk_WhenUserDoesNotExist()
    {
        var model = new ForgotPasswordModel { Email = "missing@example.com" };

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync((User)null!);

        var result = await _controller.ResendConfirmationEmail(model);

        result.Should().BeOfType<OkObjectResult>();
        _emailMessageServiceMock.Verify(
            x => x.CreateEmailMessageAsync(It.IsAny<EmailMessageDTO>()),
            Times.Never);
    }

    [Fact]
    public async Task ResendConfirmationEmail_Should_SendEmail_WhenUserIsUnconfirmed()
    {
        var model = new ForgotPasswordModel { Email = "pending@example.com" };
        var user = CreateUser(model.Email, emailConfirmed: false);

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(x => x.GenerateEmailConfirmationTokenAsync(user))
            .ReturnsAsync("confirm-token");

        var result = await _controller.ResendConfirmationEmail(model);

        result.Should().BeOfType<OkObjectResult>();
        _emailMessageServiceMock.Verify(
            x => x.CreateEmailMessageAsync(It.Is<EmailMessageDTO>(email =>
                email.Tos == model.Email &&
                email.Body == "resend-confirm-email")),
            Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_Should_ReturnOk_WhenUserDoesNotExist()
    {
        var model = new ForgotPasswordModel { Email = "missing@example.com" };

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync((User)null!);

        var result = await _controller.ForgotPassword(model);

        result.Should().BeOfType<OkObjectResult>();
        _emailMessageServiceMock.Verify(
            x => x.CreateEmailMessageAsync(It.IsAny<EmailMessageDTO>()),
            Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_Should_SendResetEmail_WhenUserExists()
    {
        var model = new ForgotPasswordModel { Email = "user@example.com" };
        var user = CreateUser(model.Email);

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(x => x.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync("reset-token");

        var result = await _controller.ForgotPassword(model);

        result.Should().BeOfType<OkObjectResult>();
        _emailMessageServiceMock.Verify(
            x => x.CreateEmailMessageAsync(It.Is<EmailMessageDTO>(email =>
                email.Tos == model.Email &&
                email.Body == "forgot-password-email")),
            Times.Once);
    }

    [Fact]
    public async Task ResetPassword_Should_ReturnBadRequest_WhenUserDoesNotExist()
    {
        var model = new ResetPasswordModel
        {
            Email = "missing@example.com",
            Token = "reset-token",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!",
        };

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync((User)null!);

        var result = await _controller.ResetPassword(model);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ResetPassword_Should_SendConfirmationEmail_WhenResetSucceeds()
    {
        var model = new ResetPasswordModel
        {
            Email = "user@example.com",
            Token = "reset-token",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!",
        };
        var user = CreateUser(model.Email);

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(x => x.ResetPasswordAsync(user, It.IsAny<string>(), model.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.ResetPassword(model);

        result.Should().BeOfType<OkObjectResult>();
        _emailMessageServiceMock.Verify(
            x => x.CreateEmailMessageAsync(It.Is<EmailMessageDTO>(email =>
                email.Tos == model.Email &&
                email.Body == "password-changed-email")),
            Times.Once);
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnBadRequest_WhenTokenMissing()
    {
        var result = await _controller.RefreshToken();

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnUnauthorized_WhenRotationFails()
    {
        _jwtTokenServiceMock
            .Setup(x => x.ValidateAndRotateRefreshTokenAsync("expired-token"))
            .ReturnsAsync(((string, string, int, ClaimsPrincipal)?)null);

        var result = await _controller.RefreshToken(new RefreshTokenModel
        {
            RefreshToken = "expired-token",
        });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        _controller.Response.Headers["Set-Cookie"].ToString().Should().Contain("ca_refresh_token=");
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnOkAndRotateCookie_WhenTokenValid()
    {
        var user = CreateUser("refresh@example.com");
        var roles = new List<string> { "User" };
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));

        _userManagerMock
            .Setup(x => x.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(roles);
        _jwtTokenServiceMock
            .Setup(x => x.ValidateAndRotateRefreshTokenAsync("valid-refresh"))
            .ReturnsAsync(("new-access-token", "new-refresh-token", 3600, principal));

        var result = await _controller.RefreshToken(new RefreshTokenModel
        {
            RefreshToken = "valid-refresh",
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<LoginResponseModel>().Subject;
        payload.AccessToken.Should().Be("new-access-token");
        payload.User.Email.Should().Be(user.Email);
        _controller.Response.Headers["Set-Cookie"].ToString().Should().Contain("ca_refresh_token=new-refresh-token");
    }

    [Fact]
    public async Task Logout_Should_RevokeRefreshToken_ClearCookie_AndBlacklistAccessToken()
    {
        var userId = Guid.NewGuid();
        var expectedExpiration = DateTimeOffset.UtcNow.AddMinutes(30);
        SetAuthenticatedUser(
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("jti", "jwt-id"),
            new Claim("exp", expectedExpiration.ToUnixTimeSeconds().ToString()));

        var result = await _controller.Logout();

        result.Should().BeOfType<OkObjectResult>();
        _jwtTokenServiceMock.Verify(x => x.RevokeRefreshTokenAsync(userId), Times.Once);
        _tokenBlacklistServiceMock.Verify(
            x => x.BlacklistToken("jwt-id", It.Is<DateTimeOffset>(d => d.ToUnixTimeSeconds() == expectedExpiration.ToUnixTimeSeconds())),
            Times.Once);
        _controller.Response.Headers["Set-Cookie"].ToString().Should().Contain("ca_refresh_token=");
    }

    [Fact]
    public async Task LoginWithGoogle_Should_ReturnServiceUnavailable_WhenProviderDisabled()
    {
        var controller = CreateController(googleProvider: null);

        var result = await controller.LoginWithGoogle(new GoogleLoginModel
        {
            IdToken = "google-id-token",
        });

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetProfile_Should_ReturnOkWithCurrentUserProfile()
    {
        var user = CreateUser("profile@example.com");
        user.UserName = "profile.user";
        user.PhoneNumber = "0123456789";
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DisplayName = "Profile User",
            Timezone = "Asia/Bangkok",
            AvatarUrl = "avatars/profile.png",
        };
        _dbContext.UserProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        SetAuthenticatedUser(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });

        var result = await _controller.GetProfile();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<UserProfileModel>().Subject;
        payload.UserId.Should().Be(user.Id);
        payload.DisplayName.Should().Be("Profile User");
        payload.Timezone.Should().Be("Asia/Bangkok");
        payload.AvatarUrl.Should().Be("avatars/profile.png");
    }

    [Fact]
    public async Task UpdateProfile_Should_UpdateDisplayNameAndTimezone_AndReturnProfile()
    {
        var user = CreateUser("update.profile@example.com");
        user.UserName = "update.profile";
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DisplayName = "Old Name",
            Timezone = "UTC",
        };
        _dbContext.UserProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        SetAuthenticatedUser(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });

        var result = await _controller.UpdateProfile(new UpdateProfileModel
        {
            DisplayName = "Updated Name",
            Timezone = "Asia/Ho_Chi_Minh",
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<UserProfileModel>().Subject;
        payload.DisplayName.Should().Be("Updated Name");
        payload.Timezone.Should().Be("Asia/Ho_Chi_Minh");

        var updatedProfile = await _dbContext.UserProfiles.SingleAsync(x => x.UserId == user.Id);
        updatedProfile.DisplayName.Should().Be("Updated Name");
        updatedProfile.Timezone.Should().Be("Asia/Ho_Chi_Minh");
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_Should_ChangePassword_RevokeTokens_AndBlacklistCurrentToken()
    {
        var user = CreateUser("changepassword@example.com");
        SetAuthenticatedUser(
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("jti", "jwt-change-password"),
            new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds().ToString()));

        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, "Current123!")).ReturnsAsync(true);
        _userManagerMock.Setup(x => x.ChangePasswordAsync(user, "Current123!", "NewPass123!")).ReturnsAsync(IdentityResult.Success);

        var result = await _controller.ChangePassword(new ChangePasswordModel
        {
            CurrentPassword = "Current123!",
            NewPassword = "NewPass123!",
            ConfirmPassword = "NewPass123!",
        });

        result.Should().BeOfType<OkObjectResult>();
        _jwtTokenServiceMock.Verify(x => x.RevokeRefreshTokenAsync(user.Id), Times.Once);
        _tokenBlacklistServiceMock.Verify(x => x.BlacklistToken("jwt-change-password", It.IsAny<DateTimeOffset>()), Times.Once);
        _emailMessageServiceMock.Verify(
            x => x.CreateEmailMessageAsync(It.Is<EmailMessageDTO>(email =>
                email.Tos == user.Email &&
                email.Body == "password-changed-email")),
            Times.Once);
        _controller.Response.Headers["Set-Cookie"].ToString().Should().Contain("ca_refresh_token=");
    }

    [Fact]
    public async Task UploadAvatar_Should_StoreAvatar_DeleteOldFile_AndReturnOk()
    {
        var user = CreateUser("avatar@example.com");
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AvatarUrl = "avatars/old-user/old-avatar.png",
        };
        _dbContext.UserProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        SetAuthenticatedUser(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        var file = CreateFormFile("avatar.png", "image/png", CreateMinimalPngBytes());

        var result = await _controller.UploadAvatar(file);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<AvatarUploadResponseModel>().Subject;
        payload.AvatarUrl.Should().StartWith($"avatars/{user.Id}/");
        payload.AvatarUrl.Should().EndWith(".png");

        _fileStorageManagerMock.Verify(x => x.CreateAsync(It.IsAny<AvatarFileEntry>(), It.IsAny<Stream>()), Times.Once);
        _fileStorageManagerMock.Verify(
            x => x.DeleteAsync(It.Is<AvatarFileEntry>(entry =>
                entry.FileName == "old-avatar.png" &&
                entry.FileLocation == "avatars/old-user/old-avatar.png")),
            Times.Once);

        var updatedProfile = await _dbContext.UserProfiles.SingleAsync(x => x.UserId == user.Id);
        updatedProfile.AvatarUrl.Should().Be(payload.AvatarUrl);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private AuthController CreateController(GoogleIdentityProvider? googleProvider)
    {
        var optionsSnapshotMock = new Mock<IOptionsSnapshot<IdentityModuleOptions>>();
        optionsSnapshotMock
            .Setup(x => x.Value)
            .Returns(new IdentityModuleOptions
            {
                IdentityServer = new IdentityServerOptions
                {
                    FrontendUrl = "http://localhost:5174",
                    Authority = "https://localhost:44367",
                },
                Jwt = new JwtOptions
                {
                    RefreshTokenExpirationDays = 7,
                },
            });

        var controller = new AuthController(
            _userManagerMock.Object,
            _roleManagerMock.Object,
            _signInManagerMock.Object,
            _jwtTokenServiceMock.Object,
            _emailMessageServiceMock.Object,
            _emailTemplateServiceMock.Object,
            optionsSnapshotMock.Object,
            _dbContext,
            new Dispatcher(new Mock<IServiceProvider>().Object),
            _fileStorageManagerMock.Object,
            _tokenBlacklistServiceMock.Object,
            _scopeFactoryMock.Object,
            new Mock<ILogger<AuthController>>().Object,
            googleProvider);

        controller.ControllerContext = _controller.ControllerContext;
        return controller;
    }

    private void SetAuthenticatedUser(params Claim[] claims)
    {
        _controller.ControllerContext.HttpContext.User = CreatePrincipal(claims);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static byte[] CreateMinimalPngBytes()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
            0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
            0x42, 0x60, 0x82
        ];
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
