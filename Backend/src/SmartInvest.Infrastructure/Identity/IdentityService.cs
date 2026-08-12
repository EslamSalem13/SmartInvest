using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartInvest.Application.Common;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SmartInvest.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtSettings _jwtSettings;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _emailService;
    private readonly EmailOptions _emailOptions;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        IOptions<JwtSettings> jwtOptions,
        ICurrentUserService currentUser,
        IEmailService emailService,
        IOptions<EmailOptions> emailOptions)
    {
        _userManager = userManager;
        _jwtSettings = jwtOptions.Value;
        _currentUser = currentUser;
        _emailService = emailService;
        _emailOptions = emailOptions.Value;
    }

    public async Task<AuthResultDto> LoginAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByNameAsync(usernameOrEmail);
        if (user == null)
        {
            user = await _userManager.FindByEmailAsync(usernameOrEmail);
        }

        if (user == null)
        {
            throw new BusinessRuleException("بيانات الدخول غير صحيحة");
        }

        if (!user.IsActive)
        {
            throw new BusinessRuleException("هذا الحساب معطّل، برجاء التواصل مع مدير التخطيط");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            throw new BusinessRuleException("بيانات الدخول غير صحيحة");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes);
        var token = GenerateJwtToken(user, role, expiresAt);

        var result = new AuthResultDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = role,
            HasAvatar = user.AvatarContent is { Length: > 0 }
        };

        return result;
    }

    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("المستخدم غير موجود");
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(" - ", result.Errors.Select(e => e.Description));
            throw new BusinessRuleException(errors);
        }
    }

    public async Task<ProfileDto> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("المستخدم غير موجود");
        }

        return await MapProfileAsync(user);
    }

    public async Task<ProfileDto> UpdateProfileAsync(string userId, UpdateProfileDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("المستخدم غير موجود");
        }

        var fullName = dto.FullName.Trim();
        var email = dto.Email.Trim();
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            throw new BusinessRuleException("الاسم والبريد الإلكتروني مطلوبان");
        }

        user.FullName = fullName;
        user.Email = email;
        user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(" - ", result.Errors.Select(e => e.Description));
            throw new BusinessRuleException(errors);
        }

        return await MapProfileAsync(user);
    }

    public async Task<UserDto> CreateEmployeeAsync(CreateEmployeeDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Role != Roles.PlanningEmployee && dto.Role != Roles.PlanningManager)
        {
            throw new BusinessRuleException("الدور الوظيفي غير صحيح");
        }

        if (dto.Role == Roles.PlanningManager && _currentUser.Role != Roles.SuperAdmin)
        {
            throw new ForbiddenAccessException("السوبر أدمن فقط يمكنه إنشاء حساب مدير تخطيط");
        }

        var user = new ApplicationUser
        {
            UserName = dto.UserName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            FullName = dto.FullName,
            IsActive = true
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(" - ", createResult.Errors.Select(e => e.Description));
            throw new BusinessRuleException(errors);
        }

        await _userManager.AddToRoleAsync(user, dto.Role);

        var userDto = new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Role = dto.Role,
            IsActive = user.IsActive,
            HasAvatar = false,
            CreatedAt = user.CreatedAt
        };

        return userDto;
    }

    public async Task ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("المستخدم غير موجود");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(" - ", result.Errors.Select(e => e.Description));
            throw new BusinessRuleException(errors);
        }
    }

    public async Task SetActiveStatusAsync(string userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("المستخدم غير موجود");
        }

        user.IsActive = isActive;
        await _userManager.UpdateAsync(user);
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users.ToListAsync(cancellationToken);
        var result = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            var userDto = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Role = roles.FirstOrDefault() ?? string.Empty,
                IsActive = user.IsActive,
                HasAvatar = user.AvatarContent is { Length: > 0 },
                CreatedAt = user.CreatedAt
            };

            result.Add(userDto);
        }

        return result;
    }

    public async Task UpdateAvatarAsync(string userId, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("المستخدم غير موجود");
        }

        user.AvatarContent = content;
        user.AvatarContentType = contentType;
        var result = await _userManager.UpdateAsync(user);
        EnsureSucceeded(result);
    }

    public async Task DeleteAvatarAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("المستخدم غير موجود");
        }

        user.AvatarContent = null;
        user.AvatarContentType = null;
        var result = await _userManager.UpdateAsync(user);
        EnsureSucceeded(result);
    }

    public async Task<AvatarDto?> GetAvatarAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user?.AvatarContent is not { Length: > 0 } || string.IsNullOrEmpty(user.AvatarContentType))
        {
            return null;
        }

        return new AvatarDto { Content = user.AvatarContent, ContentType = user.AvatarContentType };
    }

    public async Task SendPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var link = BuildFrontendLink("/reset-password", new Dictionary<string, string>
        {
            ["email"] = user.Email,
            ["token"] = token
        });
        var body = BuildEmailTemplate(
            "إعادة تعيين كلمة المرور",
            "طلب آمن لاستعادة حسابك",
            user.FullName,
            "تلقينا طلبًا لإعادة تعيين كلمة مرور حسابك في SmartInvest. استخدم الزر التالي لاختيار كلمة مرور جديدة.",
            "اختيار كلمة مرور جديدة",
            link,
            "هذا الرابط صالح لفترة محدودة. إذا لم تطلب تغيير كلمة المرور، تجاهل الرسالة ولن تتغير بيانات حسابك.");
        await _emailService.SendAsync(user.Email, "إعادة تعيين كلمة المرور - SmartInvest", body, cancellationToken);
    }

    public async Task ResetPasswordByEmailAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user == null || !user.IsActive)
        {
            throw new BusinessRuleException("رابط إعادة تعيين كلمة المرور غير صالح أو انتهت صلاحيته");
        }

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var passwordErrors = result.Errors
                .Where(e => e.Code.StartsWith("Password", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Description)
                .ToList();
            if (passwordErrors.Count > 0)
            {
                throw new BusinessRuleException(string.Join(" - ", passwordErrors));
            }

            throw new BusinessRuleException("رابط إعادة تعيين كلمة المرور غير صالح أو انتهت صلاحيته");
        }
    }

    private async Task<ProfileDto> MapProfileAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new ProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Role = roles.FirstOrDefault() ?? string.Empty,
            IsActive = user.IsActive,
            HasAvatar = user.AvatarContent is { Length: > 0 },
            CreatedAt = user.CreatedAt
        };
    }

    private string BuildFrontendLink(string path, IReadOnlyDictionary<string, string> query)
    {
        var baseUrl = _emailOptions.FrontendBaseUrl.TrimEnd('/');
        var queryString = string.Join("&", query.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
        return $"{baseUrl}{path}?{queryString}";
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(" - ", result.Errors.Select(e => e.Description));
        throw new BusinessRuleException(errors);
    }

    private static string BuildEmailTemplate(
        string title,
        string subtitle,
        string recipientName,
        string description,
        string actionText,
        string actionUrl,
        string securityNote)
    {
        var safeTitle = System.Net.WebUtility.HtmlEncode(title);
        var safeSubtitle = System.Net.WebUtility.HtmlEncode(subtitle);
        var safeName = System.Net.WebUtility.HtmlEncode(recipientName);
        var safeDescription = System.Net.WebUtility.HtmlEncode(description);
        var safeActionText = System.Net.WebUtility.HtmlEncode(actionText);
        var safeActionUrl = System.Net.WebUtility.HtmlEncode(actionUrl);
        var safeSecurityNote = System.Net.WebUtility.HtmlEncode(securityNote);

        return $$"""
            <!doctype html>
            <html lang="ar" dir="rtl">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{safeTitle}}</title>
            </head>
            <body style="margin:0;padding:0;background-color:#eef2ee;color:#14201a;font-family:Tahoma,Arial,sans-serif;direction:rtl;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">{{safeSubtitle}}</div>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="width:100%;background-color:#eef2ee;">
                <tr>
                  <td align="center" style="padding:34px 14px;">
                    <table role="presentation" width="620" cellspacing="0" cellpadding="0" border="0" style="width:100%;max-width:620px;background:#ffffff;border:1px solid #e3e9e2;border-radius:22px;overflow:hidden;box-shadow:0 12px 34px rgba(12,59,42,.10);">
                      <tr>
                        <td style="padding:30px 34px;background-color:#0c3b2a;background-image:linear-gradient(135deg,#0c3b2a,#15603f);text-align:right;">
                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                            <tr>
                              <td style="vertical-align:middle;text-align:right;">
                                <div style="color:#ffffff;font-size:24px;line-height:1.3;font-weight:800;letter-spacing:-.4px;">Smart<span style="color:#e7ce8c;">Invest</span></div>
                                <div style="margin-top:5px;color:#bcd2c3;font-size:12px;font-weight:700;">منصة إدارة الخطة الاستثمارية</div>
                              </td>
                              <td width="58" style="width:58px;vertical-align:middle;text-align:left;">
                                <div style="width:48px;height:48px;line-height:48px;border-radius:15px;background:#e7ce8c;color:#2a2107;text-align:center;font-size:17px;font-weight:900;">SI</div>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:38px 34px 30px;text-align:right;">
                          <div style="display:inline-block;margin-bottom:14px;padding:6px 12px;border-radius:999px;background:#fbf3df;color:#8a6512;font-size:12px;font-weight:800;">{{safeSubtitle}}</div>
                          <h1 style="margin:0 0 18px;color:#0c3b2a;font-size:27px;line-height:1.45;font-weight:800;">{{safeTitle}}</h1>
                          <p style="margin:0 0 12px;color:#2b3a32;font-size:16px;line-height:1.9;">مرحبًا <strong>{{safeName}}</strong>،</p>
                          <p style="margin:0;color:#53645a;font-size:15px;line-height:2;">{{safeDescription}}</p>

                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:28px 0;">
                            <tr>
                              <td align="center">
                                <a href="{{safeActionUrl}}" style="display:inline-block;min-width:230px;padding:14px 24px;border-radius:11px;background:#15603f;color:#ffffff;text-decoration:none;text-align:center;font-size:15px;line-height:1.4;font-weight:800;box-shadow:0 8px 20px rgba(21,96,63,.22);">{{safeActionText}}</a>
                              </td>
                            </tr>
                          </table>

                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:0 0 24px;background:#f6f9f6;border:1px solid #e3e9e2;border-radius:12px;">
                            <tr>
                              <td width="38" style="width:38px;padding:16px 14px 16px 0;color:#c79a3a;font-size:20px;vertical-align:top;text-align:center;">&#128274;</td>
                              <td style="padding:15px 0 15px 16px;color:#53645a;font-size:13px;line-height:1.8;">{{safeSecurityNote}}</td>
                            </tr>
                          </table>

                          <p style="margin:0 0 7px;color:#8a9a8f;font-size:11px;line-height:1.7;">إذا لم يعمل الزر، انسخ الرابط التالي والصقه في المتصفح:</p>
                          <p dir="ltr" style="margin:0;padding:11px 12px;border-radius:9px;background:#f1f8f3;color:#15603f;font-family:Arial,sans-serif;font-size:11px;line-height:1.6;text-align:left;word-break:break-all;">{{safeActionUrl}}</p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:20px 34px;background:#f6f9f6;border-top:1px solid #e3e9e2;text-align:center;color:#718077;font-size:11px;line-height:1.8;">
                          رسالة آلية من نظام SmartInvest — محافظة المنوفية<br>
                          برجاء عدم مشاركة هذا الرابط مع أي شخص.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private string GenerateJwtToken(ApplicationUser user, string role, DateTime expiresAt)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(token);

        return tokenString;
    }
}
