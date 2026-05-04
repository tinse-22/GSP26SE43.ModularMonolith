using ClassifiedAds.Application.Common.DTOs;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Persistence;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClassifiedAds.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly IdentityDbContext _identityDbContext;
    private readonly SubscriptionDbContext _subscriptionDbContext;

    public AdminDashboardController(IdentityDbContext identityDbContext,
        SubscriptionDbContext subscriptionDbContext)
    {
        _identityDbContext = identityDbContext;
        _subscriptionDbContext = subscriptionDbContext;
    }

    #region DTOs

    public class AdminDashboardSummaryDto
    {
        public long TotalUsers { get; set; }
        public long NewUsersLast30Days { get; set; }
        public long ActiveCommunities { get; set; }
        public long TotalClubs { get; set; }
        public long TotalGames { get; set; }
        public long TotalEvents { get; set; }
        public long TotalRevenueCents { get; set; }
        public long RevenueThisMonthCents { get; set; }
        public long SuccessfulTransactions { get; set; }
        public long ActiveMemberships { get; set; }
        public long OpenBugReports { get; set; }
        public DateTimeOffset GeneratedAtUtc { get; set; }
    }

    public class AdminUserStatsDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string AvatarUrl { get; set; }
        public int? Level { get; set; }
        public long? Points { get; set; }
        public long? WalletBalanceCents { get; set; }
        public string CurrentMembership { get; set; }
        public DateTimeOffset? MembershipExpiresAt { get; set; }
        public int? EventsCreated { get; set; }
        public int? EventsAttended { get; set; }
        public int? CommunitiesJoined { get; set; }
        public long? TotalSpentCents { get; set; }
        public List<string> Roles { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset? UpdatedAtUtc { get; set; }
        public bool? IsDeleted { get; set; }
    }

    public class AdminTransactionDto
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public long? AmountCents { get; set; }
        public string Currency { get; set; }
        public string Direction { get; set; }
        public string Method { get; set; }
        public string Status { get; set; }
        public Guid? EventId { get; set; }
        public string EventTitle { get; set; }
        public string Provider { get; set; }
        public string ProviderRef { get; set; }
        public string Metadata { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
    }

    public class AdminPaymentIntentDto
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public long? AmountCents { get; set; }
        public string Purpose { get; set; }
        public Guid? EventId { get; set; }
        public string EventTitle { get; set; }
        public Guid? MembershipPlanId { get; set; }
        public string MembershipPlanName { get; set; }
        public string Status { get; set; }
        public long? OrderCode { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset? UpdatedAtUtc { get; set; }
    }

    public class AdminMembershipStatsDto
    {
        public Guid PlanId { get; set; }
        public string PlanName { get; set; }
        public long? PriceCents { get; set; }
        public int? DurationMonths { get; set; }
        public int? MonthlyEventLimit { get; set; }
        public bool? IsActive { get; set; }
        public long? ActiveSubscribers { get; set; }
        public long? TotalRevenueCents { get; set; }
        public long? PurchasesThisMonth { get; set; }
    }

    public class AdminRoleStatsDto
    {
        public Guid RoleId { get; set; }
        public string RoleName { get; set; }
        public long? UsersCount { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
    }

    #endregion

    [HttpGet("summary")]
    public async Task<ActionResult<AdminDashboardSummaryDto>> GetSummary()
    {
        var totalUsers = await _identityDbContext.Users.LongCountAsync();
        var newUsersLast30Days = await _identityDbContext.Users.LongCountAsync(u => u.CreatedDateTime >= DateTimeOffset.UtcNow.AddDays(-30));
        var activeMemberships = await _subscriptionDbContext.UserSubscriptions.LongCountAsync(s => s.Status == SubscriptionStatus.Active);
        var successfulTransactions = await _subscriptionDbContext.PaymentTransactions.LongCountAsync(t => t.Status == PaymentStatus.Succeeded);

        var totalRevenue = await _subscriptionDbContext.PaymentTransactions
            .Where(t => t.Status == PaymentStatus.Succeeded)
            .Select(t => (decimal?)t.Amount)
            .SumAsync() ?? 0m;

        var startOfMonth = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var revenueThisMonth = await _subscriptionDbContext.PaymentTransactions
            .Where(t => t.Status == PaymentStatus.Succeeded && t.CreatedDateTime >= startOfMonth)
            .Select(t => (decimal?)t.Amount)
            .SumAsync() ?? 0m;

        var dto = new AdminDashboardSummaryDto
        {
            TotalUsers = totalUsers,
            NewUsersLast30Days = newUsersLast30Days,
            ActiveMemberships = activeMemberships,
            SuccessfulTransactions = successfulTransactions,
            TotalRevenueCents = (long)(totalRevenue * 100m),
            RevenueThisMonthCents = (long)(revenueThisMonth * 100m),
            GeneratedAtUtc = DateTimeOffset.UtcNow,
        };

        return Ok(dto);
    }

    [HttpGet("users")]
    public async Task<ActionResult<Paged<AdminUserStatsDto>>> GetUsers(
        [FromQuery] string Keyword = null,
        [FromQuery] string Role = null,
        [FromQuery] string MembershipPlan = null,
        [FromQuery] bool? HasActiveMembership = null,
        [FromQuery] bool? IncludeDeleted = null,
        [FromQuery] DateTimeOffset? CreatedFrom = null,
        [FromQuery] DateTimeOffset? CreatedTo = null,
        [FromQuery] long? MinBalanceCents = null,
        [FromQuery] long? MaxBalanceCents = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var usersQuery = _identityDbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            var k = Keyword.ToLower();
            usersQuery = usersQuery.Where(u => u.Email.ToLower().Contains(k) || u.UserName.ToLower().Contains(k));
        }

        if (CreatedFrom.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.CreatedDateTime >= CreatedFrom.Value);
        }

        if (CreatedTo.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.CreatedDateTime <= CreatedTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(Role))
        {
            var role = await _identityDbContext.Roles.FirstOrDefaultAsync(r => r.Name == Role);
            if (role != null)
            {
                var userIds = await _identityDbContext.UserRoles.Where(ur => ur.RoleId == role.Id).Select(ur => ur.UserId).ToListAsync();
                usersQuery = usersQuery.Where(u => userIds.Contains(u.Id));
            }
        }

        var total = await usersQuery.LongCountAsync();

        var items = await usersQuery
            .OrderBy(u => u.Email)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var resultItems = new List<AdminUserStatsDto>();
        foreach (var u in items)
        {
            var roles = await _identityDbContext.UserRoles
                .Where(ur => ur.UserId == u.Id)
                .Join(_identityDbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                .ToListAsync();

            var totalSpent = await _subscriptionDbContext.PaymentTransactions
                .Where(p => p.UserId == u.Id && p.Status == PaymentStatus.Succeeded)
                .Select(p => (decimal?)p.Amount)
                .SumAsync() ?? 0m;

            var currentSub = await _subscriptionDbContext.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == u.Id && s.Status == SubscriptionStatus.Active)
                .OrderByDescending(s => s.CreatedDateTime)
                .FirstOrDefaultAsync();

            resultItems.Add(new AdminUserStatsDto
            {
                UserId = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                Roles = roles,
                TotalSpentCents = (long?)(totalSpent * 100m),
                CurrentMembership = currentSub?.Plan?.DisplayName,
                MembershipExpiresAt = currentSub?.EndDate is DateOnly d ? new DateTimeOffset(d.ToDateTime(new TimeOnly(0,0)), TimeSpan.Zero) : (DateTimeOffset?)null,
                CreatedAtUtc = u.CreatedDateTime,
                UpdatedAtUtc = u.UpdatedDateTime,
            });
        }

        var paged = new Paged<AdminUserStatsDto>
        {
            Items = resultItems,
            TotalItems = total,
            Page = page,
            PageSize = size,
        };

        return Ok(paged);
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<AdminUserStatsDto>> GetUser(Guid userId)
    {
        var u = await _identityDbContext.Users.FindAsync(userId);
        if (u == null)
        {
            return NotFound();
        }

        var roles = await _identityDbContext.UserRoles
            .Where(ur => ur.UserId == u.Id)
            .Join(_identityDbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .ToListAsync();

        var totalSpent = await _subscriptionDbContext.PaymentTransactions
            .Where(p => p.UserId == u.Id && p.Status == PaymentStatus.Succeeded)
            .Select(p => (decimal?)p.Amount)
            .SumAsync() ?? 0m;

        var currentSub = await _subscriptionDbContext.UserSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == u.Id && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.CreatedDateTime)
            .FirstOrDefaultAsync();

        var dto = new AdminUserStatsDto
        {
            UserId = u.Id,
            UserName = u.UserName,
            Email = u.Email,
            Roles = roles,
            TotalSpentCents = (long?)(totalSpent * 100m),
            CurrentMembership = currentSub?.Plan?.DisplayName,
            MembershipExpiresAt = currentSub?.EndDate is DateOnly d ? new DateTimeOffset(d.ToDateTime(new TimeOnly(0,0)), TimeSpan.Zero) : (DateTimeOffset?)null,
            CreatedAtUtc = u.CreatedDateTime,
            UpdatedAtUtc = u.UpdatedDateTime,
        };

        return Ok(dto);
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<object>> GetRevenue([FromQuery] string period = "month", [FromQuery] DateTimeOffset? startDate = null, [FromQuery] DateTimeOffset? endDate = null)
    {
        DateTimeOffset start;
        DateTimeOffset end;
        if (startDate.HasValue && endDate.HasValue)
        {
            start = startDate.Value;
            end = endDate.Value;
        }
        else
        {
            var now = DateTimeOffset.UtcNow;
            if (period == "year")
            {
                start = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
                end = now;
            }
            else
            {
                start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
                end = now;
            }
        }

        var txns = await _subscriptionDbContext.PaymentTransactions
            .Where(t => t.CreatedDateTime >= start && t.CreatedDateTime <= end && t.Status == PaymentStatus.Succeeded)
            .ToListAsync();

        var totalRevenue = txns.Sum(t => (decimal)t.Amount);
        var daily = txns.GroupBy(t => t.CreatedDateTime.Date)
            .Select(g => new { Date = g.Key, RevenueCents = (long)(g.Sum(t => t.Amount) * 100m), Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToList();

        var result = new
        {
            periodType = period,
            periodStart = start,
            periodEnd = end,
            totalRevenueCents = (long)(totalRevenue * 100m),
            transactionCount = txns.Count,
            successfulCount = txns.Count,
            dailyBreakdown = daily.Select(d => new { date = d.Date.ToString("yyyy-MM-dd"), revenueCents = d.RevenueCents, transactionCount = d.Count })
        };

        return Ok(result);
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<Paged<AdminTransactionDto>>> GetTransactions(
        [FromQuery] Guid? UserId = null,
        [FromQuery] Guid? EventId = null,
        [FromQuery] string Status = null,
        [FromQuery] string Direction = null,
        [FromQuery] string Method = null,
        [FromQuery] DateTimeOffset? FromDate = null,
        [FromQuery] DateTimeOffset? ToDate = null,
        [FromQuery] string Period = null,
        [FromQuery] long? MinAmountCents = null,
        [FromQuery] long? MaxAmountCents = null,
        [FromQuery] string Provider = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var q = _subscriptionDbContext.PaymentTransactions.AsNoTracking();

        if (UserId.HasValue)
            q = q.Where(x => x.UserId == UserId.Value);

        if (FromDate.HasValue)
            q = q.Where(x => x.CreatedDateTime >= FromDate.Value);

        if (ToDate.HasValue)
            q = q.Where(x => x.CreatedDateTime <= ToDate.Value);

        var total = await q.LongCountAsync();

        var items = await q.OrderByDescending(x => x.CreatedDateTime)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var mapped = items.Select(x => new AdminTransactionDto
        {
            Id = x.Id,
            UserId = x.UserId,
            AmountCents = (long?)(x.Amount * 100m),
            Currency = x.Currency,
            Method = x.PaymentMethod,
            Status = x.Status.ToString(),
            Provider = x.Provider,
            ProviderRef = x.ProviderRef,
            CreatedAtUtc = x.CreatedDateTime,
            CompletedAtUtc = x.UpdatedDateTime,
        }).ToList();

        var paged = new Paged<AdminTransactionDto>
        {
            Items = mapped,
            TotalItems = total,
            Page = page,
            PageSize = size,
        };

        return Ok(paged);
    }

    [HttpGet("payments")]
    public async Task<ActionResult<Paged<AdminPaymentIntentDto>>> GetPayments(
        [FromQuery] Guid? UserId = null,
        [FromQuery] Guid? EventId = null,
        [FromQuery] Guid? MembershipPlanId = null,
        [FromQuery] string Purpose = null,
        [FromQuery] string Status = null,
        [FromQuery] DateTimeOffset? FromDate = null,
        [FromQuery] DateTimeOffset? ToDate = null,
        [FromQuery] string Period = null,
        [FromQuery] long? MinAmountCents = null,
        [FromQuery] long? MaxAmountCents = null,
        [FromQuery] long? OrderCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var q = _subscriptionDbContext.PaymentIntents.AsNoTracking();

        if (UserId.HasValue)
            q = q.Where(x => x.UserId == UserId.Value);

        if (MembershipPlanId.HasValue)
            q = q.Where(x => x.PlanId == MembershipPlanId.Value);

        if (FromDate.HasValue)
            q = q.Where(x => x.CreatedDateTime >= FromDate.Value);

        if (ToDate.HasValue)
            q = q.Where(x => x.CreatedDateTime <= ToDate.Value);

        var total = await q.LongCountAsync();

        var items = await q.OrderByDescending(x => x.CreatedDateTime)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var mapped = items.Select(x => new AdminPaymentIntentDto
        {
            Id = x.Id,
            UserId = x.UserId,
            AmountCents = (long?)(x.Amount * 100m),
            Purpose = x.Purpose.ToString(),
            MembershipPlanId = x.PlanId,
            MembershipPlanName = x.Plan?.DisplayName,
            Status = x.Status.ToString(),
            OrderCode = x.OrderCode,
            ExpiresAt = x.ExpiresAt,
            CreatedAtUtc = x.CreatedDateTime,
            UpdatedAtUtc = x.UpdatedDateTime,
        }).ToList();

        var paged = new Paged<AdminPaymentIntentDto>
        {
            Items = mapped,
            TotalItems = total,
            Page = page,
            PageSize = size,
        };

        return Ok(paged);
    }

    [HttpGet("memberships")]
    public async Task<ActionResult<Paged<AdminMembershipStatsDto>>> GetMemberships([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var q = _subscriptionDbContext.SubscriptionPlans.AsNoTracking();
        var total = await q.LongCountAsync();

        var items = await q.OrderBy(p => p.Name).Skip((page - 1) * size).Take(size).ToListAsync();

        var mapped = new List<AdminMembershipStatsDto>();
        foreach (var p in items)
        {
            var activeSubscribers = await _subscriptionDbContext.UserSubscriptions.LongCountAsync(s => s.PlanId == p.Id && s.Status == SubscriptionStatus.Active);
            var totalRevenue = await _subscriptionDbContext.PaymentTransactions
                .Where(t => t.Status == PaymentStatus.Succeeded && t.Subscription != null && t.Subscription.PlanId == p.Id)
                .Select(t => (decimal?)t.Amount)
                .SumAsync() ?? 0m;

            mapped.Add(new AdminMembershipStatsDto
            {
                PlanId = p.Id,
                PlanName = p.DisplayName,
                PriceCents = (long?)(p.PriceMonthly * 100m),
                DurationMonths = null,
                MonthlyEventLimit = null,
                IsActive = p.IsActive,
                ActiveSubscribers = activeSubscribers,
                TotalRevenueCents = (long?)(totalRevenue * 100m),
            });
        }

        var paged = new Paged<AdminMembershipStatsDto>
        {
            Items = mapped,
            TotalItems = total,
            Page = page,
            PageSize = size,
        };

        return Ok(paged);
    }

    [HttpGet("roles")]
    public async Task<ActionResult<Paged<AdminRoleStatsDto>>> GetRoles([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var q = _identityDbContext.Roles.AsNoTracking();
        var total = await q.LongCountAsync();

        var items = await q.OrderBy(r => r.Name).Skip((page - 1) * size).Take(size).ToListAsync();

        var mapped = new List<AdminRoleStatsDto>();
        foreach (var r in items)
        {
            var usersCount = await _identityDbContext.UserRoles.LongCountAsync(ur => ur.RoleId == r.Id);
            mapped.Add(new AdminRoleStatsDto
            {
                RoleId = r.Id,
                RoleName = r.Name,
                UsersCount = usersCount,
                CreatedAtUtc = r.CreatedDateTime,
            });
        }

        var paged = new Paged<AdminRoleStatsDto>
        {
            Items = mapped,
            TotalItems = total,
            Page = page,
            PageSize = size,
        };

        return Ok(paged);
    }

    // Communities/Clubs/Games endpoints are not implemented in current modules; return empty pages as placeholders.
    [HttpGet("communities")]
    public ActionResult<Paged<object>> GetCommunities([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var paged = new Paged<object> { Items = new List<object>(), TotalItems = 0, Page = page, PageSize = size };
        return Ok(paged);
    }

    [HttpGet("clubs")]
    public ActionResult<Paged<object>> GetClubs([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var paged = new Paged<object> { Items = new List<object>(), TotalItems = 0, Page = page, PageSize = size };
        return Ok(paged);
    }

    [HttpGet("games")]
    public ActionResult<Paged<object>> GetGames([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var paged = new Paged<object> { Items = new List<object>(), TotalItems = 0, Page = page, PageSize = size };
        return Ok(paged);
    }
}
