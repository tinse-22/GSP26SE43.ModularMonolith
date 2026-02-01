using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Notification.DTOs;
using ClassifiedAds.Contracts.Notification.Services;
using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Modules.Identity.Authorization;
using ClassifiedAds.Modules.Identity.Commands.Users;
using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Models;
using ClassifiedAds.Modules.Identity.Queries.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace ClassifiedAds.Modules.Identity.Controllers;

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
    private readonly IdentityModuleOptions _moduleOptions;

    public UsersController(Dispatcher dispatcher,
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        ILogger<UsersController> logger,
        IDateTimeProvider dateTimeProvider,
        IEmailMessageService emailMessageService,
        IOptionsSnapshot<IdentityModuleOptions> moduleOptions)
    {
        _dispatcher = dispatcher;
        _userManager = userManager;
        _roleManager = roleManager;
        _dateTimeProvider = dateTimeProvider;
        _emailMessageService = emailMessageService;
        _moduleOptions = moduleOptions.Value;
    }

    [Authorize(Permissions.GetUsers)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> Get()
    {
        var users = await _dispatcher.DispatchAsync(new GetUsersQuery());
        var model = users.ToModels();
        return Ok(model);
    }

    [Authorize(Permissions.GetUser)]
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<User>> Get(Guid id)
    {
        var user = await _dispatcher.DispatchAsync(new GetUserQuery { Id = id, AsNoTracking = true });
        var model = user.ToModel();
        return Ok(model);
    }

    [Authorize(Permissions.AddUser)]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserModel>> Post([FromBody] CreateUserModel model)
    {
        // Validate role exists
        var roleName = string.IsNullOrWhiteSpace(model.RoleName) ? "User" : model.RoleName;
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role == null)
        {
            return BadRequest(new { Error = $"Role '{roleName}' does not exist." });
        }

        // Check if email already exists
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            return BadRequest(new { Error = "Email is already registered." });
        }

        // Admin role: EmailConfirmed = true (no email verification)
        // User role: EmailConfirmed = false (requires email verification)
        var isAdmin = roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);

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

        // Assign role to user
        var roleResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!roleResult.Succeeded)
        {
            return BadRequest(new { Errors = roleResult.Errors.Select(e => e.Description) });
        }

        // If User role, send confirmation email
        if (!isAdmin)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationUrl = $"{_moduleOptions.IdentityServer.Authority}/Account/ConfirmEmailAddress?token={HttpUtility.UrlEncode(token)}&email={user.Email}";

            await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
            {
                From = "noreply@classifiedads.com",
                Tos = user.Email,
                Subject = "Confirm your email address",
                Body = $@"
                    <h2>Welcome to ClassifiedAds!</h2>
                    <p>Please confirm your email address by clicking the link below:</p>
                    <p><a href='{confirmationUrl}'>Confirm Email Address</a></p>
                    <p>If you did not create an account, please ignore this email.</p>
                ",
            });
        }

        var userModel = user.ToModel();
        return Created($"/api/users/{userModel.Id}", new
        {
            User = userModel,
            Role = roleName,
            EmailConfirmationRequired = !isAdmin,
            Message = isAdmin
                ? "Admin user created successfully."
                : "User created successfully. Please check email to confirm your account."
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

    [Authorize(Permissions.SendResetPasswordEmail)]
    [HttpPost("{id}/passwordresetemail")]
    public async Task<ActionResult> SendResetPasswordEmail(Guid id)
    {
        User user = await _dispatcher.DispatchAsync(new GetUserQuery { Id = id });

        if (user != null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetUrl = $"{_moduleOptions.IdentityServer.Authority}/Account/ResetPassword?token={HttpUtility.UrlEncode(token)}&email={user.Email}";

            await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
            {
                From = "phong@gmail.com",
                Tos = user.Email,
                Subject = "Forgot Password",
                Body = string.Format("Reset Url: {0}", resetUrl),
            });
        }
        else
        {
            // email user and inform them that they do not have an account
        }

        return Ok();
    }

    [Authorize(Permissions.SendConfirmationEmailAddressEmail)]
    [HttpPost("{id}/emailaddressconfirmation")]
    public async Task<ActionResult> SendConfirmationEmailAddressEmail(Guid id)
    {
        User user = await _dispatcher.DispatchAsync(new GetUserQuery { Id = id });

        if (user != null)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var confirmationEmail = $"{_moduleOptions.IdentityServer.Authority}/Account/ConfirmEmailAddress?token={HttpUtility.UrlEncode(token)}&email={user.Email}";

            await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
            {
                From = "phong@gmail.com",
                Tos = user.Email,
                Subject = "Confirmation Email",
                Body = string.Format("Confirmation Email: {0}", confirmationEmail),
            });
        }
        else
        {
            // email user and inform them that they do not have an account
        }

        return Ok();
    }
}