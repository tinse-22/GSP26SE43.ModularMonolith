using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity;

public class RoleStore : IRoleStore<Role>
{
    private readonly IRoleRepository _roleRepository;

    public RoleStore(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
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
        return PersistRoleAsync(role, cancellationToken);
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

        role.ConcurrencyStamp ??= Guid.NewGuid().ToString();
        return PersistRoleAsync(role, cancellationToken);
    }

    private async Task<IdentityResult> PersistRoleAsync(Role role, CancellationToken cancellationToken)
    {
        await _roleRepository.AddOrUpdateAsync(role, cancellationToken);
        return await SaveChangesAsync(cancellationToken);
    }

    private async Task<IdentityResult> SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _roleRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }
}
