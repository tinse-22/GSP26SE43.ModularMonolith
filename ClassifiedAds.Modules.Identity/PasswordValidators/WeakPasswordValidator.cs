using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.PasswordValidators;

/// <summary>
/// Validates passwords against multiple security criteria:
/// 1. Common password dictionary
/// 2. Have I Been Pwned API (leaked passwords)
/// 3. Pattern detection (keyboard patterns, sequences, repeated chars)
/// 4. Entropy calculation
/// 5. User-related information check
/// </summary>
public class WeakPasswordValidator : IPasswordValidator<User>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WeakPasswordValidator> _logger;
    private readonly PasswordValidationOptions _options;

    // Common weak passwords - top 1000 from SecLists
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Top 100 most common passwords
        "123456", "password", "12345678", "qwerty", "123456789", "12345", "1234", "111111",
        "1234567", "dragon", "123123", "baseball", "abc123", "football", "monkey", "letmein",
        "696969", "shadow", "master", "666666", "qwertyuiop", "123321", "mustang", "1234567890",
        "michael", "654321", "pussy", "superman", "1qaz2wsx", "7777777", "fuckyou", "121212",
        "000000", "qazwsx", "123qwe", "killer", "trustno1", "jordan", "jennifer", "zxcvbnm",
        "asdfgh", "hunter", "buster", "soccer", "harley", "batman", "andrew", "tigger",
        "sunshine", "iloveyou", "fuckme", "2000", "charlie", "robert", "thomas", "hockey",
        "ranger", "daniel", "starwars", "klaster", "112233", "george", "asshole", "computer",
        "michelle", "jessica", "pepper", "1111", "zxcvbn", "555555", "11111111", "131313",
        "freedom", "777777", "pass", "fuck", "maggie", "159753", "aaaaaa", "ginger", "princess",
        "joshua", "cheese", "amanda", "summer", "love", "ashley", "6969", "nicole", "chelsea",
        "biteme", "matthew", "access", "yankees", "987654321", "dallas", "austin", "thunder",
        "taylor", "matrix", "minecraft", "admin", "password1", "password123", "welcome",
        // Vietnamese common passwords
        "matkhau", "123456a", "anhyeuem", "yeuem", "mothai", "xinchao", "vietnam", "saigon",
        "hanoi", "thanhpho", "congty", "truong", "hocsinh", "sinhvien", "giaovien",
        // Year-based passwords
        "2020", "2021", "2022", "2023", "2024", "2025", "2026",
        // Keyboard patterns (more comprehensive)
        "qwerty123", "asdf1234", "zxcvbnm123", "1q2w3e4r", "1q2w3e", "q1w2e3r4", "qweasd",
        "qweasdzxc", "1qazxsw2", "zaq12wsx", "!qaz2wsx", "1qaz@wsx",
        // Simple patterns
        "abcd1234", "abcdef", "abcdefgh", "aaaa1111", "1111aaaa", "a1b2c3d4",
        // Common names + numbers
        "john123", "mike123", "david123", "james123", "admin123", "user123", "test123",
        // Phrases
        "letmein123", "welcome1", "welcome123", "hello123", "changeme", "temp123", "guest123",
    };

    // Keyboard patterns for detection
    private static readonly string[] KeyboardRows =
    {
        "qwertyuiop", "asdfghjkl", "zxcvbnm",
        "1234567890", "`1234567890-=",
        "qwertyuiop[]\\", "asdfghjkl;'", "zxcvbnm,./",
    };

    private static readonly string[] KeyboardDiagonals =
    {
        "1qaz", "2wsx", "3edc", "4rfv", "5tgb", "6yhn", "7ujm", "8ik,", "9ol.", "0p;/",
        "zaq1", "xsw2", "cde3", "vfr4", "bgt5", "nhy6", "mju7", ",ki8", ".lo9", "/;p0",
    };

    public WeakPasswordValidator(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<WeakPasswordValidator> logger,
        IOptions<PasswordValidationOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
        _options = options?.Value ?? new PasswordValidationOptions();
    }

    public async Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string password)
    {
        var errors = new List<IdentityError>();

        if (string.IsNullOrWhiteSpace(password))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordRequired",
                Description = "Mật khẩu không được để trống.",
            });
        }

        // 1. Dictionary check
        if (_options.EnableDictionaryCheck)
        {
            var dictionaryError = CheckDictionary(password);
            if (dictionaryError != null)
            {
                errors.Add(dictionaryError);
            }
        }

        // 2. Pattern detection
        if (_options.EnablePatternDetection)
        {
            var patternErrors = CheckPatterns(password);
            errors.AddRange(patternErrors);
        }

        // 3. Entropy check
        if (_options.EnableEntropyCheck)
        {
            var entropyError = CheckEntropy(password);
            if (entropyError != null)
            {
                errors.Add(entropyError);
            }
        }

        // 4. User info check
        if (_options.EnableUserInfoCheck && user != null)
        {
            var userInfoError = CheckUserInfo(password, user);
            if (userInfoError != null)
            {
                errors.Add(userInfoError);
            }
        }

        // 5. HIBP check (async, last because it's external)
        if (_options.EnableHibpCheck && errors.Count == 0)
        {
            var hibpError = await CheckHibpAsync(password);
            if (hibpError != null)
            {
                errors.Add(hibpError);
            }
        }

        return errors.Count > 0
            ? IdentityResult.Failed(errors.ToArray())
            : IdentityResult.Success;
    }

    #region Dictionary Check

    private IdentityError CheckDictionary(string password)
    {
        // Direct match
        if (CommonPasswords.Contains(password))
        {
            _logger.LogDebug("Password rejected: found in common dictionary");
            return new IdentityError
            {
                Code = "CommonPassword",
                Description = "Mật khẩu này quá phổ biến và dễ đoán. Vui lòng chọn mật khẩu khác.",
            };
        }

        // Check with common substitutions (l33t speak)
        var normalized = NormalizeLeetSpeak(password);
        if (normalized != password && CommonPasswords.Contains(normalized))
        {
            _logger.LogDebug("Password rejected: l33t variant of common password");
            return new IdentityError
            {
                Code = "CommonPasswordVariant",
                Description = "Mật khẩu này là biến thể của mật khẩu phổ biến. Vui lòng chọn mật khẩu khác.",
            };
        }

        return null;
    }

    private static string NormalizeLeetSpeak(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            sb.Append(c switch
            {
                '0' => 'o',
                '1' => 'i',
                '3' => 'e',
                '4' => 'a',
                '5' => 's',
                '7' => 't',
                '8' => 'b',
                '@' => 'a',
                '$' => 's',
                '!' => 'i',
                _ => char.ToLowerInvariant(c),
            });
        }
        return sb.ToString();
    }

    #endregion

    #region Pattern Detection

    private List<IdentityError> CheckPatterns(string password)
    {
        var errors = new List<IdentityError>();
        var lower = password.ToLowerInvariant();

        // Check for keyboard patterns
        if (ContainsKeyboardPattern(lower, 4))
        {
            errors.Add(new IdentityError
            {
                Code = "KeyboardPattern",
                Description = "Mật khẩu chứa mẫu bàn phím dễ đoán (ví dụ: qwerty, asdf).",
            });
        }

        // Check for sequential characters
        if (ContainsSequentialChars(lower, 4))
        {
            errors.Add(new IdentityError
            {
                Code = "SequentialPattern",
                Description = "Mật khẩu chứa chuỗi ký tự liên tiếp (ví dụ: abcd, 1234).",
            });
        }

        // Check for repeated characters
        if (ContainsRepeatedChars(password, 3))
        {
            errors.Add(new IdentityError
            {
                Code = "RepeatedChars",
                Description = "Mật khẩu chứa quá nhiều ký tự lặp lại liên tiếp.",
            });
        }

        // Check if mostly same character
        if (IsMostlySameChar(password, 0.6))
        {
            errors.Add(new IdentityError
            {
                Code = "LowVariety",
                Description = "Mật khẩu có quá ít sự đa dạng về ký tự.",
            });
        }

        return errors;
    }

    private static bool ContainsKeyboardPattern(string password, int minLength)
    {
        // Check row patterns
        foreach (var row in KeyboardRows)
        {
            for (int len = minLength; len <= Math.Min(row.Length, password.Length); len++)
            {
                for (int i = 0; i <= row.Length - len; i++)
                {
                    var pattern = row.Substring(i, len);
                    if (password.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    // Also check reversed
                    var reversed = new string(pattern.Reverse().ToArray());
                    if (password.Contains(reversed, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        // Check diagonal patterns
        foreach (var diagonal in KeyboardDiagonals)
        {
            if (password.Contains(diagonal, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            var reversed = new string(diagonal.Reverse().ToArray());
            if (password.Contains(reversed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSequentialChars(string password, int minLength)
    {
        if (password.Length < minLength)
        {
            return false;
        }

        int ascending = 1;
        int descending = 1;

        for (int i = 1; i < password.Length; i++)
        {
            int diff = password[i] - password[i - 1];

            if (diff == 1)
            {
                ascending++;
                descending = 1;
                if (ascending >= minLength)
                {
                    return true;
                }
            }
            else if (diff == -1)
            {
                descending++;
                ascending = 1;
                if (descending >= minLength)
                {
                    return true;
                }
            }
            else
            {
                ascending = 1;
                descending = 1;
            }
        }

        return false;
    }

    private static bool ContainsRepeatedChars(string password, int minRepeat)
    {
        if (password.Length < minRepeat)
        {
            return false;
        }

        int count = 1;
        for (int i = 1; i < password.Length; i++)
        {
            if (char.ToLowerInvariant(password[i]) == char.ToLowerInvariant(password[i - 1]))
            {
                count++;
                if (count >= minRepeat)
                {
                    return true;
                }
            }
            else
            {
                count = 1;
            }
        }

        return false;
    }

    private static bool IsMostlySameChar(string password, double threshold)
    {
        if (password.Length < 4)
        {
            return false;
        }

        var charCounts = new Dictionary<char, int>();
        foreach (var c in password.ToLowerInvariant())
        {
            charCounts.TryGetValue(c, out int count);
            charCounts[c] = count + 1;
        }

        int maxCount = charCounts.Values.Max();
        return (double)maxCount / password.Length >= threshold;
    }

    #endregion

    #region Entropy Check

    private IdentityError CheckEntropy(string password)
    {
        double entropy = CalculateShannonEntropy(password);

        if (entropy < _options.MinimumEntropyBits)
        {
            _logger.LogDebug("Password rejected: entropy {Entropy} bits < {Min} bits", entropy, _options.MinimumEntropyBits);
            return new IdentityError
            {
                Code = "LowEntropy",
                Description = $"Mật khẩu không đủ phức tạp. Hãy sử dụng kết hợp chữ hoa, chữ thường, số và ký tự đặc biệt.",
            };
        }

        return null;
    }

    /// <summary>
    /// Calculates Shannon entropy of the password.
    /// Formula: H = -Σ(p * log2(p)) where p is probability of each character.
    /// Then multiply by length to get total bits.
    /// </summary>
    private static double CalculateShannonEntropy(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return 0;
        }

        // Count character frequencies
        var frequency = new Dictionary<char, int>();
        foreach (var c in password)
        {
            frequency.TryGetValue(c, out int count);
            frequency[c] = count + 1;
        }

        // Calculate entropy
        double entropy = 0;
        foreach (var count in frequency.Values)
        {
            double probability = (double)count / password.Length;
            entropy -= probability * Math.Log2(probability);
        }

        // Multiply by length to get total bits
        return entropy * password.Length;
    }

    #endregion

    #region User Info Check

    private IdentityError CheckUserInfo(string password, User user)
    {
        var lower = password.ToLowerInvariant();

        // Check username
        if (!string.IsNullOrEmpty(user.UserName) && user.UserName.Length >= 3)
        {
            if (lower.Contains(user.UserName.ToLowerInvariant()))
            {
                return new IdentityError
                {
                    Code = "PasswordContainsUsername",
                    Description = "Mật khẩu không được chứa tên đăng nhập.",
                };
            }
        }

        // Check email parts
        if (!string.IsNullOrEmpty(user.Email))
        {
            var emailParts = user.Email.Split('@', '.');
            foreach (var part in emailParts)
            {
                if (part.Length >= 3 && lower.Contains(part.ToLowerInvariant()))
                {
                    return new IdentityError
                    {
                        Code = "PasswordContainsEmail",
                        Description = "Mật khẩu không được chứa các phần của địa chỉ email.",
                    };
                }
            }
        }

        // Check phone number
        if (!string.IsNullOrEmpty(user.PhoneNumber) && user.PhoneNumber.Length >= 4)
        {
            var digitsOnly = new string(user.PhoneNumber.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length >= 4 && password.Contains(digitsOnly))
            {
                return new IdentityError
                {
                    Code = "PasswordContainsPhone",
                    Description = "Mật khẩu không được chứa số điện thoại.",
                };
            }
        }

        return null;
    }

    #endregion

    #region HIBP Check

    private async Task<IdentityError> CheckHibpAsync(string password)
    {
        try
        {
            // Calculate SHA-1 hash
            var sha1Bytes = SHA1.HashData(Encoding.UTF8.GetBytes(password));
            var hashString = Convert.ToHexString(sha1Bytes);
            var prefix = hashString[..5];
            var suffix = hashString[5..];

            // Check cache first
            var cacheKey = $"hibp:{prefix}";
            if (!_cache.TryGetValue(cacheKey, out HashSet<string> suffixes))
            {
                // Call HIBP API
                var client = _httpClientFactory.CreateClient("HIBP");
                client.BaseAddress = new Uri(_options.HibpApiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(_options.HibpTimeoutSeconds);
                client.DefaultRequestHeaders.Add("User-Agent", "ClassifiedAds-PasswordValidator");

                var response = await client.GetAsync($"/range/{prefix}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    suffixes = ParseHibpResponse(content);

                    // Cache the results
                    _cache.Set(cacheKey, suffixes, TimeSpan.FromHours(_options.HibpCacheHours));
                }
                else
                {
                    _logger.LogWarning("HIBP API returned {StatusCode}, skipping check", response.StatusCode);
                    return null; // Don't block on API failure
                }
            }

            // Check if password hash suffix is in the breached list
            if (suffixes != null && suffixes.Contains(suffix))
            {
                _logger.LogDebug("Password rejected: found in HIBP database");
                return new IdentityError
                {
                    Code = "BreachedPassword",
                    Description = "Mật khẩu này đã xuất hiện trong các vụ rò rỉ dữ liệu. Vui lòng chọn mật khẩu khác.",
                };
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("HIBP API request timed out, skipping check");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HIBP API request failed, skipping check");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during HIBP check");
        }

        return null;
    }

    private static HashSet<string> ParseHibpResponse(string content)
    {
        var suffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Format: SUFFIX:COUNT
            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                var suffix = line[..colonIndex].Trim();
                suffixes.Add(suffix);
            }
        }

        return suffixes;
    }

    #endregion
}
