using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
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

        // Store refresh token
        await _userManager.SetAuthenticationTokenAsync(
            user,
            "ClassifiedAds",
            "RefreshToken",
            refreshTokenHash);

        // Store refresh token expiration
        await _userManager.SetAuthenticationTokenAsync(
            user,
            "ClassifiedAds",
            "RefreshTokenExpiration",
            DateTime.UtcNow.AddDays(RefreshTokenExpirationDays).ToString("O"));

        // Store user ID with refresh token for faster lookup
        await _userManager.SetAuthenticationTokenAsync(
            user,
            "ClassifiedAds",
            "RefreshTokenUserId",
            user.Id.ToString());

        return (accessToken, refreshToken, AccessTokenExpirationMinutes * 60);
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
        await _userManager.RemoveAuthenticationTokenAsync(
            user,
            "ClassifiedAds",
            "RefreshToken");

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
            .Where(t => t.LoginProvider == "ClassifiedAds"
                     && t.TokenName == "RefreshToken"
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
            "ClassifiedAds",
            "RefreshTokenExpiration");

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
            await _userManager.RemoveAuthenticationTokenAsync(
                user,
                "ClassifiedAds",
                "RefreshToken");

            await _userManager.RemoveAuthenticationTokenAsync(
                user,
                "ClassifiedAds",
                "RefreshTokenExpiration");
        }
    }

    public async Task RevokeAllRefreshTokensAsync(Guid userId)
    {
        // This effectively logs out the user from all devices
        await RevokeRefreshTokenAsync(userId);

        // Also remove the user ID lookup token
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user != null)
        {
            await _userManager.RemoveAuthenticationTokenAsync(
                user,
                "ClassifiedAds",
                "RefreshTokenUserId");
        }
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
