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

        // Fetch both lookup lists ONCE for this call (Finding 7) instead of once per triple.
        // Newly created units/measurements are appended to these in-memory lists as we go, so a
        // later triple in the same call can see and reuse what an earlier triple just created.
        var units = (await _lookupService.GetUnitsAsync(cancellationToken)).ToList();
        var allMeasurements = (await _measurementService.GetAllAsync(cancellationToken)).ToList();

        // A MeasurementId, once used for a triple in this call, is never reused for another
        // triple in the same call (Finding 1) — same-named-but-different-unit triples (e.g. the
        // flagship "عدد" × 3 truck types example) must resolve to distinct Measurement records.
        var claimedMeasurementIds = new HashSet<int>();

        var values = new List<SetMeasurementValueDto>();
        foreach (var measurement in measurements)
        {
            var name = measurement.Name.Trim();
            var unitName = measurement.Unit.Trim();
            if (name.Length == 0 || unitName.Length == 0)
            {
                continue;
            }

            var unitId = await EnsureUnitAsync(units, unitName, cancellationToken);
            var measurementId = await EnsureMeasurementAsync(allMeasurements, claimedMeasurementIds, name, unitName, subProgramId, unitId, cancellationToken);

            claimedMeasurementIds.Add(measurementId);
            values.Add(new SetMeasurementValueDto { MeasurementId = measurementId, UnitId = unitId, Value = measurement.Value });
        }

        if (values.Count > 0)
        {
            await _measurementService.SetValuesForSubProjectAsync(subProjectId, new SetSubProjectMeasurementValuesDto { Values = values }, cancellationToken);
        }
    }

    private async Task<int> EnsureUnitAsync(List<LookupDto> units, string unitName, CancellationToken cancellationToken)
    {
        var existing = units.FirstOrDefault(u => u.Name.Trim() == unitName);
        if (existing != null)
        {
            return existing.Id;
        }

        var created = await _lookupService.CreateUnitAsync(new CreateNamedLookupDto { Name = unitName }, cancellationToken);
        units.Add(created);
        return created.Id;
    }

    private async Task<int> EnsureMeasurementAsync(
        List<MeasurementDto> allMeasurements,
        HashSet<int> claimedMeasurementIds,
        string measurementName,
        string unitName,
        int subProgramId,
        int unitId,
        CancellationToken cancellationToken)
    {
        // Scope resolution to the target sub-program (Finding 6): only a Measurement that is
        // already applicable to this subProgramId is eligible for reuse, and only if it hasn't
        // already been claimed by an earlier triple in this same call.
        var candidate = allMeasurements.FirstOrDefault(m =>
            m.Name.Trim() == measurementName &&
            m.SubProgramIds.Contains(subProgramId) &&
            !claimedMeasurementIds.Contains(m.Id));

        if (candidate != null)
        {
            if (!candidate.UnitIds.Contains(unitId))
            {
                var updated = await _measurementService.UpdateAsync(candidate.Id, new UpdateMeasurementDto
                {
                    Name = candidate.Name,
                    SubProgramIds = candidate.SubProgramIds,
                    UnitIds = candidate.UnitIds.Append(unitId).ToList(),
                }, cancellationToken);
                ReplaceInList(allMeasurements, updated);
                return updated.Id;
            }

            return candidate.Id;
        }

        // No unclaimed, in-scope Measurement of this name exists. If one DOES exist in-scope but
        // is claimed (a same-name collision within this call, e.g. the "عدد" flagship case),
        // disambiguate the new Measurement's name so it isn't an indistinguishable duplicate in
        // the Settings > Measurements list. Otherwise (the normal, non-colliding case) use the
        // plain name.
        var hasSameNameInScope = allMeasurements.Any(m =>
            m.Name.Trim() == measurementName && m.SubProgramIds.Contains(subProgramId));
        var nameToUse = hasSameNameInScope ? $"{measurementName} - {unitName}" : measurementName;

        var created = await _measurementService.CreateAsync(new CreateMeasurementDto
        {
            Name = nameToUse,
            SubProgramIds = new List<int> { subProgramId },
            UnitIds = new List<int> { unitId },
        }, cancellationToken);
        allMeasurements.Add(created);
        return created.Id;
    }

    private static void ReplaceInList(List<MeasurementDto> list, MeasurementDto updated)
    {
        var index = list.FindIndex(m => m.Id == updated.Id);
        if (index >= 0)
        {
            list[index] = updated;
        }
        else
        {
            list.Add(updated);
        }
    }
}
