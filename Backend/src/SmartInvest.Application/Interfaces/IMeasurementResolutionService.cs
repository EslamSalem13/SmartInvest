using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IMeasurementResolutionService
{
    Task RecordMeasurementsAsync(int subProjectId, int subProgramId, List<ExtractedMeasurementDto> measurements, CancellationToken cancellationToken = default);
}
