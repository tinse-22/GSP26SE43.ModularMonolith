using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IdentityModuleOptions _options;
    private readonly UserManager<User> _userManager;
    private const int AccessTokenExpirationMinutes = 60;
    private const int RefreshTokenExpirationDays = 7;

    public JwtTokenService(
        IOptionsSnapshot<IdentityModuleOptions> options,
        UserManager<User> userManager)
    {
        _options = options.Value;
        _userManager = userManager;
    }

    public async Task<(string AccessToken, string RefreshToken, int ExpiresIn)> GenerateTokensAsync(User user, IList<string> roles)
    {
        var accessToken = GenerateAccessToken(user, roles);
        var refreshToken = GenerateRefreshToken();

        // Store refresh token
        await _userManager.SetAuthenticationTokenAsync(
            user,
            "ClassifiedAds",
            "RefreshToken",
            refreshToken);

        // Store refresh token expiration
        await _userManager.SetAuthenticationTokenAsync(
            user,
            "ClassifiedAds",
            "RefreshTokenExpiration",
            DateTime.UtcNow.AddDays(RefreshTokenExpirationDays).ToString("O"));

        return (accessToken, refreshToken, AccessTokenExpirationMinutes * 60);
    }

    public async Task<ClaimsPrincipal> ValidateRefreshTokenAsync(string refreshToken)
    {
        // Find user by refresh token
        var users = _userManager.Users;
        foreach (var user in users)
        {
            var storedToken = await _userManager.GetAuthenticationTokenAsync(
                user,
                "ClassifiedAds",
                "RefreshToken");

            if (storedToken == refreshToken)
            {
                // Check expiration
                var expirationStr = await _userManager.GetAuthenticationTokenAsync(
                    user,
                    "ClassifiedAds",
                    "RefreshTokenExpiration");

                if (!string.IsNullOrEmpty(expirationStr) && DateTime.TryParse(expirationStr, out var expiration))
                {
                    if (expiration > DateTime.UtcNow)
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                            new Claim(ClaimTypes.Name, user.UserName),
                            new Claim(ClaimTypes.Email, user.Email),
                            new Claim("sub", user.Id.ToString()),
                        };

                        foreach (var role in roles)
                        {
                            claims.Add(new Claim(ClaimTypes.Role, role));
                        }

                        var identity = new ClaimsIdentity(claims, "RefreshToken");
                        return new ClaimsPrincipal(identity);
                    }
                }
            }
        }

        return null;
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

        // Use a secure key from configuration or generate one
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetSecretKey()));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.IdentityServer?.Authority ?? "ClassifiedAds",
            audience: "ClassifiedAds.WebAPI",
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

    private string GetSecretKey()
    {
        // In production, this should come from secure configuration
        // For now, using a static key for development
        return "ClassifiedAds-Super-Secret-Key-For-JWT-Token-Generation-2026!@#$%";
    }
}
