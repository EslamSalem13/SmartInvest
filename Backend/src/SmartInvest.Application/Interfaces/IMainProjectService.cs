using SmartInvest.Application.DTOs;
using SmartInvest.Application.DTOs.Common;

namespace SmartInvest.Application.Interfaces;

public interface IMainProjectService
{
    Task<PagedResultDto<MainProjectListItemDto>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<MainProjectDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<MainProjectDetailDto> CreateAsync(CreateMainProjectDto dto, CancellationToken cancellationToken = default);

    Task<MainProjectDetailDto> UpdateAsync(int id, UpdateMainProjectDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}