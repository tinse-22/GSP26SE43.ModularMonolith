using ClassifiedAds.Application;
using ClassifiedAds.Application.Common.DTOs;
using ClassifiedAds.Contracts.Notification.DTOs;
using ClassifiedAds.Contracts.Notification.Services;
using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Modules.Identity.Authorization;
using ClassifiedAds.Modules.Identity.Commands.Users;
using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Models;
using ClassifiedAds.Modules.Identity.Queries.Roles;
using ClassifiedAds.Modules.Identity.RateLimiterPolicies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace ClassifiedAds.Modules.Identity.Controllers;

[EnableRateLimiting(RateLimiterPolicyNames.DefaultPolicy)]
[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEmailMessageService _emailMessageService;
    private readonly IEmailTemplateService _emailTemplates;
    private readonly IdentityModuleOptions _moduleOptions;

    public UsersController(Dispatcher dispatcher,
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        ILogger<UsersController> logger,
        IDateTimeProvider dateTimeProvider,
        IEmailMessageService emailMessageService,
        IEmailTemplateService emailTemplates,
        IOptionsSnapshot<IdentityModuleOptions> moduleOptions)
    {
        _dispatcher = dispatcher;
        _userManager = userManager;
        _roleManager = roleManager;
        _dateTimeProvider = dateTimeProvider;
        _emailMessageService = emailMessageService;
        _emailTemplates = emailTemplates;
        _moduleOptions = moduleOptions.Value;
    }

    /// <summary>
    /// Get users with optional pagination, search and filter.
    /// If page/pageSize not specified, returns all users (legacy behavior).
    /// </summary>
    /// <param name="page">Page number (1-based). If specified, returns paginated results.</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100)</param>
    /// <param name="search">Search by email or username</param>
    /// <param name="role">Filter by role name</param>
    /// <param name="emailConfirmed">Filter by email confirmation status</param>
    /// <param name="isLocked">Filter by account lock status</param>
    [Authorize(Permissions.GetUsers)]
    [HttpGet]
    [ProducesResponseType(typeof(Paged<UserModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Paged<UserModel>>> Get(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] bool? emailConfirmed = null,
        [FromQuery] bool? isLocked = null)
    {
        // If no pagination params, use defaults that return all (legacy behavior)
        var actualPage = page ?? 1;
        var actualPageSize = pageSize ?? (page.HasValue ? 20 : int.MaxValue);

        var result = await _dispatcher.DispatchAsync(new GetPagedUsersQuery
        {
            Page = actualPage,
            PageSize = Math.Min(actualPageSize, page.HasValue ? 100 : int.MaxValue),
            Search = search,
            Role = role,
            EmailConfirmed = emailConfirmed,
            IsLocked = isLocked,
        });

        return Ok(result);
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [Authorize(Permissions.GetUser)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserModel>> Get(Guid id)
    {
        var user = await _dispatcher.DispatchAsync(new GetUserQuery { Id = id, AsNoTracking = true });
        var model = user.ToModel();
        return Ok(model);
    }

    /// <summary>
    /// Create a new user (Admin only)
    /// </summary>
    [Authorize(Permissions.AddUser)]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserModel>> Post([FromBody] CreateUserModel model)
    {
        var roleNames = (model.Roles ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roleNames.Count == 0)
        {
            roleNames.Add("User");
        }

        foreach (var roleName in roleNames)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                return BadRequest(new { Error = $"Quyền '{roleName}' không tồn tại." });
            }
        }

        // Check if email already exists
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            return BadRequest(new { Error = "Email đã được đăng ký." });
        }

        // Admin role: EmailConfirmed = true (no email verification)
        // User role: EmailConfirmed = false (requires email verification)
        var isAdmin = roleNames.Any(x => x.Equals("Admin", StringComparison.OrdinalIgnoreCase));

        var user = new User
        {
            UserName = model.UserName ?? model.Email,
            NormalizedUserName = (model.UserName ?? model.Email).ToUpper(),
            Email = model.Email,
            NormalizedEmail = model.Email.ToUpper(),
            EmailConfirmed = isAdmin,
            PhoneNumber = model.PhoneNumber,
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

        // Assign roles to user
        var roleResult = await _userManager.AddToRolesAsync(user, roleNames);
        if (!roleResult.Succeeded)
        {
            return BadRequest(new { Errors = roleResult.Errors.Select(e => e.Description) });
        }

        // If User role, send confirmation email
        if (!isAdmin)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationUrl = $"{_moduleOptions.IdentityServer.Authority}/Account/ConfirmEmailAddress?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(user.Email)}";

            var displayName = user.UserName ?? user.Email.Split('@')[0];
            await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
            {
                From = "noreply@classifiedads.com",
                Tos = user.Email,
                Subject = "Chào mừng bạn! Vui lòng xác nhận email",
                Body = _emailTemplates.WelcomeConfirmEmail(displayName, confirmationUrl),
            });
        }

        var userModel = user.ToModel();
        return Created($"/api/users/{userModel.Id}", new
        {
            User = userModel,
            Roles = roleNames,
            EmailConfirmationRequired = !isAdmin,
            Message = isAdmin
                ? "Tạo quản trị viên thành công."
                : "Tạo người dùng thành công. Vui lòng kiểm tra email để xác nhận tài khoản."
        });
    }

    [Authorize(Permissions.UpdateUser)]
    [HttpPut("{id}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Put(Guid id, [FromBody] UserModel model)
    {
        User user = await _dispatcher.DispatchAsync(new GetUserQuery { Id = id });

        user.UserName = model.UserName;
        user.NormalizedUserName = model.UserName.ToUpper();
        user.Email = model.Email;
        user.NormalizedEmail = model.Email.ToUpper();
        user.EmailConfirmed = model.EmailConfirmed;
        user.PhoneNumber = model.PhoneNumber;
        user.PhoneNumberConfirmed = model.PhoneNumberConfirmed;
        user.TwoFactorEnabled = model.TwoFactorEnabled;
        user.LockoutEnabled = model.LockoutEnabled;
        user.LockoutEnd = model.LockoutEnd;
        user.AccessFailedCount = model.AccessFailedCount;

        _ = await _userManager.UpdateAsync(user);

        model = user.ToModel();
        return Ok(model);
    }

    [Authorize(Permissions.SetPassword)]
    [HttpPut("{id}/password")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetPassword(Guid id, [FromBody] SetPasswordModel model)
    {
        User user = await _dispatcher.DispatchAsync(new GetUserQuery { Id = id });

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var rs = await _userManager.ResetPasswordAsync(user, token, model.Password);

        if (rs.Succeeded)
        {
            // Notify user that their password was changed by admin
            var displayNameSet = user.UserName ?? user.Email.Split('@')[0];
            await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
            {
                From = "noreply@classifiedads.com",
                Tos = user.Email,
                Subject = "Mật khẩu đã được cập nhật bởi quản trị viên",
                Body = _emailTemplates.AdminSetPassword(displayNameSet),
            });

            return Ok();
        }

        return BadRequest(rs.Errors);
    }

    [Authorize(Permissions.DeleteUser)]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id)
    {
        var user = await _dispatcher.DispatchAsync(new GetUserQuery { Id = id });
        await _dispatcher.DispatchAsync(new DeleteUserCommand { User = user });

        return Ok();
    }

    /// <summary>
    /// Send password reset email to user (Admin action)
    /// </summary>
    [Authorize(Permissions.SendResetPasswordEmail)]
    [HttpPost("{id}/password-reset-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SendPasswordResetEmail(Guid id)
    {
        User user = await _dispatcher.DispatchAsync(new GetUserQuery { Id = id });

        if (user == null)
        {
            return NotFound(new { Error = "Không tìm thấy người dùng." });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = $"{_moduleOptions.IdentityServer?.Authority ?? "https://localhost:44367"}/Account/ResetPassword?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(user.Email)}";

        var displayNameReset = user.UserName ?? user.Email.Split('@')[0];
        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Yêu cầu đặt lại mật khẩu",
            Body = _emailTemplates.AdminResetPassword(displayNameReset, resetUrl),
        });

        return Ok(new { Message = "Email đặt lại mật khẩu đã được gửi thành công." });
    }

    /// <summary>
    /// Send email confirmation to user (Admin action)
    /// </summary>
    [Authorize(Permissions.SendConfirmationEmailAddressEmail)]
    [HttpPost("{id}/email-confirmation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SendEmailConfirmation(Guid id)
    {
        User user = await _dispatcher.DispatchAsync(new GetUserQuery { Id = id });

        if (user == null)
        {
            return NotFound(new { Error = "Không tìm thấy người dùng." });
        }

        if (user.EmailConfirmed)
        {
            return Ok(new { Message = "Email đã được xác nhận." });
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationUrl = $"{_moduleOptions.IdentityServer?.Authority ?? "https://localhost:44367"}/Account/ConfirmEmailAddress?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(user.Email)}";

        var displayNameConfirm = user.UserName ?? user.Email.Split('@')[0];
        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Xác nhận địa chỉ email",
            Body = _emailTemplates.AdminConfirmEmail(displayNameConfirm, confirmationUrl),
        });

        return Ok(new { Message = "Email xác nhận đã được gửi thành công." });
    }

    /// <summary>
    /// Get roles assigned to a user
    /// </summary>
    [Authorize(Permissions.GetUser)]
    [HttpGet("{id}/roles")]
    [ProducesResponseType(typeof(IEnumerable<RoleModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<RoleModel>>> GetUserRoles(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return NotFound(new { Error = "Không tìm thấy người dùng." });
        }

        var roleNames = await _userManager.GetRolesAsync(user);
        var roles = new List<RoleModel>();

        foreach (var roleName in roleNames)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                roles.Add(role.ToModel());
            }
        }

        return Ok(roles);
    }

    /// <summary>
    /// Assign a role to a user
    /// </summary>
    [Authorize(Permissions.UpdateUser)]
    [HttpPost("{id}/roles")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AssignRole(Guid id, [FromBody] AssignRoleModel model)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return NotFound(new { Error = "Không tìm thấy người dùng." });
        }

        var role = await _roleManager.FindByIdAsync(model.RoleId.ToString());
        if (role == null)
        {
            return BadRequest(new { Error = "Quyền không tồn tại." });
        }

        // Check if user already has this role
        if (await _userManager.IsInRoleAsync(user, role.Name))
        {
            return BadRequest(new { Error = $"Người dùng đã có quyền '{role.Name}'." });
        }

        var result = await _userManager.AddToRoleAsync(user, role.Name);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(new { Message = $"Gán quyền '{role.Name}' thành công." });
    }

    /// <summary>
    /// Remove a role from a user
    /// </summary>
    [Authorize(Permissions.UpdateUser)]
    [HttpDelete("{id}/roles/{roleId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveRole(Guid id, Guid roleId)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return NotFound(new { Error = "Không tìm thấy người dùng." });
        }

        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        if (role == null)
        {
            return BadRequest(new { Error = "Quyền không tồn tại." });
        }

        // Check if user has this role
        if (!await _userManager.IsInRoleAsync(user, role.Name))
        {
            return BadRequest(new { Error = $"Người dùng không có quyền '{role.Name}'." });
        }

        var result = await _userManager.RemoveFromRoleAsync(user, role.Name);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(new { Message = $"Thoát quyền '{role.Name}' thành công." });
    }

    /// <summary>
    /// Lock/Ban a user account
    /// </summary>
    [Authorize(Permissions.UpdateUser)]
    [HttpPost("{id}/lock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> LockUser(Guid id, [FromBody] LockUserModel model)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return NotFound(new { Error = "Không tìm thấy người dùng." });
        }

        // Set lockout end date
        var lockoutEnd = model.Permanent
            ? DateTimeOffset.MaxValue
            : DateTimeOffset.UtcNow.AddDays(model.Days ?? 30);

        var result = await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
        }

        // Notify user about account lock
        var displayNameLock = user.UserName ?? user.Email.Split('@')[0];
        var lockoutEndDisplay = model.Permanent
            ? "Vĩnh viễn (cho đến khi được mở khóa)"
            : lockoutEnd.ToString("dd/MM/yyyy HH:mm:ss") + " (UTC)";

        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Tài khoản của bạn đã bị khóa",
            Body = _emailTemplates.AccountLocked(displayNameLock, lockoutEndDisplay),
        });

        return Ok(new
        {
            Message = model.Permanent
                ? "Người dùng đã bị khóa vĩnh viễn."
                : $"Người dùng đã bị khóa đến {lockoutEnd:yyyy-MM-dd HH:mm:ss} UTC.",
            LockoutEnd = lockoutEnd
        });
    }

    /// <summary>
    /// Unlock a user account
    /// </summary>
    [Authorize(Permissions.UpdateUser)]
    [HttpPost("{id}/unlock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UnlockUser(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return NotFound(new { Error = "Không tìm thấy người dùng." });
        }

        var result = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
        }

        // Reset access failed count
        await _userManager.ResetAccessFailedCountAsync(user);

        // Notify user about account unlock
        var displayNameUnlock = user.UserName ?? user.Email.Split('@')[0];
        await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
        {
            From = "noreply@classifiedads.com",
            Tos = user.Email,
            Subject = "Tài khoản của bạn đã được mở khóa",
            Body = _emailTemplates.AccountUnlocked(displayNameUnlock),
        });

        return Ok(new { Message = "Người dùng đã được mở khóa." });
    }
}
