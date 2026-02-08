using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.Identity.Models;

public class UserModel
{
    public Guid Id { get; set; }

    public string UserName { get; set; }

    public string NormalizedUserName { get; set; }

    public string Email { get; set; }

    public string NormalizedEmail { get; set; }

    public bool EmailConfirmed { get; set; }

    public string PhoneNumber { get; set; }

    public bool PhoneNumberConfirmed { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public string ConcurrencyStamp { get; set; }

    public string SecurityStamp { get; set; }

    public bool LockoutEnabled { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public int AccessFailedCount { get; set; }

    public string Password { get; set; }
}

public class CreateUserModel
{
    public string UserName { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; }

    [Phone(ErrorMessage = "Invalid phone number format")]
    public string PhoneNumber { get; set; }

    public List<string> Roles { get; set; } = new () { "User" };
}

/// <summary>
/// Model for assigning a role to a user.
/// </summary>
public class AssignRoleModel
{
    [Required(ErrorMessage = "Role ID is required")]
    public Guid RoleId { get; set; }
}

/// <summary>
/// Model for locking/banning a user.
/// </summary>
public class LockUserModel
{
    /// <summary>
    /// Number of days to lock the user. Ignored if Permanent is true.
    /// </summary>
    public int? Days { get; set; } = 30;

    /// <summary>
    /// If true, user is permanently locked.
    /// </summary>
    public bool Permanent { get; set; } = false;

    /// <summary>
    /// Optional reason for locking the user.
    /// </summary>
    [StringLength(500)]
    public string Reason { get; set; }
}
