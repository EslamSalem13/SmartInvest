using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.Infrastructure.Identity;

/// <summary>إدارة الأدوار الديناميكية وصلاحياتها (السوبر أدمن فقط).</summary>
public class RoleService : IRoleService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public RoleService(RoleManager<ApplicationRole> roleManager, UserManager<ApplicationUser> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public Task<IReadOnlyList<PermissionGroupDto>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PermissionGroupDto> catalog = Permissions.Catalog
            .Select(g => new PermissionGroupDto
            {
                Key = g.Key,
                Label = g.Label,
                Items = g.Items.Select(i => new PermissionItemDto { Key = i.Key, Label = i.Label }).ToList()
            })
            .ToList();

        return Task.FromResult(catalog);
    }

    public async Task<IReadOnlyList<RoleListItemDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _roleManager.Roles.OrderBy(r => r.CreatedAt).ToListAsync(cancellationToken);
        var result = new List<RoleListItemDto>();

        foreach (var role in roles)
        {
            result.Add(await ToListItemAsync(role));
        }

        return result;
    }

    public async Task<RoleDetailDto> GetRoleAsync(string id, CancellationToken cancellationToken = default)
    {
        var role = await FindRoleAsync(id);
        var permissions = await GetPermissionsAsync(role);
        var item = await ToListItemAsync(role, permissions.Count);

        return ToDetail(item, permissions);
    }

    public async Task<RoleDetailDto> CreateRoleAsync(SaveRoleDto dto, CancellationToken cancellationToken = default)
    {
        var displayName = (dto.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BusinessRuleException("اسم الدور مطلوب");
        }

        var permissions = NormalizePermissions(dto.Permissions);

        if (await _roleManager.Roles.AnyAsync(r => r.DisplayName == displayName, cancellationToken))
        {
            throw new BusinessRuleException("يوجد دور آخر بنفس الاسم");
        }

        var role = new ApplicationRole(GenerateRoleName())
        {
            DisplayName = displayName,
            IsSystem = false
        };

        var created = await _roleManager.CreateAsync(role);
        if (!created.Succeeded)
        {
            throw new BusinessRuleException(string.Join(" - ", created.Errors.Select(e => e.Description)));
        }

        foreach (var permission in permissions)
        {
            await _roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
        }

        var item = await ToListItemAsync(role, permissions.Count);

        return ToDetail(item, permissions);
    }

    public async Task<RoleDetailDto> UpdateRoleAsync(string id, SaveRoleDto dto, CancellationToken cancellationToken = default)
    {
        var role = await FindRoleAsync(id);

        if (role.IsSystem)
        {
            throw new BusinessRuleException("لا يمكن تعديل صلاحيات دور النظام");
        }

        var displayName = (dto.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BusinessRuleException("اسم الدور مطلوب");
        }

        var permissions = NormalizePermissions(dto.Permissions);

        if (await _roleManager.Roles.AnyAsync(r => r.DisplayName == displayName && r.Id != role.Id, cancellationToken))
        {
            throw new BusinessRuleException("يوجد دور آخر بنفس الاسم");
        }

        role.DisplayName = displayName;
        var updated = await _roleManager.UpdateAsync(role);
        if (!updated.Succeeded)
        {
            throw new BusinessRuleException(string.Join(" - ", updated.Errors.Select(e => e.Description)));
        }

        // استبدال الصلاحيات القديمة بالجديدة
        var existing = await _roleManager.GetClaimsAsync(role);
        foreach (var claim in existing.Where(c => c.Type == Permissions.ClaimType))
        {
            await _roleManager.RemoveClaimAsync(role, claim);
        }

        foreach (var permission in permissions)
        {
            await _roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
        }

        var item = await ToListItemAsync(role, permissions.Count);

        return ToDetail(item, permissions);
    }

    public async Task DeleteRoleAsync(string id, CancellationToken cancellationToken = default)
    {
        var role = await FindRoleAsync(id);

        if (role.IsSystem)
        {
            throw new BusinessRuleException("لا يمكن حذف دور النظام");
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Count > 0)
        {
            throw new BusinessRuleException($"لا يمكن حذف الدور لوجود {usersInRole.Count} مستخدم مرتبط به");
        }

        var deleted = await _roleManager.DeleteAsync(role);
        if (!deleted.Succeeded)
        {
            throw new BusinessRuleException(string.Join(" - ", deleted.Errors.Select(e => e.Description)));
        }
    }

    // ===== helpers =====

    private async Task<ApplicationRole> FindRoleAsync(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null)
        {
            throw new NotFoundException("الدور غير موجود");
        }

        return role;
    }

    private async Task<List<string>> GetPermissionsAsync(ApplicationRole role)
    {
        if (role.Name == Roles.SuperAdmin)
        {
            return Permissions.All.ToList();
        }

        var claims = await _roleManager.GetClaimsAsync(role);

        return claims
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .ToList();
    }

    private async Task<RoleListItemDto> ToListItemAsync(ApplicationRole role, int? permissionCount = null)
    {
        var users = await _userManager.GetUsersInRoleAsync(role.Name!);

        return new RoleListItemDto
        {
            Id = role.Id,
            Name = role.Name ?? string.Empty,
            DisplayName = role.DisplayName,
            IsSystem = role.IsSystem,
            UserCount = users.Count,
            PermissionCount = permissionCount ?? (await GetPermissionsAsync(role)).Count,
            CreatedAt = role.CreatedAt
        };
    }

    private static RoleDetailDto ToDetail(RoleListItemDto item, List<string> permissions) => new()
    {
        Id = item.Id,
        Name = item.Name,
        DisplayName = item.DisplayName,
        IsSystem = item.IsSystem,
        UserCount = item.UserCount,
        PermissionCount = permissions.Count,
        CreatedAt = item.CreatedAt,
        Permissions = permissions
    };

    private static List<string> NormalizePermissions(IEnumerable<string>? permissions)
    {
        var normalized = (permissions ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct()
            .ToList();

        var unknown = normalized.Where(p => !Permissions.All.Contains(p)).ToList();
        if (unknown.Count > 0)
        {
            throw new BusinessRuleException($"صلاحيات غير معروفة: {string.Join("، ", unknown)}");
        }

        return normalized;
    }

    /// <summary>اسم داخلي فريد للدور — الاسم المعروض بالعربية هو المهم للمستخدم.</summary>
    private static string GenerateRoleName() => $"role_{Guid.NewGuid():N}"[..16];
}
