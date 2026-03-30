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

public class RoleStore : IRoleStore<Role>, IRoleClaimStore<Role>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IdentityDbContext _dbContext;

    public RoleStore(IRoleRepository roleRepository, IdentityDbContext dbContext)
    {
        _roleRepository = roleRepository;
        _dbContext = dbContext;
    }

    public void Dispose()
    {
    }

    public Task<IdentityResult> CreateAsync(Role role, CancellationToken cancellationToken)
    {
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }

        role.ConcurrencyStamp ??= Guid.NewGuid().ToString();
        return PersistRoleAsync(role, cancellationToken, isCreate: true);
    }

    public Task<IdentityResult> DeleteAsync(Role role, CancellationToken cancellationToken)
    {
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }

        _roleRepository.Delete(role);
        return SaveChangesAsync(cancellationToken);
    }

    public Task<Role> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(roleId, out var id))
        {
            return Task.FromResult<Role>(null);
        }

        return _roleRepository.Get(new RoleQueryOptions())
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Role> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        return _roleRepository.Get(new RoleQueryOptions())
            .FirstOrDefaultAsync(x => x.NormalizedName == normalizedRoleName, cancellationToken);
    }

    public Task<string> GetNormalizedRoleNameAsync(Role role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.NormalizedName);
    }

    public Task<string> GetRoleIdAsync(Role role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.Id.ToString());
    }

    public Task<string> GetRoleNameAsync(Role role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.Name);
    }

    public Task SetNormalizedRoleNameAsync(Role role, string normalizedName, CancellationToken cancellationToken)
    {
        role.NormalizedName = normalizedName;
        return Task.CompletedTask;
    }

    public Task SetRoleNameAsync(Role role, string roleName, CancellationToken cancellationToken)
    {
        role.Name = roleName;
        return Task.CompletedTask;
    }

    public Task<IdentityResult> UpdateAsync(Role role, CancellationToken cancellationToken)
    {
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }

        role.ConcurrencyStamp = Guid.NewGuid().ToString();
        return PersistRoleAsync(role, cancellationToken, isCreate: false);
    }

    private async Task<IdentityResult> PersistRoleAsync(Role role, CancellationToken cancellationToken, bool isCreate)
    {
        if (isCreate)
        {
            await _roleRepository.AddAsync(role, cancellationToken);
        }
        else
        {
            await _roleRepository.UpdateAsync(role, cancellationToken);
        }

        return await SaveChangesAsync(cancellationToken);
    }

    private async Task<IdentityResult> SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _roleRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    // IRoleClaimStore implementation
    public async Task<IList<Claim>> GetClaimsAsync(Role role, CancellationToken cancellationToken = default)
    {
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }

        var claims = await _dbContext.Set<RoleClaim>()
            .Where(rc => rc.RoleId == role.Id)
            .Select(rc => new Claim(rc.Type, rc.Value))
            .ToListAsync(cancellationToken);

        return claims;
    }

    public async Task AddClaimAsync(Role role, Claim claim, CancellationToken cancellationToken = default)
    {
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }

        if (claim == null)
        {
            throw new ArgumentNullException(nameof(claim));
        }

        var roleClaim = new RoleClaim
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            Type = claim.Type,
            Value = claim.Value
        };

        await _dbContext.Set<RoleClaim>().AddAsync(roleClaim, cancellationToken);
    }

    public async Task RemoveClaimAsync(Role role, Claim claim, CancellationToken cancellationToken = default)
    {
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }

        if (claim == null)
        {
            throw new ArgumentNullException(nameof(claim));
        }

        var roleClaims = await _dbContext.Set<RoleClaim>()
            .Where(rc => rc.RoleId == role.Id && rc.Type == claim.Type && rc.Value == claim.Value)
            .ToListAsync(cancellationToken);

        _dbContext.Set<RoleClaim>().RemoveRange(roleClaims);
    }
}
