using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

/// <summary>
/// إدارة الأدوار الديناميكية. إنشاء/تعديل/حذف للسوبر أدمن فقط،
/// أما قراءة قائمة الأدوار فمتاحة لمن يدير المستخدمين (لملء قائمة إسناد الدور).
/// </summary>
[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    /// <summary>كتالوج الصلاحيات مُجمَّعًا حسب الصفحة — لبناء شجرة الاختيار في الواجهة.</summary>
    [HttpGet("permissions")]
    [HasPermission(Permissions.RolesManage)]
    public async Task<ActionResult<IReadOnlyList<PermissionGroupDto>>> GetPermissions(CancellationToken cancellationToken)
        => Ok(await _roleService.GetPermissionCatalogAsync(cancellationToken));

    [HttpGet]
    [HasPermission(Permissions.UsersManage)]
    public async Task<ActionResult<IReadOnlyList<RoleListItemDto>>> GetRoles(CancellationToken cancellationToken)
        => Ok(await _roleService.GetRolesAsync(cancellationToken));

    [HttpGet("{id}")]
    [HasPermission(Permissions.RolesManage)]
    public async Task<ActionResult<RoleDetailDto>> GetRole(string id, CancellationToken cancellationToken)
        => Ok(await _roleService.GetRoleAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<ActionResult<RoleDetailDto>> CreateRole(SaveRoleDto dto, CancellationToken cancellationToken)
        => Ok(await _roleService.CreateRoleAsync(dto, cancellationToken));

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<ActionResult<RoleDetailDto>> UpdateRole(string id, SaveRoleDto dto, CancellationToken cancellationToken)
        => Ok(await _roleService.UpdateRoleAsync(id, dto, cancellationToken));

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> DeleteRole(string id, CancellationToken cancellationToken)
    {
        await _roleService.DeleteRoleAsync(id, cancellationToken);
        return NoContent();
    }
}
