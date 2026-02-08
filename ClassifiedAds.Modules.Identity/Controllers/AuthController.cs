using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Contracts.Notification.DTOs;
using ClassifiedAds.Contracts.Notification.Services;
using ClassifiedAds.Infrastructure.Storages;
using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Helpers;
using ClassifiedAds.Modules.Identity.Models;
using ClassifiedAds.Modules.Identity.Persistence;
using ClassifiedAds.Modules.Identity.Queries.Roles;
using ClassifiedAds.Modules.Identity.RateLimiterPolicies;
using ClassifiedAds.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;

// Alias for brevity
using ITemplates = ClassifiedAds.Contracts.Notification.Services.IEmailTemplateService;

namespace ClassifiedAds.Modules.Identity.Controllers;

[EnableRateLimiting(RateLimiterPolicyNames.DefaultPolicy)]
[Produces("application/json")]
[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailMessageService _emailMessageService;
    private readonly ITemplates _emailTemplates;
    private readonly IdentityModuleOptions _moduleOptions;
    private readonly IdentityDbContext _dbContext;
    private readonly Dispatcher _dispatcher;
    private readonly IFileStorageManager _fileStorageManager;
    private readonly ITokenBlacklistService _tokenBlacklistService;

    public AuthController(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        SignInManager<User> signInManager,
        IJwtTokenService jwtTokenService,
        IEmailMessageService emailMessageService,
        IEmailTemplateService emailTemplates,
        IOptionsSnapshot<IdentityModuleOptions> moduleOptions,
        IdentityDbContext dbContext,
        Dispatcher dispatcher,
        IFileStorageManager fileStorageManager,
        ITokenBlacklistService tokenBlacklistService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _emailMessageService = emailMessageService;
        _emailTemplates = emailTemplates;
        _moduleOptions = moduleOptions.Value;
        _dbContext = dbContext;
        _dispatcher = dispatcher;
        _fileStorageManager = fileStorageManager;
        _tokenBlacklistService = tokenBlacklistService;
    }

    /// <summary>
    /// Register a new user account (self-registration)
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimiterPolicyNames.AuthPolicy)]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponseModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RegisterResponseModel>> Register([FromBody] RegisterModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Check if email already exists
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            return BadRequest(new { Error = "Email đã được đăng ký." });
        }

        // Create new user with minimal required fields
        var user = new User
        {
            UserName = model.Email,
            NormalizedUserName = model.Email.ToUpper(),
            Email = model.Email,
            NormalizedEmail = model.Email.ToUpper(),
            EmailConfirmed = false,
            PhoneNumberConfirmed = false,
            TwoFactorEnabled = false,
            LockoutEnabled = true,
            AccessFailedCount = 0,
        };

        // Create user with password
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
        }

        // Assign default "User" role
        var userRole = await _roleManager.FindByNameAsync("User");
        if (userRole != null)
        {
            await _userManager.AddToRoleAsync(user, "User");
        }

        // Create user profile
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DisplayName = model.Email.Split('@')[0], // Default display name from email
        };
        _dbContext.UserProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        // Send confirmation email
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationUrl = $"{_moduleOptions.IdentityServer?.Authority ?? "https://localhost:44367"}/Account/ConfirmEmailAddress?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(user.Email)}";

        var displayName = profile.DisplayName ?? user.Email.Split('@')[0];
        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Chào mừng bạn! Vui lòng xác nhận email",
            Body = _emailTemplates.WelcomeConfirmEmail(displayName, confirmationUrl),
        });

        return Created($"/api/auth/me", new RegisterResponseModel
        {
            UserId = user.Id,
            Email = user.Email,
            Message = "Đăng ký thành công. Vui lòng kiểm tra email để xác nhận tài khoản.",
            EmailConfirmationRequired = true,
        });
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimiterPolicyNames.AuthPolicy)]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponseModel>> Login([FromBody] LoginModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return Unauthorized(new { Error = "Email hoặc mật khẩu không đúng." });
        }

        // Check if email is confirmed
        if (!user.EmailConfirmed)
        {
            return BadRequest(new { Error = "Vui lòng xác nhận địa chỉ email trước khi đăng nhập." });
        }

        // Check if user is locked out
        if (await _userManager.IsLockedOutAsync(user))
        {
            return BadRequest(new { Error = "Tài khoản đã bị khóa. Vui lòng thử lại sau." });
        }

        // Verify password
        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                return BadRequest(new { Error = "Tài khoản bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau." });
            }

            return Unauthorized(new { Error = "Email hoặc mật khẩu không đúng." });
        }

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);
        var profile = await GetOrCreateUserProfileAsync(user);

        // Generate tokens
        var (accessToken, refreshToken, expiresIn) = await _jwtTokenService.GenerateTokensAsync(user, roles);

        return Ok(new LoginResponseModel
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = ToUserInfoModel(user, profile, roles),
        });
    }

    /// <summary>
    /// Refresh access token using refresh token.
    /// Implements token rotation: old refresh token is invalidated and a new one is issued.
    /// </summary>
    /// <remarks>
    /// Token rotation is a security best practice that:
    /// - Limits the window of opportunity if a refresh token is compromised
    /// - Enables detection of token theft (if old token is reused)
    /// - Forces attackers to constantly steal new tokens
    /// </remarks>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimiterPolicyNames.AuthPolicy)]
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(LoginResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponseModel>> RefreshToken([FromBody] RefreshTokenModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Use token rotation: validate and get new tokens in one atomic operation
        var result = await _jwtTokenService.ValidateAndRotateRefreshTokenAsync(model.RefreshToken);
        if (result == null)
        {
            return Unauthorized(new { Error = "Mã xác thực không hợp lệ hoặc đã hết hạn." });
        }

        var (accessToken, refreshToken, expiresIn, principal) = result.Value;

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { Error = "Mã xác thực không hợp lệ." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { Error = "Không tìm thấy người dùng." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var profile = await GetOrCreateUserProfileAsync(user);

        return Ok(new LoginResponseModel
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken, // New refresh token (old one is now invalid)
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = ToUserInfoModel(user, profile, roles),
        });
    }

    /// <summary>
    /// Logout - revoke refresh token and blacklist current access token
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Logout()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
        {
            await _jwtTokenService.RevokeRefreshTokenAsync(userGuid);
        }

        // Blacklist the current access token so it cannot be used anymore
        BlacklistCurrentAccessToken();

        return Ok(new { Message = "Đăng xuất thành công." });
    }

    /// <summary>
    /// Get current user information
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserInfoModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserInfoModel>> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { Error = "Người dùng chưa được xác thực." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { Error = "Không tìm thấy người dùng." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var profile = await GetOrCreateUserProfileAsync(user);

        return Ok(ToUserInfoModel(user, profile, roles));
    }

    /// <summary>
    /// Request password reset email
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimiterPolicyNames.PasswordPolicy)]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);

        // Always return success to prevent email enumeration
        if (user == null)
        {
            return Ok(new { Message = "Nếu tài khoản tồn tại, email đặt lại mật khẩu đã được gửi." });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = $"{_moduleOptions.IdentityServer?.Authority ?? "https://localhost:44367"}/Account/ResetPassword?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(user.Email)}";

        var displayName = user.UserName ?? user.Email.Split('@')[0];
        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Đặt lại mật khẩu",
            Body = _emailTemplates.ForgotPassword(displayName, resetUrl),
        });

        return Ok(new { Message = "Nếu tài khoản tồn tại, email đặt lại mật khẩu đã được gửi." });
    }

    /// <summary>
    /// Reset password using token from email
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimiterPolicyNames.PasswordPolicy)]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return BadRequest(new { Error = "Yêu cầu không hợp lệ." });
        }

        var normalizedToken = IdentityTokenNormalizer.Normalize(model.Token);
        var result = await _userManager.ResetPasswordAsync(user, normalizedToken, model.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
        }

        // Send confirmation email
        var displayNameReset = user.UserName ?? user.Email.Split('@')[0];
        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Mật khẩu đã được thay đổi",
            Body = _emailTemplates.PasswordChanged(displayNameReset),
        });

        return Ok(new { Message = "Đặt lại mật khẩu thành công." });
    }

    /// <summary>
    /// Change password for authenticated user
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { Error = "Người dùng chưa được xác thực." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { Error = "Không tìm thấy người dùng." });
        }

        // Verify current password
        var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, model.CurrentPassword);
        if (!isCurrentPasswordValid)
        {
            return BadRequest(new { Error = "Mật khẩu hiện tại không đúng." });
        }

        // Change password
        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
        }

        // Revoke existing refresh tokens for security
        await _jwtTokenService.RevokeRefreshTokenAsync(user.Id);

        // Blacklist the current access token so it cannot be used anymore
        BlacklistCurrentAccessToken();

        // Send confirmation email
        var displayNameChange = user.UserName ?? user.Email.Split('@')[0];
        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Mật khẩu đã được thay đổi",
            Body = _emailTemplates.PasswordChanged(displayNameChange),
        });

        return Ok(new { Message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại." });
    }

    /// <summary>
    /// Confirm email address
    /// </summary>
    [AllowAnonymous]
    [HttpPost("confirm-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ConfirmEmail([FromBody] ConfirmEmailModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return BadRequest(new { Error = "Yêu cầu không hợp lệ." });
        }

        if (user.EmailConfirmed)
        {
            return Ok(new { Message = "Email đã được xác nhận." });
        }

        var normalizedToken = IdentityTokenNormalizer.Normalize(model.Token);
        var result = await _userManager.ConfirmEmailAsync(user, normalizedToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(new { Message = "Xác nhận email thành công. Bạn có thể đăng nhập ngay bây giờ." });
    }

    /// <summary>
    /// Resend email confirmation
    /// </summary>
    [AllowAnonymous]
    [HttpPost("resend-confirmation-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ResendConfirmationEmail([FromBody] ForgotPasswordModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);

        // Always return success to prevent email enumeration
        if (user == null || user.EmailConfirmed)
        {
            return Ok(new { Message = "Nếu tài khoản tồn tại và chưa được xác nhận, email xác nhận đã được gửi." });
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationUrl = $"{_moduleOptions.IdentityServer?.Authority ?? "https://localhost:44367"}/Account/ConfirmEmailAddress?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(user.Email)}";

        var displayNameResend = user.UserName ?? user.Email.Split('@')[0];
        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Xác nhận địa chỉ email",
            Body = _emailTemplates.ResendConfirmEmail(displayNameResend, confirmationUrl),
        });

        return Ok(new { Message = "Nếu tài khoản tồn tại và chưa được xác nhận, email xác nhận đã được gửi." });
    }

    /// <summary>
    /// Get current user's profile
    /// </summary>
    [Authorize]
    [HttpGet("me/profile")]
    [ProducesResponseType(typeof(UserProfileModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileModel>> GetProfile()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out _))
        {
            return Unauthorized(new { Error = "Người dùng chưa được xác thực." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { Error = "Không tìm thấy người dùng." });
        }

        var profile = await GetOrCreateUserProfileAsync(user);
        var roles = await _userManager.GetRolesAsync(user);

        return Ok(ToUserProfileModel(user, profile, roles));
    }

    /// <summary>
    /// Update current user's profile
    /// </summary>
    [Authorize]
    [HttpPut("me/profile")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(UserProfileModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileModel>> UpdateProfile([FromBody] UpdateProfileModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out _))
        {
            return Unauthorized(new { Error = "Người dùng chưa được xác thực." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { Error = "Không tìm thấy người dùng." });
        }

        var profile = await GetOrCreateUserProfileAsync(user, saveChanges: false);
        var userChanged = false;
        var emailChanged = false;

        if (model.UserName != null)
        {
            var newUserName = model.UserName.Trim();
            if (string.IsNullOrWhiteSpace(newUserName))
            {
                return BadRequest(new { Error = "Tên đăng nhập không được để trống." });
            }

            if (!string.Equals(newUserName, user.UserName, StringComparison.Ordinal))
            {
                var existingUser = await _userManager.FindByNameAsync(newUserName);
                if (existingUser != null && existingUser.Id != user.Id)
                {
                    return BadRequest(new { Error = "Tên đăng nhập đã được sử dụng." });
                }

                user.UserName = newUserName;
                user.NormalizedUserName = _userManager.NormalizeName(newUserName) ?? newUserName.ToUpperInvariant();
                userChanged = true;
            }
        }

        if (model.Email != null)
        {
            var newEmail = model.Email.Trim();
            if (string.IsNullOrWhiteSpace(newEmail))
            {
                return BadRequest(new { Error = "Email không được để trống." });
            }

            if (!string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existingUser = await _userManager.FindByEmailAsync(newEmail);
                if (existingUser != null && existingUser.Id != user.Id)
                {
                    return BadRequest(new { Error = "Email đã được đăng ký." });
                }

                user.Email = newEmail;
                user.NormalizedEmail = _userManager.NormalizeEmail(newEmail) ?? newEmail.ToUpperInvariant();
                user.EmailConfirmed = false;
                userChanged = true;
                emailChanged = true;
            }
        }

        if (model.DisplayName != null)
        {
            profile.DisplayName = string.IsNullOrWhiteSpace(model.DisplayName)
                ? null
                : model.DisplayName.Trim();
        }

        if (model.Timezone != null)
        {
            profile.Timezone = string.IsNullOrWhiteSpace(model.Timezone)
                ? null
                : model.Timezone.Trim();
        }

        if (model.PhoneNumber != null)
        {
            var newPhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber)
                ? null
                : model.PhoneNumber.Trim();

            if (!string.Equals(newPhoneNumber, user.PhoneNumber, StringComparison.Ordinal))
            {
                user.PhoneNumber = newPhoneNumber;
                user.PhoneNumberConfirmed = false;
                userChanged = true;
            }
        }

        if (userChanged)
        {
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return BadRequest(new { Errors = updateResult.Errors.Select(e => e.Description) });
            }
        }

        await _dbContext.SaveChangesAsync();

        if (emailChanged)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationUrl = $"{_moduleOptions.IdentityServer?.Authority ?? "https://localhost:44367"}/Account/ConfirmEmailAddress?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(user.Email)}";
            var displayName = profile.DisplayName ?? user.UserName ?? user.Email.Split('@')[0];

            await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
            {
                From = "noreply@classifiedads.com",
                Tos = user.Email,
                Subject = "Xác nhận địa chỉ email mới",
                Body = _emailTemplates.ResendConfirmEmail(displayName, confirmationUrl),
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(ToUserProfileModel(user, profile, roles));
    }

    /// <summary>
    /// Upload user avatar
    /// </summary>
    /// <remarks>
    /// Allowed file types: JPEG, PNG, GIF, WebP (validated by magic bytes)
    /// Maximum file size: 2MB
    /// Files are sanitized and stored with unique filenames
    /// </remarks>
    [Authorize]
    [HttpPost("me/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024)] // 2MB hard limit
    [ProducesResponseType(typeof(AvatarUploadResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<AvatarUploadResponseModel>> UploadAvatar(IFormFile file)
    {
        if (file is not { Length: > 0 })
        {
            return BadRequest(new { Error = "Chưa chọn tệp tin." });
        }

        // Hard limit file size (max 2MB) - checked server-side before any processing
        const int maxFileSize = 2 * 1024 * 1024;
        if (file.Length > maxFileSize)
        {
            return BadRequest(new { Error = "Kích thước tệp tin vượt quá giới hạn 2MB." });
        }

        // SECURITY: Validate magic bytes FIRST - this is the server-side source of truth.
        // Do NOT trust file.ContentType or file.FileName as they are user-controlled.
        var detectedType = await DetectImageTypeFromMagicBytesAsync(file);
        if (detectedType == null)
        {
            return BadRequest(new { Error = "Định dạng tệp tin không hợp lệ. Chỉ chấp nhận: JPEG, PNG, GIF, WebP." });
        }

        // Use server-detected extension (not user-provided filename)
        var extension = detectedType.Value.Extension;

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized(new { Error = "Người dùng chưa được xác thực." });
        }

        var profile = await _dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userGuid);

        if (profile == null)
        {
            profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userGuid,
            };
            _dbContext.UserProfiles.Add(profile);
        }

        // Generate sanitized unique filename using server-detected extension (no user input)
        var safeFileName = $"{Guid.NewGuid()}{extension}";
        var fileLocation = $"avatars/{userGuid}/{safeFileName}";

        // Use IFileStorageManager to store file (supports Local, Azure, Amazon, Firebase)
        var avatarFileEntry = new AvatarFileEntry
        {
            Id = Guid.NewGuid(),
            FileName = safeFileName,
            FileLocation = fileLocation,
        };

        using (var stream = file.OpenReadStream())
        {
            await _fileStorageManager.CreateAsync(avatarFileEntry, stream);
        }

        // Delete old avatar file if exists
        if (!string.IsNullOrEmpty(profile.AvatarUrl))
        {
            try
            {
                var oldFileEntry = new AvatarFileEntry
                {
                    Id = Guid.Empty,
                    FileName = Path.GetFileName(profile.AvatarUrl),
                    FileLocation = profile.AvatarUrl.TrimStart('/'),
                };
                await _fileStorageManager.DeleteAsync(oldFileEntry);
            }
            catch
            {
                // Log but don't fail if old file can't be deleted
            }
        }

        profile.AvatarUrl = fileLocation;
        await _dbContext.SaveChangesAsync();

        return Ok(new AvatarUploadResponseModel
        {
            AvatarUrl = fileLocation,
            Message = "Avatar uploaded successfully.",
        });
    }

    private async Task<UserProfile> GetOrCreateUserProfileAsync(User user, bool saveChanges = true)
    {
        var profile = await _dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == user.Id);

        if (profile != null)
        {
            return profile;
        }

        var defaultDisplayName = user.UserName;
        if (string.IsNullOrWhiteSpace(defaultDisplayName) && !string.IsNullOrWhiteSpace(user.Email))
        {
            defaultDisplayName = user.Email.Split('@')[0];
        }

        profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DisplayName = defaultDisplayName,
        };

        _dbContext.UserProfiles.Add(profile);

        if (saveChanges)
        {
            await _dbContext.SaveChangesAsync();
        }

        return profile;
    }

    private static UserInfoModel ToUserInfoModel(User user, UserProfile profile, IEnumerable<string> roles)
    {
        var roleList = roles?.ToList() ?? new List<string>();
        var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

        return new UserInfoModel
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            LockoutEnabled = user.LockoutEnabled,
            LockoutEnd = user.LockoutEnd,
            IsLockedOut = isLockedOut,
            AccessFailedCount = user.AccessFailedCount,
            DisplayName = profile?.DisplayName,
            AvatarUrl = profile?.AvatarUrl,
            Timezone = profile?.Timezone,
            CreatedDateTime = user.CreatedDateTime,
            UpdatedDateTime = user.UpdatedDateTime,
            ProfileCreatedDateTime = profile?.CreatedDateTime,
            ProfileUpdatedDateTime = profile?.UpdatedDateTime,
            Roles = roleList,
        };
    }

    private static UserProfileModel ToUserProfileModel(User user, UserProfile profile, IEnumerable<string> roles)
    {
        var roleList = roles?.ToList() ?? new List<string>();
        var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

        return new UserProfileModel
        {
            UserId = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            DisplayName = profile?.DisplayName,
            AvatarUrl = profile?.AvatarUrl,
            Timezone = profile?.Timezone,
            PhoneNumber = user.PhoneNumber,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            LockoutEnabled = user.LockoutEnabled,
            LockoutEnd = user.LockoutEnd,
            IsLockedOut = isLockedOut,
            AccessFailedCount = user.AccessFailedCount,
            CreatedDateTime = user.CreatedDateTime,
            UpdatedDateTime = user.UpdatedDateTime,
            ProfileCreatedDateTime = profile?.CreatedDateTime,
            ProfileUpdatedDateTime = profile?.UpdatedDateTime,
            Roles = roleList,
        };
    }

    /// <summary>
    /// Detects image type by reading file magic bytes (server-side truth).
    /// Returns null if file is not a recognized image format.
    /// This eliminates reliance on user-controlled ContentType/FileName.
    /// </summary>
    private static async Task<(string MimeType, string Extension)?> DetectImageTypeFromMagicBytesAsync(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var headerBytes = new byte[12];
        var bytesRead = await stream.ReadAsync(headerBytes.AsMemory(0, 12));

        if (bytesRead < 4)
        {
            return null;
        }

        // JPEG: FF D8 FF
        if (bytesRead >= 3 && headerBytes[0] == 0xFF && headerBytes[1] == 0xD8 && headerBytes[2] == 0xFF)
        {
            return ("image/jpeg", ".jpg");
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytesRead >= 8
            && headerBytes[0] == 0x89 && headerBytes[1] == 0x50
            && headerBytes[2] == 0x4E && headerBytes[3] == 0x47
            && headerBytes[4] == 0x0D && headerBytes[5] == 0x0A
            && headerBytes[6] == 0x1A && headerBytes[7] == 0x0A)
        {
            return ("image/png", ".png");
        }

        // GIF: 47 49 46 38 (GIF87a or GIF89a)
        if (bytesRead >= 4
            && headerBytes[0] == 0x47 && headerBytes[1] == 0x49
            && headerBytes[2] == 0x46 && headerBytes[3] == 0x38)
        {
            return ("image/gif", ".gif");
        }

        // WebP: 52 49 46 46 xx xx xx xx 57 45 42 50 (RIFF....WEBP)
        if (bytesRead >= 12
            && headerBytes[0] == 0x52 && headerBytes[1] == 0x49
            && headerBytes[2] == 0x46 && headerBytes[3] == 0x46
            && headerBytes[8] == 0x57 && headerBytes[9] == 0x45
            && headerBytes[10] == 0x42 && headerBytes[11] == 0x50)
        {
            return ("image/webp", ".webp");
        }

        return null;
    }

    /// <summary>
    /// Blacklists the current request's access token so it cannot be reused.
    /// Extracts JTI and expiration from the current JWT claims.
    /// </summary>
    private void BlacklistCurrentAccessToken()
    {
        var jti = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrEmpty(jti))
        {
            return;
        }

        // Get token expiration from claims
        var expClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Exp)?.Value;
        DateTimeOffset expiresAt;
        if (!string.IsNullOrEmpty(expClaim) && long.TryParse(expClaim, out var expUnix))
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
        }
        else
        {
            // Fallback: blacklist for the max token lifetime (default 60 min)
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(60);
        }

        _tokenBlacklistService.BlacklistToken(jti, expiresAt);
    }

    /// <summary>
    /// Lightweight IFileEntry implementation for avatar file operations.
    /// </summary>
    private class AvatarFileEntry : IFileEntry
    {
        public Guid Id { get; set; }

        public string FileName { get; set; }

        public string FileLocation { get; set; }
    }
}
