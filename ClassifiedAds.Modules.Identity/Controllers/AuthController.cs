using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Contracts.Notification.DTOs;
using ClassifiedAds.Contracts.Notification.Services;
using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Models;
using ClassifiedAds.Modules.Identity.Queries.Roles;
using ClassifiedAds.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;

namespace ClassifiedAds.Modules.Identity.Controllers;

[Produces("application/json")]
[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailMessageService _emailMessageService;
    private readonly IdentityModuleOptions _moduleOptions;
    private readonly Dispatcher _dispatcher;

    public AuthController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IJwtTokenService jwtTokenService,
        IEmailMessageService emailMessageService,
        IOptionsSnapshot<IdentityModuleOptions> moduleOptions,
        Dispatcher dispatcher)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _emailMessageService = emailMessageService;
        _moduleOptions = moduleOptions.Value;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    /// Refresh access token using refresh token
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(LoginResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponseModel>> RefreshToken([FromBody] RefreshTokenModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var principal = await _jwtTokenService.ValidateRefreshTokenAsync(model.RefreshToken);
        if (principal == null)
        {
            return Unauthorized(new { Error = "Invalid or expired refresh token." });
        }

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
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
}
