using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FreeSql;
using Microsoft.AspNetCore.Identity;
using SchoolRecommender.Api.Data;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Options;

namespace SchoolRecommender.Api.Services;

public sealed class AuthService
{
    private static readonly Regex EmailRegex = new("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly ConcurrentDictionary<string, SessionUser> _sessions = new(StringComparer.Ordinal);
    private readonly IFreeSql _freeSql;
    private readonly SchoolServiceOptions _options;
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public AuthService(IFreeSql freeSql, SchoolServiceOptions options)
    {
        _freeSql = freeSql;
        _options = options;
    }

    public async Task<AuthResult> SendRegisterCodeAsync(SendRegisterCodeRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var now = DateTime.UtcNow;
        if (!EmailRegex.IsMatch(email))
        {
            return AuthResult.Fail("请输入有效的邮箱地址");
        }

        var existing = await _freeSql.Select<AppUser>()
            .Where(user => user.Email == email)
            .FirstAsync(cancellationToken);
        if (existing is not null)
        {
            return AuthResult.Fail("该邮箱已注册，请直接登录");
        }

        var latest = await _freeSql.Select<EmailVerificationCode>()
            .Where(item => item.Email == email && item.UsedAt == null)
            .OrderByDescending(item => item.CreatedAt)
            .FirstAsync(cancellationToken);
        if (latest is not null && now - latest.CreatedAt < TimeSpan.FromSeconds(60))
        {
            return AuthResult.Fail("验证码发送过于频繁，请稍后再试");
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var verificationCode = new EmailVerificationCode
        {
            Email = email,
            Code = code,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(10),
        };
        await _freeSql.Insert(verificationCode).ExecuteAffrowsAsync(cancellationToken);

        await SendVerificationEmailAsync(email, code, cancellationToken);
        return AuthResult.Sent();
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? email.Split('@')[0] : request.DisplayName.Trim();
        var validationError = ValidateCredentials(email, request.Password, displayName);
        if (validationError is not null)
        {
            return AuthResult.Fail(validationError);
        }

        var codeError = await ValidateVerificationCodeAsync(email, request.VerificationCode, cancellationToken);
        if (codeError is not null)
        {
            return AuthResult.Fail(codeError);
        }

        var existing = await _freeSql.Select<AppUser>()
            .Where(user => user.Email == email)
            .FirstAsync(cancellationToken);
        if (existing is not null)
        {
            return AuthResult.Fail("该邮箱已注册，请直接登录");
        }

        var user = new AppUser
        {
            Email = email,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        user.Id = (long)await _freeSql.Insert(user).ExecuteIdentityAsync(cancellationToken);

        await _freeSql.Update<EmailVerificationCode>()
            .Set(item => item.UsedAt, DateTime.UtcNow)
            .Where(item => item.Email == email && item.Code == request.VerificationCode.Trim() && item.UsedAt == null)
            .ExecuteAffrowsAsync(cancellationToken);

        return AuthResult.Ok(CreateSession(user));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (!EmailRegex.IsMatch(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthResult.Fail("邮箱或密码不正确");
        }

        var user = await _freeSql.Select<AppUser>()
            .Where(item => item.Email == email)
            .FirstAsync(cancellationToken);
        if (user is null)
        {
            return AuthResult.Fail("邮箱或密码不正确");
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return AuthResult.Fail("邮箱或密码不正确");
        }

        return AuthResult.Ok(CreateSession(user));
    }

    public SessionUser? GetUserFromRequest(HttpContext httpContext)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorization[prefix.Length..].Trim();
        return _sessions.TryGetValue(token, out var user) ? user : null;
    }

    private AuthPayload CreateSession(AppUser user)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var sessionUser = new SessionUser(user.Id, user.Email, user.DisplayName);
        _sessions[token] = sessionUser;
        return new AuthPayload(token, sessionUser);
    }

        private async Task<string?> ValidateVerificationCodeAsync(string email, string code, CancellationToken cancellationToken)
        {
                var normalizedCode = code.Trim();
                if (!Regex.IsMatch(normalizedCode, "^\\d{6}$"))
                {
                        return "请输入 6 位邮箱验证码";
                }

                var verificationCode = await _freeSql.Select<EmailVerificationCode>()
                        .Where(item => item.Email == email && item.Code == normalizedCode && item.UsedAt == null)
                        .OrderByDescending(item => item.CreatedAt)
                        .FirstAsync(cancellationToken);

                if (verificationCode is null)
                {
                        return "邮箱验证码不正确";
                }

                return verificationCode.ExpiresAt < DateTime.UtcNow ? "邮箱验证码已过期，请重新获取" : null;
        }

        private async Task SendVerificationEmailAsync(string email, string code, CancellationToken cancellationToken)
        {
                if (string.IsNullOrWhiteSpace(_options.SmtpHost) || string.IsNullOrWhiteSpace(_options.SmtpFromEmail))
                {
                        throw new InvalidOperationException("未配置 SMTP 邮件服务，无法发送验证码");
                }

                using var message = new MailMessage
                {
                        From = new MailAddress(_options.SmtpFromEmail, _options.SmtpFromName),
                        Subject = "学校推荐系统注册验证码",
                        Body = BuildVerificationEmailHtml(code),
                        IsBodyHtml = true,
                };
                message.To.Add(email);

                using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
                {
                        EnableSsl = _options.SmtpEnableSsl,
                };
                if (!string.IsNullOrWhiteSpace(_options.SmtpUserName))
                {
                        client.Credentials = new NetworkCredential(_options.SmtpUserName, _options.SmtpPassword);
                }

                using var registration = cancellationToken.Register(client.SendAsyncCancel);
                await client.SendMailAsync(message, cancellationToken);
        }

        private static string BuildVerificationEmailHtml(string code) => $$"""
                <!doctype html>
                <html lang="zh-CN">
                <body style="margin:0;background:#f7efe4;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI','Microsoft YaHei',sans-serif;color:#2f261f;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:linear-gradient(135deg,#fff8ef 0%,#f2eadc 100%);padding:36px 14px;">
                        <tr>
                            <td align="center">
                                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:520px;background:#fffdf8;border:1px solid #ead9c7;border-radius:28px;overflow:hidden;box-shadow:0 24px 60px rgba(112,76,45,.16);">
                                    <tr>
                                        <td style="padding:30px 30px 18px;background:linear-gradient(135deg,#e07a3f 0%,#f0a66c 100%);color:white;">
                                            <div style="font-size:12px;letter-spacing:.24em;text-transform:uppercase;opacity:.86;">Geo School Match</div>
                                            <h1 style="margin:10px 0 0;font-size:24px;line-height:1.35;font-weight:700;">邮箱验证码</h1>
                                            <p style="margin:10px 0 0;font-size:14px;line-height:1.8;opacity:.9;">用于完成学校推荐系统账号注册</p>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style="padding:34px 30px 28px;text-align:center;">
                                            <p style="margin:0 0 18px;font-size:15px;line-height:1.8;color:#6f6257;">请在注册页面输入以下 6 位验证码：</p>
                                            <div style="display:inline-block;padding:18px 26px;border-radius:22px;background:#fff4e7;border:1px solid #f0c7a6;color:#b85e2c;font-size:36px;line-height:1;font-weight:800;letter-spacing:.22em;box-shadow:inset 0 1px 0 rgba(255,255,255,.8);">{{code}}</div>
                                            <p style="margin:22px 0 0;font-size:13px;line-height:1.8;color:#8d7c6e;">验证码 10 分钟内有效。若非本人操作，请忽略本邮件。</p>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style="padding:18px 30px 28px;background:#fbf6ee;color:#9a897b;font-size:12px;line-height:1.8;text-align:center;">
                                            学校推荐系统 · AI 多源择校分析
                                        </td>
                                    </tr>
                                </table>
                            </td>
                        </tr>
                    </table>
                </body>
                </html>
                """;

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string? ValidateCredentials(string email, string password, string displayName)
    {
        if (!EmailRegex.IsMatch(email))
        {
            return "请输入有效的邮箱地址";
        }

        if (password.Length < 8)
        {
            return "密码至少需要 8 位";
        }

        if (displayName.Length is < 2 or > 32)
        {
            return "昵称长度需要在 2 到 32 个字符之间";
        }

        return null;
    }
}

public sealed record AuthResult(bool Success, AuthPayload? Payload, string? Message)
{
    public static AuthResult Ok(AuthPayload payload) => new(true, payload, null);

    public static AuthResult Sent() => new(true, null, null);

    public static AuthResult Fail(string message) => new(false, null, message);
}
