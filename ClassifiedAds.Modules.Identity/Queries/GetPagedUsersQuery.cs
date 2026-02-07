using ClassifiedAds.Application;
using ClassifiedAds.Application.Common.DTOs;
using ClassifiedAds.Application.Decorators.AuditLog;
using ClassifiedAds.Application.Decorators.DatabaseRetry;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Models;
using ClassifiedAds.Modules.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.Queries.Roles;

public class GetPagedUsersQuery : IQuery<Paged<UserModel>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string Search { get; set; }
    public string Role { get; set; }
    public bool? EmailConfirmed { get; set; }
    public bool? IsLocked { get; set; }
}

[AuditLog]
[DatabaseRetry(retryTimes: 4)]
public class GetPagedUsersQueryHandler : IQueryHandler<GetPagedUsersQuery, Paged<UserModel>>
{
    private readonly IUserRepository _userRepository;
    private readonly UserManager<User> _userManager;
    private readonly IdentityDbContext _dbContext;

    public GetPagedUsersQueryHandler(
        IUserRepository userRepository,
        UserManager<User> userManager,
        IdentityDbContext dbContext)
    {
        _userRepository = userRepository;
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task<Paged<UserModel>> HandleAsync(GetPagedUsersQuery query, CancellationToken cancellationToken = default)
    {
        var usersQuery = _dbContext.Users.AsNoTracking();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchLower = query.Search.ToLower();
            usersQuery = usersQuery.Where(u =>
                u.Email.ToLower().Contains(searchLower) ||
                u.UserName.ToLower().Contains(searchLower) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(query.Search)));
        }

        // Apply email confirmed filter
        if (query.EmailConfirmed.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.EmailConfirmed == query.EmailConfirmed.Value);
        }

        // Apply locked filter
        if (query.IsLocked.HasValue)
        {
            var now = System.DateTimeOffset.UtcNow;
            if (query.IsLocked.Value)
            {
                usersQuery = usersQuery.Where(u => u.LockoutEnd != null && u.LockoutEnd > now);
            }
            else
            {
                usersQuery = usersQuery.Where(u => u.LockoutEnd == null || u.LockoutEnd <= now);
            }
        }

        // Apply role filter
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var role = await _dbContext.Roles
                .FirstOrDefaultAsync(r => r.Name == query.Role, cancellationToken);

            if (role != null)
            {
                var userIdsInRole = await _dbContext.Set<UserRole>()
                    .Where(ur => ur.RoleId == role.Id)
                    .Select(ur => ur.UserId)
                    .ToListAsync(cancellationToken);

                usersQuery = usersQuery.Where(u => userIdsInRole.Contains(u.Id));
            }
        }

        // Get total count
        var totalCount = await usersQuery.CountAsync(cancellationToken);

        // Apply pagination
        var users = await usersQuery
            .OrderBy(u => u.Email)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // Map to models
        var userModels = users.ToModels().ToList();

        return new Paged<UserModel>
        {
            Items = userModels,
            TotalItems = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
