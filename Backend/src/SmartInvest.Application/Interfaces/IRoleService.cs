using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IRoleService
{
    Task<IReadOnlyList<PermissionGroupDto>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleListItemDto>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<RoleDetailDto> GetRoleAsync(string id, CancellationToken cancellationToken = default);

    Task<RoleDetailDto> CreateRoleAsync(SaveRoleDto dto, CancellationToken cancellationToken = default);

    Task<RoleDetailDto> UpdateRoleAsync(string id, SaveRoleDto dto, CancellationToken cancellationToken = default);

    Task DeleteRoleAsync(string id, CancellationToken cancellationToken = default);
}
