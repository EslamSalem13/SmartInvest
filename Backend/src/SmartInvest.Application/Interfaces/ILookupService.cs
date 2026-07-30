using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface ILookupService
{
    Task<IReadOnlyList<LookupDto>> GetPrioritiesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetStatusesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetMainProgramsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubProgramLookupDto>> GetSubProgramsAsync(int? mainProgramId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetGovernoratesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MarkazLookupDto>> GetMarkazAsync(int? governorateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VillageLookupDto>> GetVillagesAsync(int? markazId, CancellationToken cancellationToken = default);

    Task<LookupDto> CreatePriorityAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdatePriorityAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeletePriorityAsync(int id, CancellationToken cancellationToken = default);

    Task<LookupDto> CreateStatusAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdateStatusAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeleteStatusAsync(int id, CancellationToken cancellationToken = default);

    Task<LookupDto> CreateMainProgramAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdateMainProgramAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeleteMainProgramAsync(int id, CancellationToken cancellationToken = default);

    Task<SubProgramLookupDto> CreateSubProgramAsync(CreateSubProgramDto dto, CancellationToken cancellationToken = default);
    Task<SubProgramLookupDto> UpdateSubProgramAsync(int id, UpdateSubProgramDto dto, CancellationToken cancellationToken = default);
    Task DeleteSubProgramAsync(int id, CancellationToken cancellationToken = default);

    Task<LookupDto> CreateGovernorateAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdateGovernorateAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeleteGovernorateAsync(int id, CancellationToken cancellationToken = default);

    Task<MarkazLookupDto> CreateMarkazAsync(CreateMarkazDto dto, CancellationToken cancellationToken = default);
    Task<MarkazLookupDto> UpdateMarkazAsync(int id, UpdateMarkazDto dto, CancellationToken cancellationToken = default);
    Task DeleteMarkazAsync(int id, CancellationToken cancellationToken = default);

    Task<VillageLookupDto> CreateVillageAsync(CreateVillageDto dto, CancellationToken cancellationToken = default);
    Task<VillageLookupDto> UpdateVillageAsync(int id, UpdateVillageDto dto, CancellationToken cancellationToken = default);
    Task DeleteVillageAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetComponentTypesAsync(CancellationToken cancellationToken = default);
    Task<LookupDto> CreateComponentTypeAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdateComponentTypeAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeleteComponentTypeAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetProjectLevelsAsync(CancellationToken cancellationToken = default);
    Task<LookupDto> CreateProjectLevelAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdateProjectLevelAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeleteProjectLevelAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetAccountingUnitsAsync(CancellationToken cancellationToken = default);
    Task<LookupDto> CreateAccountingUnitAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdateAccountingUnitAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeleteAccountingUnitAsync(int id, CancellationToken cancellationToken = default);
}