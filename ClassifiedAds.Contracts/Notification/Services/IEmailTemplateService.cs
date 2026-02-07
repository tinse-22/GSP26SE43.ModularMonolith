namespace ClassifiedAds.Contracts.Notification.Services;

/// <summary>
/// Generates HTML email bodies from predefined templates.
/// Interface lives in Contracts so any module can use it without coupling to Notification module internals.
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Generate the Welcome + Email Confirmation template.
    /// </summary>
    string WelcomeConfirmEmail(string displayName, string confirmationUrl);

    /// <summary>
    /// Generate the Resend Email Confirmation template.
    /// </summary>
    string ResendConfirmEmail(string displayName, string confirmationUrl);

    /// <summary>
    /// Generate the Forgot Password / Reset Password Link template.
    /// </summary>
    string ForgotPassword(string displayName, string resetUrl);

    /// <summary>
    /// Generate the Password Changed Successfully confirmation template.
    /// </summary>
    string PasswordChanged(string displayName);

    /// <summary>
    /// Generate the Admin-initiated Password Reset template.
    /// </summary>
    string AdminResetPassword(string displayName, string resetUrl);

    /// <summary>
    /// Generate the Admin-initiated Email Confirmation template.
    /// </summary>
    string AdminConfirmEmail(string displayName, string confirmationUrl);

    /// <summary>
    /// Generate the Admin Set Password notification template.
    /// </summary>
    string AdminSetPassword(string displayName);

    /// <summary>
    /// Generate the Account Locked notification template.
    /// </summary>
    string AccountLocked(string displayName, string lockoutEnd);

    /// <summary>
    /// Generate the Account Unlocked notification template.
    /// </summary>
    string AccountUnlocked(string displayName);
}
