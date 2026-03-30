using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.PasswordValidators;

/// <summary>
/// Validates that a new password has not been used recently by the user.
/// Checks against the last N passwords stored in the password history table.
/// Also enforces minimum password age policy if configured.
/// </summary>
public class HistoricalPasswordValidator : IPasswordValidator<User>
{
    private readonly IPasswordHistoryRepository _passwordHistoryRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<HistoricalPasswordValidator> _logger;
    private readonly PasswordValidationOptions _options;

    public HistoricalPasswordValidator(
        IPasswordHistoryRepository passwordHistoryRepository,
        IPasswordHasher<User> passwordHasher,
        IDateTimeProvider dateTimeProvider,
        ILogger<HistoricalPasswordValidator> logger,
        IOptions<PasswordValidationOptions> options)
    {
        _passwordHistoryRepository = passwordHistoryRepository;
        _passwordHasher = passwordHasher;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _options = options?.Value ?? new PasswordValidationOptions();
    }

    public async Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string password)
    {
        if (user == null || user.Id == Guid.Empty)
        {
            // New user, no history to check
            return IdentityResult.Success;
        }

        // 1. Check minimum password age
        if (_options.MinimumPasswordAgeHours > 0)
        {
            var ageError = await CheckMinimumPasswordAgeAsync(user.Id);
            if (ageError != null)
            {
                return IdentityResult.Failed(ageError);
            }
        }

        // 2. Check password history
        if (_options.PasswordHistoryCount > 0)
        {
            var historyError = await CheckPasswordHistoryAsync(user, password);
            if (historyError != null)
            {
                return IdentityResult.Failed(historyError);
            }
        }

        return IdentityResult.Success;
    }

    /// <summary>
    /// Checks if enough time has passed since the last password change.
    /// </summary>
    private async Task<IdentityError> CheckMinimumPasswordAgeAsync(Guid userId)
    {
        var lastChange = await _passwordHistoryRepository.GetLastPasswordChangeDateAsync(userId);

        if (lastChange.HasValue)
        {
            var minimumAge = TimeSpan.FromHours(_options.MinimumPasswordAgeHours);
            var timeSinceChange = _dateTimeProvider.OffsetUtcNow - lastChange.Value;

            if (timeSinceChange < minimumAge)
            {
                var remainingTime = minimumAge - timeSinceChange;
                _logger.LogDebug(
                    "Password change rejected: minimum age not met. Last change: {LastChange}, Remaining: {Remaining}",
                    lastChange.Value,
                    remainingTime);

                return new IdentityError
                {
                    Code = "PasswordTooNew",
                    Description = $"Bạn phải đợi ít nhất {FormatTimeSpan(remainingTime)} trước khi có thể đổi mật khẩu.",
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if the new password matches any of the recent passwords.
    /// </summary>
    private async Task<IdentityError> CheckPasswordHistoryAsync(User user, string newPassword)
    {
        var recentPasswords = await _passwordHistoryRepository.GetRecentByUserIdAsync(
            user.Id,
            _options.PasswordHistoryCount);

        foreach (var history in recentPasswords)
        {
            var result = _passwordHasher.VerifyHashedPassword(user, history.PasswordHash, newPassword);

            if (result == PasswordVerificationResult.Success ||
                result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                _logger.LogDebug(
                    "Password change rejected: matches historical password from {Date}",
                    history.CreatedDateTime);

                return new IdentityError
                {
                    Code = "PasswordRecentlyUsed",
                    Description = $"Mật khẩu này đã được sử dụng trong {_options.PasswordHistoryCount} lần đổi mật khẩu gần đây. Vui lòng chọn mật khẩu khác.",
                };
            }
        }

        // Also check the current password
        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, newPassword);

            if (result == PasswordVerificationResult.Success ||
                result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                _logger.LogDebug("Password change rejected: same as current password");

                return new IdentityError
                {
                    Code = "PasswordSameAsCurrent",
                    Description = "Mật khẩu mới không được trùng với mật khẩu hiện tại.",
                };
            }
        }

        return null;
    }

    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
        {
            return $"{(int)timeSpan.TotalDays} ngày";
        }
        if (timeSpan.TotalHours >= 1)
        {
            return $"{(int)timeSpan.TotalHours} giờ";
        }
        if (timeSpan.TotalMinutes >= 1)
        {
            return $"{(int)timeSpan.TotalMinutes} phút";
        }
        return $"{(int)timeSpan.TotalSeconds} giây";
    }
}
