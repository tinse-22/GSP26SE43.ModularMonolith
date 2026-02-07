using System;
using ClassifiedAds.Contracts.Notification.Services;

namespace ClassifiedAds.Modules.Notification.Services;

/// <summary>
/// Generates HTML email bodies from predefined Vietnamese templates.
/// Uses a shared base layout with consistent branding.
/// </summary>
public sealed class EmailTemplateService : IEmailTemplateService
{
    private const string AppName = "ClassifiedAds";
    private const string PrimaryColor = "#2563EB";
    private const string SuccessColor = "#16A34A";
    private const string WarningColor = "#DC2626";
    private const string TextColor = "#1F2937";
    private const string MutedColor = "#6B7280";

    public string WelcomeConfirmEmail(string displayName, string confirmationUrl)
    {
        var content = $@"
            <h2 style='color: {PrimaryColor}; margin: 0 0 16px 0; font-size: 22px;'>
                Chào mừng bạn đến với {AppName}! 🎉
            </h2>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Xin chào <strong>{EscapeHtml(displayName)}</strong>,
            </p>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Cảm ơn bạn đã đăng ký tài khoản. Để bắt đầu sử dụng dịch vụ, vui lòng xác nhận địa chỉ email của bạn bằng cách nhấn vào nút bên dưới:
            </p>
            {RenderButton("Xác nhận Email", confirmationUrl, PrimaryColor)}
            <p style='color: {MutedColor}; font-size: 13px; line-height: 1.5; margin: 16px 0 0 0;'>
                Liên kết này sẽ hết hạn sau <strong>2 ngày</strong>.<br/>
                Nếu bạn không tạo tài khoản này, vui lòng bỏ qua email này.
            </p>";

        return WrapInBaseLayout(content, "Xác nhận Email");
    }

    public string ResendConfirmEmail(string displayName, string confirmationUrl)
    {
        var content = $@"
            <h2 style='color: {PrimaryColor}; margin: 0 0 16px 0; font-size: 22px;'>
                Xác nhận địa chỉ Email
            </h2>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Xin chào <strong>{EscapeHtml(displayName)}</strong>,
            </p>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Bạn đã yêu cầu gửi lại email xác nhận. Vui lòng nhấn vào nút bên dưới để xác nhận địa chỉ email:
            </p>
            {RenderButton("Xác nhận Email", confirmationUrl, PrimaryColor)}
            <p style='color: {MutedColor}; font-size: 13px; line-height: 1.5; margin: 16px 0 0 0;'>
                Nếu bạn không yêu cầu điều này, vui lòng bỏ qua email này.
            </p>";

        return WrapInBaseLayout(content, "Xác nhận Email");
    }

    public string ForgotPassword(string displayName, string resetUrl)
    {
        var content = $@"
            <h2 style='color: {PrimaryColor}; margin: 0 0 16px 0; font-size: 22px;'>
                Đặt lại mật khẩu
            </h2>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Xin chào <strong>{EscapeHtml(displayName)}</strong>,
            </p>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Nhấn vào nút bên dưới để tạo mật khẩu mới:
            </p>
            {RenderButton("Đặt lại mật khẩu", resetUrl, PrimaryColor)}
            <p style='color: {MutedColor}; font-size: 13px; line-height: 1.5; margin: 16px 0 0 0;'>
                Liên kết này sẽ hết hạn sau <strong>3 giờ</strong>.<br/>
                Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này. Tài khoản của bạn vẫn an toàn.
            </p>";

        return WrapInBaseLayout(content, "Đặt lại mật khẩu");
    }

    public string PasswordChanged(string displayName)
    {
        var content = $@"
            <h2 style='color: {SuccessColor}; margin: 0 0 16px 0; font-size: 22px;'>
                Mật khẩu đã được thay đổi ✓
            </h2>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Xin chào <strong>{EscapeHtml(displayName)}</strong>,
            </p>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Mật khẩu tài khoản của bạn đã được thay đổi thành công.
            </p>
            {RenderInfoBox(@"
                <strong>⏰ Thời gian:</strong> " + DateTimeOffset.UtcNow.ToString("dd/MM/yyyy HH:mm:ss") + @" (UTC)<br/>
                Nếu bạn <strong>không thực hiện</strong> thay đổi này, vui lòng liên hệ bộ phận hỗ trợ ngay lập tức.
            ", WarningColor)}";

        return WrapInBaseLayout(content, "Mật khẩu đã thay đổi");
    }

    public string AdminResetPassword(string displayName, string resetUrl)
    {
        var content = $@"
            <h2 style='color: {PrimaryColor}; margin: 0 0 16px 0; font-size: 22px;'>
                Yêu cầu đặt lại mật khẩu
            </h2>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Xin chào <strong>{EscapeHtml(displayName)}</strong>,
            </p>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Quản trị viên đã yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Vui lòng nhấn vào nút bên dưới để tạo mật khẩu mới:
            </p>
            {RenderButton("Đặt lại mật khẩu", resetUrl, PrimaryColor)}
            <p style='color: {MutedColor}; font-size: 13px; line-height: 1.5; margin: 16px 0 0 0;'>
                Liên kết này sẽ hết hạn sau <strong>3 giờ</strong>.
            </p>";

        return WrapInBaseLayout(content, "Đặt lại mật khẩu");
    }

    public string AdminConfirmEmail(string displayName, string confirmationUrl)
    {
        var content = $@"
            <h2 style='color: {PrimaryColor}; margin: 0 0 16px 0; font-size: 22px;'>
                Xác nhận địa chỉ Email
            </h2>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Xin chào <strong>{EscapeHtml(displayName)}</strong>,
            </p>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Quản trị viên đã yêu cầu bạn xác nhận địa chỉ email. Vui lòng nhấn vào nút bên dưới:
            </p>
            {RenderButton("Xác nhận Email", confirmationUrl, PrimaryColor)}
            <p style='color: {MutedColor}; font-size: 13px; line-height: 1.5; margin: 16px 0 0 0;'>
                Liên kết này sẽ hết hạn sau <strong>2 ngày</strong>.
            </p>";

        return WrapInBaseLayout(content, "Xác nhận Email");
    }

    public string AdminSetPassword(string displayName)
    {
        var content = $@"
            <h2 style='color: {WarningColor}; margin: 0 0 16px 0; font-size: 22px;'>
                Mật khẩu đã được cập nhật
            </h2>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Xin chào <strong>{EscapeHtml(displayName)}</strong>,
            </p>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Quản trị viên đã cập nhật mật khẩu cho tài khoản của bạn. Vui lòng liên hệ quản trị viên để nhận mật khẩu mới và đăng nhập lại.
            </p>
            {RenderInfoBox(@"
                <strong>⚠️ Lưu ý:</strong> Tất cả phiên đăng nhập hiện tại đã bị vô hiệu hóa. Bạn cần đăng nhập lại bằng mật khẩu mới.
            ", WarningColor)}";

        return WrapInBaseLayout(content, "Mật khẩu đã cập nhật");
    }

    public string AccountLocked(string displayName, string lockoutEnd)
    {
        var content = $@"
            <h2 style='color: {WarningColor}; margin: 0 0 16px 0; font-size: 22px;'>
                Tài khoản đã bị khóa 🔒
            </h2>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Xin chào <strong>{EscapeHtml(displayName)}</strong>,
            </p>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Tài khoản của bạn đã bị khóa bởi quản trị viên.
            </p>
            {RenderInfoBox($@"
                <strong>🕐 Thời gian mở khóa:</strong> {EscapeHtml(lockoutEnd)}<br/>
                Nếu bạn cho rằng đây là nhầm lẫn, vui lòng liên hệ bộ phận hỗ trợ.
            ", WarningColor)}";

        return WrapInBaseLayout(content, "Tài khoản bị khóa");
    }

    public string AccountUnlocked(string displayName)
    {
        var content = $@"
            <h2 style='color: {SuccessColor}; margin: 0 0 16px 0; font-size: 22px;'>
                Tài khoản đã được mở khóa 🔓
            </h2>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Xin chào <strong>{EscapeHtml(displayName)}</strong>,
            </p>
            <p style='color: {TextColor}; font-size: 15px; line-height: 1.6; margin: 0 0 12px 0;'>
                Tài khoản của bạn đã được mở khóa thành công. Bạn có thể đăng nhập trở lại bình thường.
            </p>
            <p style='color: {MutedColor}; font-size: 13px; line-height: 1.5; margin: 16px 0 0 0;'>
                Nếu bạn gặp khó khăn khi đăng nhập, vui lòng sử dụng chức năng &quot;Quên mật khẩu&quot; hoặc liên hệ bộ phận hỗ trợ.
            </p>";

        return WrapInBaseLayout(content, "Tài khoản đã mở khóa");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Base Layout & Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static string WrapInBaseLayout(string bodyContent, string previewText)
    {
        return $@"<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <meta http-equiv='X-UA-Compatible' content='IE=edge' />
    <title>{EscapeHtml(previewText)}</title>
    <!--[if mso]>
    <noscript>
        <xml>
            <o:OfficeDocumentSettings>
                <o:PixelsPerInch>96</o:PixelsPerInch>
            </o:OfficeDocumentSettings>
        </xml>
    </noscript>
    <![endif]-->
</head>
<body style='margin: 0; padding: 0; background-color: #F3F4F6; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif;'>
    <!-- Preview text (hidden, shown in inbox list) -->
    <div style='display: none; max-height: 0; overflow: hidden;'>
        {EscapeHtml(previewText)}
    </div>

    <!-- Outer wrapper -->
    <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #F3F4F6;'>
        <tr>
            <td align='center' style='padding: 32px 16px;'>
                <!-- Inner card -->
                <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='max-width: 560px; background-color: #FFFFFF; border-radius: 12px; box-shadow: 0 1px 3px rgba(0,0,0,0.1);'>
                    <!-- Header -->
                    <tr>
                        <td style='padding: 28px 32px 0 32px;'>
                            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td style='font-size: 20px; font-weight: 700; color: {PrimaryColor};'>
                                        {AppName}
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    <!-- Divider -->
                    <tr>
                        <td style='padding: 16px 32px 0 32px;'>
                            <hr style='border: none; border-top: 1px solid #E5E7EB; margin: 0;' />
                        </td>
                    </tr>
                    <!-- Body -->
                    <tr>
                        <td style='padding: 24px 32px;'>
                            {bodyContent}
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style='padding: 0 32px 28px 32px;'>
                            <hr style='border: none; border-top: 1px solid #E5E7EB; margin: 0 0 16px 0;' />
                            <p style='color: {MutedColor}; font-size: 12px; line-height: 1.5; margin: 0; text-align: center;'>
                                © {DateTime.UtcNow.Year} {AppName}. Tất cả quyền được bảo lưu.<br/>
                                Email này được gửi tự động, vui lòng không trả lời trực tiếp.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private static string RenderButton(string text, string url, string color)
    {
        return $@"
            <table role='presentation' cellpadding='0' cellspacing='0' border='0' style='margin: 20px 0;'>
                <tr>
                    <td align='center' style='border-radius: 8px; background-color: {color};'>
                        <a href='{EscapeHtml(url)}' target='_blank'
                           style='display: inline-block; padding: 14px 32px; color: #FFFFFF; font-size: 15px; font-weight: 600; text-decoration: none; border-radius: 8px; background-color: {color};'>
                            {EscapeHtml(text)}
                        </a>
                    </td>
                </tr>
            </table>
            <p style='color: {MutedColor}; font-size: 12px; line-height: 1.5; margin: 0;'>
                Nếu nút không hoạt động, hãy sao chép và dán liên kết sau vào trình duyệt:<br/>
                <a href='{EscapeHtml(url)}' style='color: {PrimaryColor}; word-break: break-all;'>{EscapeHtml(url)}</a>
            </p>";
    }

    private static string RenderInfoBox(string html, string borderColor)
    {
        return $@"
            <table role='presentation' cellpadding='0' cellspacing='0' border='0' width='100%' style='margin: 16px 0;'>
                <tr>
                    <td style='padding: 14px 16px; background-color: #FEF2F2; border-left: 4px solid {borderColor}; border-radius: 4px;'>
                        <p style='color: {TextColor}; font-size: 13px; line-height: 1.6; margin: 0;'>
                            {html}
                        </p>
                    </td>
                </tr>
            </table>";
    }

    private static string EscapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
