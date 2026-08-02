using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;

namespace SmartInvest.Application.Services.Import;

public class MeasurementResolutionService : IMeasurementResolutionService
{
    private readonly IMeasurementService _measurementService;
    private readonly ILookupService _lookupService;

    public MeasurementResolutionService(IMeasurementService measurementService, ILookupService lookupService)
    {
        _measurementService = measurementService;
        _lookupService = lookupService;
    }

    public async Task RecordMeasurementsAsync(int subProjectId, int subProgramId, List<ExtractedMeasurementDto> measurements, CancellationToken cancellationToken = default)
    {
        if (measurements.Count == 0)
        {
            return;
        }

        var values = new List<SetMeasurementValueDto>();
        foreach (var measurement in measurements)
        {
            var name = measurement.Name.Trim();
            var unitName = measurement.Unit.Trim();
            if (name.Length == 0 || unitName.Length == 0)
            {
                continue;
            }

            var unitId = await EnsureUnitAsync(unitName, cancellationToken);
            var measurementId = await EnsureMeasurementAsync(name, subProgramId, unitId, cancellationToken);

            values.Add(new SetMeasurementValueDto { MeasurementId = measurementId, UnitId = unitId, Value = measurement.Value });
        }

        if (values.Count > 0)
        {
            await _measurementService.SetValuesForSubProjectAsync(subProjectId, new SetSubProjectMeasurementValuesDto { Values = values }, cancellationToken);
        }
    }

    private async Task<int> EnsureUnitAsync(string unitName, CancellationToken cancellationToken)
    {
        var units = await _lookupService.GetUnitsAsync(cancellationToken);
        var existing = units.FirstOrDefault(u => u.Name.Trim() == unitName);
        if (existing != null)
        {
            return existing.Id;
        }

        var created = await _lookupService.CreateUnitAsync(new CreateNamedLookupDto { Name = unitName }, cancellationToken);
        return created.Id;
    }

    private async Task<int> EnsureMeasurementAsync(string measurementName, int subProgramId, int unitId, CancellationToken cancellationToken)
    {
        var all = await _measurementService.GetAllAsync(cancellationToken);
        var existing = all.FirstOrDefault(m => m.Name.Trim() == measurementName);

        if (existing == null)
        {
            var created = await _measurementService.CreateAsync(new CreateMeasurementDto
            {
                Name = measurementName,
                SubProgramIds = new List<int> { subProgramId },
                UnitIds = new List<int> { unitId },
            }, cancellationToken);
            return created.Id;
        }

        var needsSubProgram = !existing.SubProgramIds.Contains(subProgramId);
        var needsUnit = !existing.UnitIds.Contains(unitId);
        if (needsSubProgram || needsUnit)
        {
            await _measurementService.UpdateAsync(existing.Id, new UpdateMeasurementDto
            {
                Name = existing.Name,
                SubProgramIds = needsSubProgram ? existing.SubProgramIds.Append(subProgramId).ToList() : existing.SubProgramIds,
                UnitIds = needsUnit ? existing.UnitIds.Append(unitId).ToList() : existing.UnitIds,
            }, cancellationToken);
        }

        return existing.Id;
    }
}
