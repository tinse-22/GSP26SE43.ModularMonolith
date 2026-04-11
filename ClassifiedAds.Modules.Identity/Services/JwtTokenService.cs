using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.Services;

public class JwtTokenService : IJwtTokenService
{
    private const string TokenLoginProvider = "ClassifiedAds";
    private const string RefreshTokenName = "RefreshToken";
    private const string RefreshTokenExpirationName = "RefreshTokenExpiration";
    private const string RefreshTokenUserIdName = "RefreshTokenUserId";

    private readonly IdentityModuleOptions _options;
    private readonly UserManager<User> _userManager;
    private readonly IdentityDbContext _dbContext;

    // Default values if not configured
    private int AccessTokenExpirationMinutes => _options.Jwt?.AccessTokenExpirationMinutes ?? 60;
    private int RefreshTokenExpirationDays => _options.Jwt?.RefreshTokenExpirationDays ?? 7;

    public JwtTokenService(
        IOptionsSnapshot<IdentityModuleOptions> options,
        UserManager<User> userManager,
        IdentityDbContext dbContext)
    {
        _options = options.Value;
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task<(string AccessToken, string RefreshToken, int ExpiresIn)> GenerateTokensAsync(User user, IList<string> roles)
    {
        var accessToken = GenerateAccessToken(user, roles);
        var refreshToken = GenerateRefreshToken();

        // Store refresh token with hash for security
        var refreshTokenHash = HashRefreshToken(refreshToken);

        await PersistRefreshTokensWithRetryAsync(user, refreshTokenHash);

        return (accessToken, refreshToken, AccessTokenExpirationMinutes * 60);
    }

    /// <summary>
    /// Persists refresh token data to the database with retry logic.
    /// Supabase's Supavisor/PgBouncer proxy can cause Npgsql's internal
    /// ManualResetEventSlim to be disposed due to a race condition during
    /// connection recycling. This is not classified as a transient error
    /// by EF Core's retry strategy, so we add explicit retry handling.
    /// </summary>
    private async Task PersistRefreshTokensWithRetryAsync(User user, string refreshTokenHash)
    {
        const int maxRetries = 3;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await PersistRefreshTokensOnceAsync(user, refreshTokenHash);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && IsManualResetEventDisposed(ex))
            {
                await Task.Delay(100 * attempt);
            }
        }
    }

    private Task PersistRefreshTokensOnceAsync(User user, string refreshTokenHash)
    {
        var commandTimestamp = DateTimeOffset.UtcNow;
        var expirationValue = commandTimestamp.UtcDateTime.AddDays(RefreshTokenExpirationDays).ToString("O");
        var tokenData = new Dictionary<string, string>
        {
            [RefreshTokenName] = refreshTokenHash,
            [RefreshTokenExpirationName] = expirationValue,
            [RefreshTokenUserIdName] = user.Id.ToString(),
        };

        var newConcurrencyStamp = Guid.NewGuid().ToString();
        using var connection = CreateDedicatedTokenConnection();
        connection.Open();

        using var upsertTokensCommand = new NpgsqlCommand(
            """
            WITH updated_user AS (
                UPDATE identity."Users"
                SET "ConcurrencyStamp" = @concurrencyStamp,
                    "UpdatedDateTime" = @updatedDateTime
                WHERE "Id" = @userId
                RETURNING 1
            ),
            token_data("Id", "TokenName", "TokenValue") AS (
                VALUES
                    (@tokenId1, @tokenName1, @tokenValue1),
                    (@tokenId2, @tokenName2, @tokenValue2),
                    (@tokenId3, @tokenName3, @tokenValue3)
            ),
            updated_tokens AS (
                UPDATE identity."UserTokens" ut
                SET "TokenValue" = td."TokenValue",
                    "UpdatedDateTime" = @updatedDateTime
                FROM token_data td
                WHERE ut."UserId" = @userId
                  AND ut."LoginProvider" = @loginProvider
                  AND ut."TokenName" = td."TokenName"
                RETURNING td."TokenName"
            ),
            inserted_tokens AS (
                INSERT INTO identity."UserTokens" (
                    "Id",
                    "UserId",
                    "LoginProvider",
                    "TokenName",
                    "TokenValue",
                    "CreatedDateTime",
                    "UpdatedDateTime"
                )
                SELECT
                    td."Id",
                    @userId,
                    @loginProvider,
                    td."TokenName",
                    td."TokenValue",
                    @updatedDateTime,
                    @updatedDateTime
                FROM token_data td
                WHERE EXISTS (SELECT 1 FROM updated_user)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM identity."UserTokens" ut
                      WHERE ut."UserId" = @userId
                        AND ut."LoginProvider" = @loginProvider
                        AND ut."TokenName" = td."TokenName"
                  )
                RETURNING 1
            )
            SELECT COUNT(*) FROM updated_user;
            """,
            connection);
        upsertTokensCommand.Parameters.AddWithValue("concurrencyStamp", newConcurrencyStamp);
        upsertTokensCommand.Parameters.AddWithValue("updatedDateTime", commandTimestamp);
        upsertTokensCommand.Parameters.AddWithValue("userId", user.Id);
        upsertTokensCommand.Parameters.AddWithValue("loginProvider", TokenLoginProvider);
        upsertTokensCommand.Parameters.AddWithValue("tokenId1", Guid.NewGuid());
        upsertTokensCommand.Parameters.AddWithValue("tokenName1", RefreshTokenName);
        upsertTokensCommand.Parameters.AddWithValue("tokenValue1", tokenData[RefreshTokenName]);
        upsertTokensCommand.Parameters.AddWithValue("tokenId2", Guid.NewGuid());
        upsertTokensCommand.Parameters.AddWithValue("tokenName2", RefreshTokenExpirationName);
        upsertTokensCommand.Parameters.AddWithValue("tokenValue2", tokenData[RefreshTokenExpirationName]);
        upsertTokensCommand.Parameters.AddWithValue("tokenId3", Guid.NewGuid());
        upsertTokensCommand.Parameters.AddWithValue("tokenName3", RefreshTokenUserIdName);
        upsertTokensCommand.Parameters.AddWithValue("tokenValue3", tokenData[RefreshTokenUserIdName]);

        var updatedUsers = Convert.ToInt64(upsertTokensCommand.ExecuteScalar());

        if (updatedUsers == 0)
        {
            throw new InvalidOperationException($"Could not persist refresh tokens because user '{user.Id}' was not found.");
        }

        user.ConcurrencyStamp = newConcurrencyStamp;
        SetTokensInMemory(user, tokenData);
        return Task.CompletedTask;
    }

    private static bool IsManualResetEventDisposed(Exception exception)
    {
        if (exception is ObjectDisposedException disposedException)
        {
            return string.Equals(disposedException.ObjectName, "System.Threading.ManualResetEventSlim", StringComparison.Ordinal)
                || disposedException.Message.Contains("ManualResetEventSlim", StringComparison.OrdinalIgnoreCase);
        }

        return exception.InnerException is not null && IsManualResetEventDisposed(exception.InnerException);
    }

    private void SetTokensInMemory(User user, IReadOnlyDictionary<string, string> tokenData)
    {
        // Ensure the Tokens collection is loaded
        user.Tokens ??= new List<Entities.UserToken>();

        foreach (var (name, value) in tokenData)
        {
            var existing = user.Tokens.SingleOrDefault(
                t => t.LoginProvider == TokenLoginProvider && t.TokenName == name);

            if (existing != null)
            {
                existing.TokenValue = value;
            }
            else
            {
                user.Tokens.Add(new Entities.UserToken
                {
                    UserId = user.Id,
                    LoginProvider = TokenLoginProvider,
                    TokenName = name,
                    TokenValue = value,
                });
            }
        }
    }

    private void RemoveTokensInMemory(User user, IReadOnlyCollection<string> tokenNames)
    {
        if (user.Tokens == null || user.Tokens.Count == 0)
        {
            return;
        }

        var tokensToRemove = user.Tokens
            .Where(t => t.LoginProvider == TokenLoginProvider && tokenNames.Contains(t.TokenName))
            .ToList();

        foreach (var token in tokensToRemove)
        {
            user.Tokens.Remove(token);
        }
    }

    public async Task<ClaimsPrincipal?> ValidateRefreshTokenAsync(string refreshToken)
    {
        var result = await ValidateRefreshTokenInternalAsync(refreshToken);
        return result?.Principal;
    }

    public async Task<(string AccessToken, string RefreshToken, int ExpiresIn, ClaimsPrincipal Principal)?> ValidateAndRotateRefreshTokenAsync(string refreshToken)
    {
        var validation = await ValidateRefreshTokenInternalAsync(refreshToken);
        if (validation == null)
        {
            return null;
        }

        var (user, principal) = validation.Value;

        // Revoke old refresh token immediately (token rotation)
        await DeleteRefreshTokensWithRetryAsync(user, includeLookupToken: false);

        // Generate new tokens
        var roles = await _userManager.GetRolesAsync(user);
        var (newAccessToken, newRefreshToken, expiresIn) = await GenerateTokensAsync(user, roles);

        return (newAccessToken, newRefreshToken, expiresIn, principal);
    }

    private async Task<(User User, ClaimsPrincipal Principal)?> ValidateRefreshTokenInternalAsync(string refreshToken)
    {
        var refreshTokenHash = HashRefreshToken(refreshToken);

        // Optimized: Query directly instead of scanning all users
        var userToken = await _dbContext.Set<UserToken>()
            .Where(t => t.LoginProvider == TokenLoginProvider
                     && t.TokenName == RefreshTokenName
                     && t.TokenValue == refreshTokenHash)
            .FirstOrDefaultAsync();

        if (userToken == null)
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(userToken.UserId.ToString());
        if (user == null)
        {
            return null;
        }

        // Check if user is locked out
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            return null;
        }

        // Check expiration
        var expirationStr = await _userManager.GetAuthenticationTokenAsync(
            user,
            TokenLoginProvider,
            RefreshTokenExpirationName);

        if (string.IsNullOrEmpty(expirationStr) || !DateTime.TryParse(expirationStr, out var expiration))
        {
            return null;
        }

        if (expiration <= DateTime.UtcNow)
        {
            // Token expired, clean up
            await RevokeRefreshTokenAsync(user.Id);
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim("sub", user.Id.ToString()),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "RefreshToken");
        var principal = new ClaimsPrincipal(identity);

        return (user, principal);
    }

    public async Task RevokeRefreshTokenAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user != null)
        {
            await DeleteRefreshTokensWithRetryAsync(user, includeLookupToken: false);
        }
    }

    public async Task RevokeAllRefreshTokensAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user != null)
        {
            await DeleteRefreshTokensWithRetryAsync(user, includeLookupToken: true);
        }
    }

    private async Task DeleteRefreshTokensWithRetryAsync(User user, bool includeLookupToken)
    {
        const int maxRetries = 3;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await DeleteRefreshTokensOnceAsync(user, includeLookupToken);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && IsManualResetEventDisposed(ex))
            {
                await Task.Delay(100 * attempt);
            }
        }
    }

    private Task DeleteRefreshTokensOnceAsync(User user, bool includeLookupToken)
    {
        var commandTimestamp = DateTimeOffset.UtcNow;
        var tokenNames = GetRefreshTokenNames(includeLookupToken);
        var newConcurrencyStamp = Guid.NewGuid().ToString();

        using var connection = CreateDedicatedTokenConnection();
        connection.Open();

        using var deleteTokensCommand = new NpgsqlCommand(
            """
            WITH updated_user AS (
                UPDATE identity."Users"
                SET "ConcurrencyStamp" = @concurrencyStamp,
                    "UpdatedDateTime" = @updatedDateTime
                WHERE "Id" = @userId
                RETURNING 1
            ),
            deleted_tokens AS (
                DELETE FROM identity."UserTokens"
                WHERE "UserId" = @userId
                  AND "LoginProvider" = @loginProvider
                  AND "TokenName" = ANY (@tokenNames)
                RETURNING 1
            )
            SELECT COUNT(*) FROM updated_user;
            """,
            connection);
        deleteTokensCommand.Parameters.AddWithValue("concurrencyStamp", newConcurrencyStamp);
        deleteTokensCommand.Parameters.AddWithValue("updatedDateTime", commandTimestamp);
        deleteTokensCommand.Parameters.AddWithValue("userId", user.Id);
        deleteTokensCommand.Parameters.AddWithValue("loginProvider", TokenLoginProvider);
        deleteTokensCommand.Parameters.AddWithValue("tokenNames", tokenNames);

        var updatedUsers = Convert.ToInt64(deleteTokensCommand.ExecuteScalar());

        if (updatedUsers == 0)
        {
            throw new InvalidOperationException($"Could not revoke refresh tokens because user '{user.Id}' was not found.");
        }

        user.ConcurrencyStamp = newConcurrencyStamp;
        RemoveTokensInMemory(user, tokenNames);
        return Task.CompletedTask;
    }

    private NpgsqlConnection CreateDedicatedTokenConnection()
    {
        var builder = new NpgsqlConnectionStringBuilder(_options.ConnectionStrings.Default)
        {
            Pooling = false,
            NoResetOnClose = false,
        };

        return new NpgsqlConnection(builder.ConnectionString);
    }

    private static string[] GetRefreshTokenNames(bool includeLookupToken)
    {
        return includeLookupToken
            ? new[] { RefreshTokenName, RefreshTokenExpirationName, RefreshTokenUserIdName }
            : new[] { RefreshTokenName, RefreshTokenExpirationName };
    }

    private string GenerateAccessToken(User user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Email, user.Email),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetSecretKey()));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Jwt?.Issuer ?? _options.IdentityServer?.Authority ?? "ClassifiedAds",
            audience: _options.Jwt?.Audience ?? "ClassifiedAds.WebAPI",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenExpirationMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private static string HashRefreshToken(string refreshToken)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(refreshToken);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private string GetSecretKey()
    {
        // Get from configuration - MUST be configured in production
        var secretKey = _options.Jwt?.SecretKey;

        if (string.IsNullOrEmpty(secretKey))
        {
            throw new InvalidOperationException(
                "JWT Secret Key is not configured. " +
                "Please set 'Modules:Identity:Jwt:SecretKey' in configuration or User Secrets.");
        }

        if (secretKey.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT Secret Key must be at least 32 characters for HMAC-SHA256.");
        }

        return secretKey;
    }
}
