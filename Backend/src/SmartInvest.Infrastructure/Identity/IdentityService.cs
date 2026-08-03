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
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly JwtSettings _jwtSettings;
    private readonly ICurrentUserService _currentUser;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<JwtSettings> jwtOptions,
        ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtSettings = jwtOptions.Value;
        _currentUser = currentUser;
    }

    /// <summary>الاسم المعروض للدور بالعربية — يقع على اسم الدور نفسه لو غير محدَّد.</summary>
    private async Task<string> GetRoleDisplayNameAsync(string role)
    {
        if (string.IsNullOrEmpty(role))
        {
            return string.Empty;
        }

        var appRole = await _roleManager.FindByNameAsync(role);

        return string.IsNullOrWhiteSpace(appRole?.DisplayName) ? role : appRole!.DisplayName;
    }

    /// <summary>صلاحيات الدور — السوبر أدمن يحصل على كل الصلاحيات.</summary>
    private async Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(string role)
    {
        if (role == Roles.SuperAdmin)
        {
            return Permissions.All.ToList();
        }

        if (string.IsNullOrEmpty(role))
        {
            return [];
        }

        var appRole = await _roleManager.FindByNameAsync(role);
        if (appRole == null)
        {
            return [];
        }

        var claims = await _roleManager.GetClaimsAsync(appRole);

        return claims
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .ToList();
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

        var permissions = await GetPermissionsForRoleAsync(role);

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes);
        var token = GenerateJwtToken(user, role, permissions, expiresAt);

        var result = new AuthResultDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = role,
            RoleDisplayName = await GetRoleDisplayNameAsync(role),
            Permissions = permissions.ToList(),
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

    public async Task<UserDto> CreateEmployeeAsync(CreateEmployeeDto dto, CancellationToken cancellationToken = default)
    {
        // الأدوار ديناميكية: نتحقق فقط أن الدور موجود فعلًا.
        if (string.IsNullOrWhiteSpace(dto.Role) || !await _roleManager.RoleExistsAsync(dto.Role))
        {
            throw new BusinessRuleException("الدور الوظيفي غير صحيح");
        }

        if (dto.Role == Roles.SuperAdmin && _currentUser.Role != Roles.SuperAdmin)
        {
            throw new ForbiddenAccessException("السوبر أدمن فقط يمكنه إنشاء حساب سوبر أدمن");
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
            RoleDisplayName = await GetRoleDisplayNameAsync(dto.Role),
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
                RoleDisplayName = await GetRoleDisplayNameAsync(roles.FirstOrDefault() ?? string.Empty),
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
        await _userManager.UpdateAsync(user);
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

    private string GenerateJwtToken(ApplicationUser user, string role, IReadOnlyList<string> permissions, DateTime expiresAt)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(permissions.Select(p => new Claim(Permissions.ClaimType, p)));

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