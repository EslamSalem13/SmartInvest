using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IMeasurementService
{
    Task<IReadOnlyList<MeasurementDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MeasurementDto>> GetApplicableForSubProgramAsync(int subProgramId, CancellationToken cancellationToken = default);

    Task<MeasurementDto> CreateAsync(CreateMeasurementDto dto, CancellationToken cancellationToken = default);

    Task<MeasurementDto> UpdateAsync(int id, UpdateMeasurementDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubProjectMeasurementValueDto>> GetValuesForSubProjectAsync(int subProjectId, CancellationToken cancellationToken = default);

    Task SetValuesForSubProjectAsync(int subProjectId, SetSubProjectMeasurementValuesDto dto, CancellationToken cancellationToken = default);
}
