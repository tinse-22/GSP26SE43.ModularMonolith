using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Contracts.Notification.DTOs;
using ClassifiedAds.Contracts.Notification.Services;
using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.Entities;
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
    private readonly IdentityModuleOptions _moduleOptions;
    private readonly IdentityDbContext _dbContext;
    private readonly Dispatcher _dispatcher;

    public AuthController(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        SignInManager<User> signInManager,
        IJwtTokenService jwtTokenService,
        IEmailMessageService emailMessageService,
        IOptionsSnapshot<IdentityModuleOptions> moduleOptions,
        IdentityDbContext dbContext,
        Dispatcher dispatcher)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _emailMessageService = emailMessageService;
        _moduleOptions = moduleOptions.Value;
        _dbContext = dbContext;
        _dispatcher = dispatcher;
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
            return BadRequest(new { Error = "Email is already registered." });
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

        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Welcome! Please confirm your email address",
            Body = $@"
                <h2>Welcome to ClassifiedAds!</h2>
                <p>Thank you for registering. Please confirm your email address by clicking the link below:</p>
                <p><a href='{confirmationUrl}'>Confirm Email Address</a></p>
                <p>This link will expire in 2 days.</p>
                <p>If you did not create an account, please ignore this email.</p>
            ",
        });

        return Created($"/api/auth/me", new RegisterResponseModel
        {
            UserId = user.Id,
            Email = user.Email,
            Message = "Registration successful. Please check your email to confirm your account.",
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
            return Unauthorized(new { Error = "Invalid email or password." });
        }

        // Check if email is confirmed
        if (!user.EmailConfirmed)
        {
            return BadRequest(new { Error = "Please confirm your email address before logging in." });
        }

        // Check if user is locked out
        if (await _userManager.IsLockedOutAsync(user))
        {
            return BadRequest(new { Error = "Account is locked. Please try again later." });
        }

        // Verify password
        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                return BadRequest(new { Error = "Account is locked due to multiple failed login attempts. Please try again later." });
            }

            return Unauthorized(new { Error = "Invalid email or password." });
        }

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);

        // Generate tokens
        var (accessToken, refreshToken, expiresIn) = await _jwtTokenService.GenerateTokensAsync(user, roles);

        return Ok(new LoginResponseModel
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = new UserInfoModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                TwoFactorEnabled = user.TwoFactorEnabled,
                Roles = roles.ToList(),
            },
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
            return Unauthorized(new { Error = "Invalid or expired refresh token." });
        }

        var (accessToken, refreshToken, expiresIn, principal) = result.Value;

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { Error = "Invalid refresh token." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { Error = "User not found." });
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new LoginResponseModel
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken, // New refresh token (old one is now invalid)
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = new UserInfoModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                TwoFactorEnabled = user.TwoFactorEnabled,
                Roles = roles.ToList(),
            },
        });
    }

    /// <summary>
    /// Logout - revoke refresh token
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

        return Ok(new { Message = "Logged out successfully." });
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
            return Unauthorized(new { Error = "User not authenticated." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { Error = "User not found." });
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new UserInfoModel
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            Roles = roles.ToList(),
        });
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
            return Ok(new { Message = "If an account with that email exists, a password reset link has been sent." });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = $"{_moduleOptions.IdentityServer?.Authority ?? "https://localhost:44367"}/Account/ResetPassword?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(user.Email)}";

        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Reset Your Password",
            Body = $@"
                <h2>Password Reset Request</h2>
                <p>You have requested to reset your password. Click the link below to reset it:</p>
                <p><a href='{resetUrl}'>Reset Password</a></p>
                <p>This link will expire in 3 hours.</p>
                <p>If you did not request a password reset, please ignore this email.</p>
            ",
        });

        return Ok(new { Message = "If an account with that email exists, a password reset link has been sent." });
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
            return BadRequest(new { Error = "Invalid request." });
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
        }

        // Send confirmation email
        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Password Changed Successfully",
            Body = $@"
                <h2>Password Changed</h2>
                <p>Your password has been changed successfully.</p>
                <p>If you did not make this change, please contact support immediately.</p>
            ",
        });

        return Ok(new { Message = "Password has been reset successfully." });
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
            return Unauthorized(new { Error = "User not authenticated." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { Error = "User not found." });
        }

        // Verify current password
        var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, model.CurrentPassword);
        if (!isCurrentPasswordValid)
        {
            return BadRequest(new { Error = "Current password is incorrect." });
        }

        // Change password
        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
        }

        // Revoke existing refresh tokens for security
        await _jwtTokenService.RevokeRefreshTokenAsync(user.Id);

        // Send confirmation email
        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Password Changed Successfully",
            Body = $@"
                <h2>Password Changed</h2>
                <p>Your password has been changed successfully.</p>
                <p>If you did not make this change, please contact support immediately.</p>
            ",
        });

        return Ok(new { Message = "Password has been changed successfully. Please login again with your new password." });
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
            return BadRequest(new { Error = "Invalid request." });
        }

        if (user.EmailConfirmed)
        {
            return Ok(new { Message = "Email is already confirmed." });
        }

        var result = await _userManager.ConfirmEmailAsync(user, model.Token);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(new { Message = "Email confirmed successfully. You can now login." });
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
            return Ok(new { Message = "If an account with that email exists and is not confirmed, a confirmation link has been sent." });
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationUrl = $"{_moduleOptions.IdentityServer?.Authority ?? "https://localhost:44367"}/Account/ConfirmEmailAddress?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(user.Email)}";

        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Confirm your email address",
            Body = $@"
                <h2>Email Confirmation</h2>
                <p>Please confirm your email address by clicking the link below:</p>
                <p><a href='{confirmationUrl}'>Confirm Email Address</a></p>
                <p>If you did not create an account, please ignore this email.</p>
            ",
        });

        return Ok(new { Message = "If an account with that email exists and is not confirmed, a confirmation link has been sent." });
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

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized(new { Error = "User not authenticated." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { Error = "User not found." });
        }

        var profile = await _dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userGuid);

        // Create profile if not exists
        if (profile == null)
        {
            profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userGuid,
                DisplayName = user.UserName,
            };
            _dbContext.UserProfiles.Add(profile);
            await _dbContext.SaveChangesAsync();
        }

        return Ok(new UserProfileModel
        {
            UserId = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            DisplayName = profile.DisplayName,
            AvatarUrl = profile.AvatarUrl,
            Timezone = profile.Timezone,
            PhoneNumber = user.PhoneNumber,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
        });
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

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized(new { Error = "User not authenticated." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { Error = "User not found." });
        }

        var profile = await _dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userGuid);

        // Create profile if not exists
        if (profile == null)
        {
            profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userGuid,
            };
            _dbContext.UserProfiles.Add(profile);
        }

        // Update profile fields
        if (!string.IsNullOrWhiteSpace(model.DisplayName))
        {
            profile.DisplayName = model.DisplayName;
        }

        if (model.Timezone != null)
        {
            profile.Timezone = model.Timezone;
        }

        // Update phone number if provided
        if (model.PhoneNumber != null && model.PhoneNumber != user.PhoneNumber)
        {
            user.PhoneNumber = model.PhoneNumber;
            user.PhoneNumberConfirmed = false;
            await _userManager.UpdateAsync(user);
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new UserProfileModel
        {
            UserId = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            DisplayName = profile.DisplayName,
            AvatarUrl = profile.AvatarUrl,
            Timezone = profile.Timezone,
            PhoneNumber = user.PhoneNumber,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
        });
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
            return BadRequest(new { Error = "No file uploaded." });
        }

        // Hard limit file size (max 2MB) - checked server-side before any processing
        const int maxFileSize = 2 * 1024 * 1024;
        if (file.Length > maxFileSize)
        {
            return BadRequest(new { Error = "File size exceeds 2MB limit." });
        }

        // SECURITY: Validate magic bytes FIRST - this is the server-side source of truth.
        // Do NOT trust file.ContentType or file.FileName as they are user-controlled.
        var detectedType = await DetectImageTypeFromMagicBytesAsync(file);
        if (detectedType == null)
        {
            return BadRequest(new { Error = "File content does not match a valid image format. Allowed: JPEG, PNG, GIF, WebP." });
        }

        // Use server-detected extension (not user-provided filename)
        var extension = detectedType.Value.Extension;

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized(new { Error = "User not authenticated." });
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

        // TODO: In production, integrate with ClassifiedAds.Modules.Storage
        // Example: await _fileStorageService.UploadAsync(fileLocation, file.OpenReadStream());
        // For now, store locally in wwwroot/uploads
        var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars", userGuid.ToString());
        Directory.CreateDirectory(uploadsPath);

        var filePath = Path.Combine(uploadsPath, safeFileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var avatarUrl = $"/uploads/{fileLocation}";

        // Delete old avatar file if exists
        if (!string.IsNullOrEmpty(profile.AvatarUrl))
        {
            var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", profile.AvatarUrl.TrimStart('/'));
            if (System.IO.File.Exists(oldFilePath))
            {
                try
                {
                    System.IO.File.Delete(oldFilePath);
                }
                catch
                {
                    // Log but don't fail if old file can't be deleted
                }
            }
        }

        profile.AvatarUrl = avatarUrl;
        await _dbContext.SaveChangesAsync();

        return Ok(new AvatarUploadResponseModel
        {
            AvatarUrl = avatarUrl,
            Message = "Avatar uploaded successfully.",
        });
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
}
