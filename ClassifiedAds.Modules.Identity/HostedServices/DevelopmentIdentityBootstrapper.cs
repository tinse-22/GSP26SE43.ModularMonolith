using ClassifiedAds.Modules.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.HostedServices;

public class DevelopmentIdentityBootstrapper : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<DevelopmentIdentityBootstrapper> _logger;

    public DevelopmentIdentityBootstrapper(
        IServiceProvider serviceProvider,
        IHostEnvironment hostEnvironment,
        ILogger<DevelopmentIdentityBootstrapper> logger)
    {
        _serviceProvider = serviceProvider;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_hostEnvironment.IsDevelopment())
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        await EnsureRoleAsync(roleManager, "Admin", cancellationToken);
        await EnsureRoleAsync(roleManager, "User", cancellationToken);

        await EnsureUserAsync(
            userManager,
            "tinvtse@gmail.com",
            "Admin@123",
            new[] { "Admin" },
            cancellationToken);

        await EnsureUserAsync(
            userManager,
            "user@example.com",
            "User@123",
            new[] { "User" },
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureRoleAsync(RoleManager<Role> roleManager, string roleName, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role != null)
        {
            return;
        }

        var result = await roleManager.CreateAsync(new Role
        {
            Id = Guid.NewGuid(),
            Name = roleName,
            NormalizedName = roleName.ToUpperInvariant(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        });

        if (!result.Succeeded)
        {
            _logger.LogWarning("Cannot create role {RoleName}: {Errors}", roleName, string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task EnsureUserAsync(
        UserManager<User> userManager,
        string email,
        string expectedPassword,
        string[] roles,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new User
            {
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                LockoutEnabled = true,
            };

            var createResult = await userManager.CreateAsync(user, expectedPassword);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning("Cannot create user {Email}: {Errors}", RedactEmail(email), string.Join("; ", createResult.Errors.Select(e => e.Description)));
                return;
            }
        }

        var changed = false;
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            changed = true;
        }

        if (!user.LockoutEnabled)
        {
            user.LockoutEnabled = true;
            changed = true;
        }

        if (user.AccessFailedCount > 0)
        {
            user.AccessFailedCount = 0;
            changed = true;
        }

        if (user.LockoutEnd.HasValue)
        {
            user.LockoutEnd = null;
            changed = true;
        }

        if (changed)
        {
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.CheckPasswordAsync(user, expectedPassword))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await userManager.ResetPasswordAsync(user, resetToken, expectedPassword);
            if (!resetResult.Succeeded)
            {
                _logger.LogWarning("Cannot reset password for {Email}: {Errors}", RedactEmail(email), string.Join("; ", resetResult.Errors.Select(e => e.Description)));
            }
        }

        foreach (var role in roles)
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }

    private static string RedactEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return email;
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return "***";
        }

        return string.Concat(email.AsSpan(0, 1), "***", email.AsSpan(atIndex));
    }
}
