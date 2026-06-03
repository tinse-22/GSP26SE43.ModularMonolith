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
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

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
    public async Task Login_Should_ReturnBadRequest_WhenEmailIsNull()
    {
        var model = new LoginModel
        {
            Email = null!,
            Password = "Password123!",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.Login(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_Should_ReturnBadRequest_WhenPasswordIsNull()
    {
        var model = new LoginModel
        {
            Email = "user@example.com",
            Password = null!,
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.Login(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_Should_ReturnBadRequest_WhenEmailIsWhitespaceOnly()
    {
        var model = new LoginModel
        {
            Email = "   ",
            Password = "Password123!",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.Login(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_Should_ReturnUnauthorized_WhenPasswordIsWhitespaceOnly()
    {
        var model = new LoginModel
        {
            Email = "user@example.com",
            Password = "   ",
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
            .ReturnsAsync(1);
        _signInManagerMock
            .Setup(x => x.CheckPasswordSignInAsync(user, model.Password, true))
            .ReturnsAsync(IdentitySignInResult.Failed);

        var result = await _controller.Login(model);

        var unauthorized = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetPropertyValue<string>(unauthorized.Value!, "Code").Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Login_Should_ReturnBadRequest_WhenEmailFormatHasNoDomain()
    {
        var model = new LoginModel
        {
            Email = "user@",
            Password = "Password123!",
        };
        ApplyDataAnnotationErrorsToModelState(model);

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
            .ReturnsAsync(IdentitySignInResult.Failed);

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
            .ReturnsAsync(IdentitySignInResult.Success);
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

    [Fact]
    public async Task Register_Should_ReturnBadRequest_WhenEmailFormatIsInvalid()
    {
        var model = new RegisterModel
        {
            Email = "invalid-email",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.Register(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_Should_ReturnBadRequest_WhenConfirmPasswordDoesNotMatch()
    {
        var model = new RegisterModel
        {
            Email = "register@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password999!",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.Register(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_Should_ReturnBadRequest_WhenEmailAlreadyFullyRegistered()
    {
        using var scopedDbContext = CreateRelationalRegisterDbContext();
        ConfigureRegisterScope(scopedDbContext);

        var model = new RegisterModel
        {
            Email = "registered@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
        };
        var existingUser = CreateUser(model.Email);

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync(existingUser);
        _userManagerMock
            .Setup(x => x.IsInRoleAsync(existingUser, "User"))
            .ReturnsAsync(true);

        var result = await _controller.Register(model);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        GetPropertyValue<string>(badRequest.Value!, "Error").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_Should_ReturnBadRequest_WhenEmailIsNull()
    {
        var model = new RegisterModel
        {
            Email = null!,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.Register(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_Should_ReturnBadRequest_WhenEmailIsWhitespaceOnly()
    {
        var model = new RegisterModel
        {
            Email = "   ",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.Register(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_Should_AcceptEmailAtMaxLength_WhenEmailLengthIs256()
    {
        var localPart = new string('a', 250);
        var email = $"{localPart}@x.com";
        email.Length.Should().Be(256);

        var model = new RegisterModel
        {
            Email = email,
            Password = "123456",
            ConfirmPassword = "123456",
        };

        var validationResults = ValidateModel(model);

        validationResults.Should().NotContain(x => x.MemberNames.Contains(nameof(RegisterModel.Email)));
    }

    [Fact]
    public async Task Register_Should_ReturnBadRequest_WhenEmailLengthExceeds256()
    {
        var localPart = new string('a', 251);
        var email = $"{localPart}@x.com";
        email.Length.Should().Be(257);

        var model = new RegisterModel
        {
            Email = email,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.Register(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_Should_ReturnBadRequest_WhenPasswordIsNull()
    {
        var model = new RegisterModel
        {
            Email = "register@example.com",
            Password = null!,
            ConfirmPassword = "Password123!",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.Register(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_Should_ReturnBadRequest_WhenPasswordLengthIs5()
    {
        var model = new RegisterModel
        {
            Email = "register@example.com",
            Password = "12345",
            ConfirmPassword = "12345",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.Register(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_Should_AcceptPasswordAtMinLength_WhenPasswordLengthIs6()
    {
        var model = new RegisterModel
        {
            Email = "register.min6@example.com",
            Password = "123456",
            ConfirmPassword = "123456",
        };

        var validationResults = ValidateModel(model);

        validationResults.Should().NotContain(x => x.MemberNames.Contains(nameof(RegisterModel.Password)));
    }

    [Fact]
    public async Task Register_Should_AcceptPasswordAtMaxLength_WhenPasswordLengthIs100()
    {
        var password = new string('a', 100);
        var model = new RegisterModel
        {
            Email = "register.max100@example.com",
            Password = password,
            ConfirmPassword = password,
        };

        var validationResults = ValidateModel(model);

        validationResults.Should().NotContain(x => x.MemberNames.Contains(nameof(RegisterModel.Password)));
    }

    [Fact]
    public async Task Register_Should_ReturnBadRequest_WhenPasswordLengthExceeds100()
    {
        var password = new string('a', 101);
        var model = new RegisterModel
        {
            Email = "register.too-long@example.com",
            Password = password,
            ConfirmPassword = password,
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.Register(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_Should_ReturnBadRequest_WhenConfirmPasswordIsNull()
    {
        var model = new RegisterModel
        {
            Email = "register@example.com",
            Password = "Password123!",
            ConfirmPassword = null!,
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.Register(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_Should_ReturnBadRequest_WhenConfirmPasswordIsWhitespaceMismatch()
    {
        var model = new RegisterModel
        {
            Email = "register@example.com",
            Password = "Password123!",
            ConfirmPassword = "   ",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.Register(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task LoginWithGoogle_Should_ReturnBadRequest_WhenIdTokenIsNull()
    {
        var model = new GoogleLoginModel
        {
            IdToken = null!,
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.LoginWithGoogle(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
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
    public async Task LoginWithGoogle_Should_ReturnBadRequest_WhenIdTokenIsMalformed()
    {
        var controller = CreateGoogleLoginController(_ => throw new InvalidJwtException("Malformed token"));

        var result = await controller.LoginWithGoogle(new GoogleLoginModel
        {
            IdToken = "malformed-token",
        });

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        GetPropertyValue<string>(badRequest.Value!, "Error").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LoginWithGoogle_Should_ReturnBadRequest_WhenIdTokenIsExpired()
    {
        var controller = CreateGoogleLoginController(_ => throw new InvalidJwtException("Expired token"));

        var result = await controller.LoginWithGoogle(new GoogleLoginModel
        {
            IdToken = "expired-token",
        });

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        GetPropertyValue<string>(badRequest.Value!, "Error").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LoginWithGoogle_Should_ReturnBadRequest_WhenEmailClaimIsMissing()
    {
        var controller = CreateGoogleLoginController(_ => Task.FromResult(new GoogleJsonWebSignature.Payload
        {
            Email = null,
            Name = "Google User",
        }));

        var result = await controller.LoginWithGoogle(new GoogleLoginModel
        {
            IdToken = "valid-without-email",
        });

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        GetPropertyValue<string>(badRequest.Value!, "Error").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ForgotPassword_Should_ReturnOk_WhenUserDoesNotExist()
    {
        var model = new ForgotPasswordModel { Email = "missing@example.com" };

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(model.Email))
            .ReturnsAsync((User)null!);

        var result = await _controller.ForgotPassword(model);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        GetPropertyValue<string>(ok.Value!, "Message").Should().NotBeNullOrWhiteSpace();
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

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        GetPropertyValue<string>(ok.Value!, "Message").Should().NotBeNullOrWhiteSpace();
        _emailMessageServiceMock.Verify(
            x => x.CreateEmailMessageAsync(It.Is<EmailMessageDTO>(email =>
                email.Tos == model.Email &&
                email.Body == "forgot-password-email")),
            Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_Should_ReturnSameSuccessMessage_ForExistingAndMissingUser()
    {
        var missingEmail = "missing@example.com";
        var existingEmail = "existing@example.com";
        var existingUser = CreateUser(existingEmail);

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(missingEmail))
            .ReturnsAsync((User)null!);
        _userManagerMock
            .Setup(x => x.FindByEmailAsync(existingEmail))
            .ReturnsAsync(existingUser);
        _userManagerMock
            .Setup(x => x.GeneratePasswordResetTokenAsync(existingUser))
            .ReturnsAsync("reset-token");

        var missingResult = await _controller.ForgotPassword(new ForgotPasswordModel { Email = missingEmail });
        var existingResult = await _controller.ForgotPassword(new ForgotPasswordModel { Email = existingEmail });

        var missingMessage = GetPropertyValue<string>(((OkObjectResult)missingResult).Value!, "Message");
        var existingMessage = GetPropertyValue<string>(((OkObjectResult)existingResult).Value!, "Message");
        existingMessage.Should().Be(missingMessage);
    }

    [Fact]
    public async Task ForgotPassword_Should_ReturnBadRequest_WhenEmailIsNull()
    {
        var model = new ForgotPasswordModel { Email = null! };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.ForgotPassword(model);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ForgotPassword_Should_ReturnBadRequest_WhenEmailIsWhitespaceOnly()
    {
        var model = new ForgotPasswordModel { Email = "   " };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.ForgotPassword(model);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ForgotPassword_Should_ReturnBadRequest_WhenEmailFormatIsInvalid()
    {
        var model = new ForgotPasswordModel { Email = "invalid-email" };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.ForgotPassword(model);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnUnauthorized_WhenUserIsUnauthenticated()
    {
        var result = await _controller.ChangePassword(new ChangePasswordModel
        {
            CurrentPassword = "Current123!",
            NewPassword = "NewPass123!",
            ConfirmPassword = "NewPass123!",
        });

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetPropertyValue<string>(unauthorized.Value!, "Error").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_WhenConfirmPasswordDoesNotMatch()
    {
        var model = new ChangePasswordModel
        {
            CurrentPassword = "Current123!",
            NewPassword = "NewPass123!",
            ConfirmPassword = "DifferentPass123!",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.ChangePassword(model);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_WhenCurrentPasswordIsNull()
    {
        var model = new ChangePasswordModel
        {
            CurrentPassword = null!,
            NewPassword = "NewPass123!",
            ConfirmPassword = "NewPass123!",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.ChangePassword(model);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_WhenNewPasswordIsNull()
    {
        var model = new ChangePasswordModel
        {
            CurrentPassword = "Current123!",
            NewPassword = null!,
            ConfirmPassword = "NewPass123!",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.ChangePassword(model);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_WhenConfirmPasswordIsNull()
    {
        var model = new ChangePasswordModel
        {
            CurrentPassword = "Current123!",
            NewPassword = "NewPass123!",
            ConfirmPassword = null!,
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.ChangePassword(model);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_WhenNewPasswordLengthIs5()
    {
        var model = new ChangePasswordModel
        {
            CurrentPassword = "Current123!",
            NewPassword = "12345",
            ConfirmPassword = "12345",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.ChangePassword(model);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_Should_AcceptNewPasswordAtMinLength_WhenNewPasswordLengthIs6()
    {
        var user = CreateUser("change.min6@example.com");
        SetAuthenticatedUser(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, "Current123!")).ReturnsAsync(false);

        var result = await _controller.ChangePassword(new ChangePasswordModel
        {
            CurrentPassword = "Current123!",
            NewPassword = "123456",
            ConfirmPassword = "123456",
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        GetPropertyValue<string>(badRequest.Value!, "Error").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_WhenCurrentPasswordIsWhitespaceOnly()
    {
        var user = CreateUser("change.whitespace@example.com");
        SetAuthenticatedUser(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, "   ")).ReturnsAsync(false);

        var result = await _controller.ChangePassword(new ChangePasswordModel
        {
            CurrentPassword = "   ",
            NewPassword = "NewPass123!",
            ConfirmPassword = "NewPass123!",
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        GetPropertyValue<string>(badRequest.Value!, "Error").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_WhenConfirmPasswordIsWhitespaceMismatch()
    {
        var model = new ChangePasswordModel
        {
            CurrentPassword = "Current123!",
            NewPassword = "NewPass123!",
            ConfirmPassword = "   ",
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.ChangePassword(model);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnUnauthorized_WhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        SetAuthenticatedUser(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString())).ReturnsAsync((User)null!);

        var result = await _controller.ChangePassword(new ChangePasswordModel
        {
            CurrentPassword = "Current123!",
            NewPassword = "NewPass123!",
            ConfirmPassword = "NewPass123!",
        });

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        GetPropertyValue<string>(unauthorized.Value!, "Error").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_WhenCurrentPasswordIsIncorrect()
    {
        var user = CreateUser("wrong.current@example.com");
        SetAuthenticatedUser(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, "Current123!")).ReturnsAsync(false);

        var result = await _controller.ChangePassword(new ChangePasswordModel
        {
            CurrentPassword = "Current123!",
            NewPassword = "NewPass123!",
            ConfirmPassword = "NewPass123!",
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        GetPropertyValue<string>(badRequest.Value!, "Error").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_WhenIdentityRejectsNewPassword()
    {
        var user = CreateUser("policy.fail@example.com");
        SetAuthenticatedUser(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, "Current123!")).ReturnsAsync(true);
        _userManagerMock
            .Setup(x => x.ChangePasswordAsync(user, "Current123!", "weak"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        var result = await _controller.ChangePassword(new ChangePasswordModel
        {
            CurrentPassword = "Current123!",
            NewPassword = "weak",
            ConfirmPassword = "weak",
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        _jwtTokenServiceMock.Verify(x => x.RevokeRefreshTokenAsync(It.IsAny<Guid>()), Times.Never);
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
    public async Task UpdateProfile_Should_ReturnUnauthorized_WhenUserIsUnauthenticated()
    {
        var result = await _controller.UpdateProfile(new UpdateProfileModel
        {
            DisplayName = "New Name",
        });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task UpdateProfile_Should_ReturnUnauthorized_WhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        SetAuthenticatedUser(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString())).ReturnsAsync((User)null!);

        var result = await _controller.UpdateProfile(new UpdateProfileModel
        {
            DisplayName = "New Name",
        });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
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
    }

    [Fact]
    public async Task UpdateProfile_Should_SetDisplayNameToNull_WhenWhitespaceProvided()
    {
        var user = CreateUser("whitespace.profile@example.com");
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DisplayName = "Existing Name",
            Timezone = "UTC",
        };
        _dbContext.UserProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        SetAuthenticatedUser(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });

        var result = await _controller.UpdateProfile(new UpdateProfileModel
        {
            DisplayName = "   ",
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<UserProfileModel>().Subject;
        payload.DisplayName.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfile_Should_AcceptDisplayNameAtMaxLength_WhenDisplayNameLengthIs200()
    {
        var user = CreateUser("display200@example.com");
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DisplayName = "Old Name",
        };
        _dbContext.UserProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        SetAuthenticatedUser(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });

        var displayName = new string('a', 200);
        var result = await _controller.UpdateProfile(new UpdateProfileModel
        {
            DisplayName = displayName,
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<UserProfileModel>().Subject;
        payload.DisplayName.Should().Be(displayName);
    }

    [Fact]
    public async Task UpdateProfile_Should_ReturnBadRequest_WhenDisplayNameLengthExceeds200()
    {
        var model = new UpdateProfileModel
        {
            DisplayName = new string('a', 201),
        };
        ApplyDataAnnotationErrorsToModelState(model);

        var result = await _controller.UpdateProfile(model);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateProfile_Should_CreateProfile_WhenProfileDoesNotExist()
    {
        var user = CreateUser("no.profile@example.com");

        SetAuthenticatedUser(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });

        var result = await _controller.UpdateProfile(new UpdateProfileModel
        {
            DisplayName = "Created Profile",
            Timezone = "Asia/Ho_Chi_Minh",
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<UserProfileModel>().Subject;
        payload.DisplayName.Should().Be("Created Profile");
        payload.Timezone.Should().Be("Asia/Ho_Chi_Minh");

        var createdProfile = await _dbContext.UserProfiles.SingleAsync(x => x.UserId == user.Id);
        createdProfile.DisplayName.Should().Be("Created Profile");
        createdProfile.Timezone.Should().Be("Asia/Ho_Chi_Minh");
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

    private AuthController CreateGoogleLoginController(Func<string, Task<GoogleJsonWebSignature.Payload>> verifier)
    {
        return new TestableGoogleAuthController(
            _userManagerMock.Object,
            _roleManagerMock.Object,
            _signInManagerMock.Object,
            _jwtTokenServiceMock.Object,
            _emailMessageServiceMock.Object,
            _emailTemplateServiceMock.Object,
            _dbContext,
            _fileStorageManagerMock.Object,
            _tokenBlacklistServiceMock.Object,
            _scopeFactoryMock.Object,
            verifier)
        {
            ControllerContext = _controller.ControllerContext,
        };
    }

    private void ConfigureRegisterScope(IdentityDbContext scopedDbContext)
    {
        _scopedServiceProviderMock
            .Setup(x => x.GetService(typeof(IdentityDbContext)))
            .Returns(scopedDbContext);
    }

    private void SetAuthenticatedUser(params Claim[] claims)
    {
        _controller.ControllerContext.HttpContext.User = CreatePrincipal(claims);
    }

    private void ApplyDataAnnotationErrorsToModelState(object model)
    {
        var validationResults = ValidateModel(model);

        foreach (var validationResult in validationResults)
        {
            var memberNames = validationResult.MemberNames.Any()
                ? validationResult.MemberNames
                : new[] { string.Empty };

            foreach (var memberName in memberNames)
            {
                _controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage ?? "Validation error");
            }
        }
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationContext = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);
        return validationResults;
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static IdentityDbContext CreateRelationalRegisterDbContext()
    {
        return new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql("Host=localhost;Database=fake_register_tests;Username=test;Password=test")
                .Options);
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

    private sealed class TestableGoogleAuthController : AuthController
    {
        private readonly Func<string, Task<GoogleJsonWebSignature.Payload>> _verifier;

        public TestableGoogleAuthController(
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            SignInManager<User> signInManager,
            IJwtTokenService jwtTokenService,
            IEmailMessageService emailMessageService,
            IEmailTemplateService emailTemplateService,
            IdentityDbContext dbContext,
            IFileStorageManager fileStorageManager,
            ITokenBlacklistService tokenBlacklistService,
            IServiceScopeFactory scopeFactory,
            Func<string, Task<GoogleJsonWebSignature.Payload>> verifier)
            : base(
                userManager,
                roleManager,
                signInManager,
                jwtTokenService,
                emailMessageService,
                emailTemplateService,
                CreateIdentityOptionsSnapshot(),
                dbContext,
                new Dispatcher(new Mock<IServiceProvider>().Object),
                fileStorageManager,
                tokenBlacklistService,
                scopeFactory,
                new Mock<ILogger<AuthController>>().Object,
                new GoogleIdentityProvider(new GoogleOptions { ClientId = "test" }))
        {
            _verifier = verifier;
        }

        protected override Task<GoogleJsonWebSignature.Payload> VerifyGoogleIdTokenAsync(string idToken)
        {
            return _verifier(idToken);
        }
    }

    private static IOptionsSnapshot<IdentityModuleOptions> CreateIdentityOptionsSnapshot()
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
        return optionsSnapshotMock.Object;
    }
}
