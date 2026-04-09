using ClassifiedAds.Contracts.Identity.DTOs;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity;

public class UserStore : IUserStore<User>,
                         IUserPasswordStore<User>,
                         IUserSecurityStampStore<User>,
                         IUserEmailStore<User>,
                         IUserPhoneNumberStore<User>,
                         IUserTwoFactorStore<User>,
                         IUserLockoutStore<User>,
                         IUserAuthenticationTokenStore<User>,
                         IUserAuthenticatorKeyStore<User>,
                         IUserTwoFactorRecoveryCodeStore<User>,
                         IUserRoleStore<User>,
                         IUserClaimStore<User>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserRepository _userRepository;
    private readonly IdentityDbContext _dbContext;

    public UserStore(IUserRepository userRepository, IdentityDbContext dbContext)
    {
        _unitOfWork = userRepository.UnitOfWork;
        _userRepository = userRepository;
        _dbContext = dbContext;
    }

    public void Dispose()
    {
    }

    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureCollectionsInitialized(user);
        user.ConcurrencyStamp ??= Guid.NewGuid().ToString();
        user.SecurityStamp ??= Guid.NewGuid().ToString();

        await _userRepository.AddAsync(user, cancellationToken);
        await PersistChangesAsync();
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        _userRepository.Delete(user);
        await PersistChangesAsync();
        return IdentityResult.Success;
    }

    public Task<User> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return _userRepository.Get(new UserQueryOptions { IncludeTokens = true }).FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken: cancellationToken);
    }

    public Task<User> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        return _userRepository.Get(new UserQueryOptions { IncludeTokens = true }).FirstOrDefaultAsync(x => x.Id == Guid.Parse(userId), cancellationToken: cancellationToken);
    }

    public Task<User> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return _userRepository.Get(new UserQueryOptions { IncludeTokens = true }).FirstOrDefaultAsync(x => x.NormalizedUserName == normalizedUserName, cancellationToken: cancellationToken);
    }

    public Task<int> GetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task<string> GetEmailAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Email);
    }

    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.EmailConfirmed);
    }

    public Task<bool> GetLockoutEnabledAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.LockoutEnabled);
    }

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.LockoutEnd);
    }

    public Task<string> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.NormalizedEmail);
    }

    public Task<string> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.NormalizedUserName);
    }

    public Task<string> GetPasswordHashAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.PasswordHash);
    }

    public Task<string> GetPhoneNumberAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.PhoneNumber);
    }

    public Task<bool> GetPhoneNumberConfirmedAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.PhoneNumberConfirmed);
    }

    public Task<string> GetSecurityStampAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.SecurityStamp ?? string.Empty);
    }

    public Task<bool> GetTwoFactorEnabledAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.TwoFactorEnabled);
    }

    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Id.ToString());
    }

    public Task<string> GetUserNameAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.UserName);
    }

    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.PasswordHash != null);
    }

    public Task<int> IncrementAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount++;
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task ResetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task SetEmailAsync(User user, string email, CancellationToken cancellationToken)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public Task SetLockoutEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task SetLockoutEndDateAsync(User user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task SetNormalizedEmailAsync(User user, string normalizedEmail, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    public Task SetNormalizedUserNameAsync(User user, string normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public Task SetPasswordHashAsync(User user, string passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task SetPhoneNumberAsync(User user, string phoneNumber, CancellationToken cancellationToken)
    {
        user.PhoneNumber = phoneNumber;
        return Task.CompletedTask;
    }

    public Task SetPhoneNumberConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
    {
        user.PhoneNumberConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public Task SetSecurityStampAsync(User user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task SetTwoFactorEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        user.TwoFactorEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task SetUserNameAsync(User user, string userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureCollectionsInitialized(user);
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        await _userRepository.UpdateAsync(user, cancellationToken);
        await PersistChangesAsync();
        return IdentityResult.Success;
    }

    private const string AuthenticatorStoreLoginProvider = "[AuthenticatorStore]";
    private const string AuthenticatorKeyTokenName = "AuthenticatorKey";
    private const string RecoveryCodeTokenName = "RecoveryCodes";

    public Task<string> GetTokenAsync(User user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        var tokenEntity = user.Tokens.SingleOrDefault(
                l => l.TokenName == name && l.LoginProvider == loginProvider);
        return Task.FromResult(tokenEntity?.TokenValue);
    }

    public async Task SetTokenAsync(User user, string loginProvider, string name, string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureCollectionsInitialized(user);

        var tokenEntity = user.Tokens.SingleOrDefault(
                l => l.TokenName == name && l.LoginProvider == loginProvider);
        if (tokenEntity != null)
        {
            tokenEntity.TokenValue = value;
        }
        else
        {
            user.Tokens.Add(new UserToken
            {
                UserId = user.Id,
                LoginProvider = loginProvider,
                TokenName = name,
                TokenValue = value,
            });
        }
    }

    public async Task RemoveTokenAsync(User user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (user.Tokens == null || user.Tokens.Count == 0)
        {
            return;
        }

        var tokenEntity = user.Tokens.SingleOrDefault(
                l => l.TokenName == name && l.LoginProvider == loginProvider);
        if (tokenEntity != null)
        {
            user.Tokens.Remove(tokenEntity);
        }
    }

    public Task SetAuthenticatorKeyAsync(User user, string key, CancellationToken cancellationToken)
    {
        return SetTokenAsync(user, AuthenticatorStoreLoginProvider, AuthenticatorKeyTokenName, key, cancellationToken);
    }

    public Task<string> GetAuthenticatorKeyAsync(User user, CancellationToken cancellationToken)
    {
        return GetTokenAsync(user, AuthenticatorStoreLoginProvider, AuthenticatorKeyTokenName, cancellationToken);
    }

    public Task ReplaceCodesAsync(User user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
    {
        var mergedCodes = string.Join(";", recoveryCodes);
        return SetTokenAsync(user, AuthenticatorStoreLoginProvider, RecoveryCodeTokenName, mergedCodes, cancellationToken);
    }

    public async Task<bool> RedeemCodeAsync(User user, string code, CancellationToken cancellationToken)
    {
        var mergedCodes = await GetTokenAsync(user, AuthenticatorStoreLoginProvider, RecoveryCodeTokenName, cancellationToken) ?? "";
        var splitCodes = mergedCodes.Split(';');
        if (splitCodes.Contains(code))
        {
            var updatedCodes = new List<string>(splitCodes.Where(s => s != code));
            await ReplaceCodesAsync(user, updatedCodes, cancellationToken);
            return true;
        }

        return false;
    }

    public async Task<int> CountCodesAsync(User user, CancellationToken cancellationToken)
    {
        var mergedCodes = await GetTokenAsync(user, AuthenticatorStoreLoginProvider, RecoveryCodeTokenName, cancellationToken) ?? "";
        if (mergedCodes.Length > 0)
        {
            return mergedCodes.Split(';').Length;
        }

        return 0;
    }

    // IUserRoleStore<User> implementation
    public async Task AddToRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        var normalizedRoleName = roleName.ToUpperInvariant();
        var role = await _dbContext.Set<Role>().FirstOrDefaultAsync(r => r.NormalizedName == normalizedRoleName, cancellationToken);
        if (role == null)
        {
            throw new InvalidOperationException($"Role '{roleName}' not found.");
        }

        var existingUserRole = await _dbContext.Set<UserRole>()
            .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, cancellationToken);
        if (existingUserRole)
        {
            return;
        }

        var userRole = new UserRole { UserId = user.Id, RoleId = role.Id };
        await _dbContext.Set<UserRole>().AddAsync(userRole, cancellationToken);
        await PersistChangesAsync();
    }

    public async Task RemoveFromRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        var normalizedRoleName = roleName.ToUpperInvariant();
        var role = await _dbContext.Set<Role>().FirstOrDefaultAsync(r => r.NormalizedName == normalizedRoleName, cancellationToken);
        if (role != null)
        {
            var userRole = await _dbContext.Set<UserRole>().FirstOrDefaultAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, cancellationToken);
            if (userRole != null)
            {
                _dbContext.Set<UserRole>().Remove(userRole);
                await PersistChangesAsync();
            }
        }
    }

    public async Task<IList<string>> GetRolesAsync(User user, CancellationToken cancellationToken)
    {
        var roleIds = await _dbContext.Set<UserRole>()
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        var roles = await _dbContext.Set<Role>()
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles;
    }

    public async Task<bool> IsInRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        var normalizedRoleName = roleName.ToUpperInvariant();
        var role = await _dbContext.Set<Role>().FirstOrDefaultAsync(r => r.NormalizedName == normalizedRoleName, cancellationToken);
        if (role == null)
        {
            return false;
        }

        return await _dbContext.Set<UserRole>().AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, cancellationToken);
    }

    public async Task<IList<User>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var normalizedRoleName = roleName.ToUpperInvariant();
        var role = await _dbContext.Set<Role>().FirstOrDefaultAsync(r => r.NormalizedName == normalizedRoleName, cancellationToken);
        if (role == null)
        {
            return new List<User>();
        }

        var userIds = await _dbContext.Set<UserRole>()
            .Where(ur => ur.RoleId == role.Id)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        return await _dbContext.Set<User>()
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(cancellationToken);
    }

    private Task PersistChangesAsync()
    {
        // Identity write flows can re-enter the store (create -> add role -> update user).
        // Persist without propagating request cancellation so Npgsql keeps the write durable.
        return _unitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    private static void EnsureCollectionsInitialized(User user)
    {
        user.Tokens ??= new List<UserToken>();
        user.Claims ??= new List<UserClaim>();
        user.UserRoles ??= new List<UserRole>();
        user.UserLogins ??= new List<UserLogin>();
    }

    // IUserClaimStore<User> implementation
    public async Task<IList<Claim>> GetClaimsAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        var claims = await _dbContext.Set<UserClaim>()
            .Where(uc => uc.UserId == user.Id)
            .Select(uc => new Claim(uc.Type, uc.Value))
            .ToListAsync(cancellationToken);

        return claims;
    }

    public async Task AddClaimsAsync(User user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(claims);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var claim in claims)
        {
            var userClaim = new UserClaim
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Type = claim.Type,
                Value = claim.Value,
            };
            await _dbContext.Set<UserClaim>().AddAsync(userClaim, cancellationToken);
        }

        await PersistChangesAsync();
    }

    public async Task ReplaceClaimAsync(User user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(newClaim);
        cancellationToken.ThrowIfCancellationRequested();

        var matchedClaims = await _dbContext.Set<UserClaim>()
            .Where(uc => uc.UserId == user.Id && uc.Type == claim.Type && uc.Value == claim.Value)
            .ToListAsync(cancellationToken);

        foreach (var matchedClaim in matchedClaims)
        {
            matchedClaim.Type = newClaim.Type;
            matchedClaim.Value = newClaim.Value;
        }

        await PersistChangesAsync();
    }

    public async Task RemoveClaimsAsync(User user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(claims);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var claim in claims)
        {
            var matchedClaims = await _dbContext.Set<UserClaim>()
                .Where(uc => uc.UserId == user.Id && uc.Type == claim.Type && uc.Value == claim.Value)
                .ToListAsync(cancellationToken);

            _dbContext.Set<UserClaim>().RemoveRange(matchedClaims);
        }

        await PersistChangesAsync();
    }

    public async Task<IList<User>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        cancellationToken.ThrowIfCancellationRequested();

        var userIds = await _dbContext.Set<UserClaim>()
            .Where(uc => uc.Type == claim.Type && uc.Value == claim.Value)
            .Select(uc => uc.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await _dbContext.Set<User>()
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(cancellationToken);
    }
}
