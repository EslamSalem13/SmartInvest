using AutoMapper;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services;

public class MeasurementService : IMeasurementService
{
    private readonly IMeasurementRepository _measurementRepository;
    private readonly IGenericRepository<MeasurementSubProgram> _linkRepository;
    private readonly IGenericRepository<MeasurementUnit> _unitLinkRepository;
    private readonly IGenericRepository<SubProjectMeasurementValue> _valueRepository;
    private readonly IGenericRepository<SubProject> _subProjectRepository;
    private readonly IGenericRepository<MainProject> _mainProjectRepository;
    private readonly IGenericRepository<SubProgram> _subProgramRepository;
    private readonly IGenericRepository<Unit> _unitRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public MeasurementService(
        IMeasurementRepository measurementRepository,
        IGenericRepository<MeasurementSubProgram> linkRepository,
        IGenericRepository<MeasurementUnit> unitLinkRepository,
        IGenericRepository<SubProjectMeasurementValue> valueRepository,
        IGenericRepository<SubProject> subProjectRepository,
        IGenericRepository<MainProject> mainProjectRepository,
        IGenericRepository<SubProgram> subProgramRepository,
        IGenericRepository<Unit> unitRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _measurementRepository = measurementRepository;
        _linkRepository = linkRepository;
        _unitLinkRepository = unitLinkRepository;
        _valueRepository = valueRepository;
        _subProjectRepository = subProjectRepository;
        _mainProjectRepository = mainProjectRepository;
        _subProgramRepository = subProgramRepository;
        _unitRepository = unitRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<MeasurementDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var measurements = await _measurementRepository.GetAllWithSubProgramsAsync(cancellationToken);
        return _mapper.Map<List<MeasurementDto>>(measurements);
    }

    public async Task<IReadOnlyList<MeasurementDto>> GetApplicableForSubProgramAsync(int subProgramId, CancellationToken cancellationToken = default)
    {
        var measurements = await _measurementRepository.GetApplicableForSubProgramAsync(subProgramId, cancellationToken);
        return _mapper.Map<List<MeasurementDto>>(measurements);
    }

    public async Task<MeasurementDto> CreateAsync(CreateMeasurementDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.UnitIds.Count == 0)
        {
            throw new BusinessRuleException("يجب اختيار وحدة قياس واحدة على الأقل للقياس");
        }

        await ValidateSubProgramIdsAsync(dto.SubProgramIds, cancellationToken);
        await ValidateUnitIdsAsync(dto.UnitIds, cancellationToken);

        var entity = new Measurement
        {
            Name = dto.Name.Trim(),
            MeasurementSubPrograms = dto.SubProgramIds
                .Select(spId => new MeasurementSubProgram { SubProgramId = spId })
                .ToList(),
            MeasurementUnits = dto.UnitIds
                .Select(uId => new MeasurementUnit { UnitId = uId })
                .ToList(),
        };

        await _measurementRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdOrThrowAsync(entity.Id, cancellationToken);
    }

    public async Task<MeasurementDto> UpdateAsync(int id, UpdateMeasurementDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.UnitIds.Count == 0)
        {
            throw new BusinessRuleException("يجب اختيار وحدة قياس واحدة على الأقل للقياس");
        }

        await ValidateSubProgramIdsAsync(dto.SubProgramIds, cancellationToken);
        await ValidateUnitIdsAsync(dto.UnitIds, cancellationToken);

        var entity = await _measurementRepository.GetByIdWithSubProgramsAsync(id, cancellationToken)
            ?? throw new NotFoundException($"القياس رقم {id} غير موجود");

        entity.Name = dto.Name.Trim();

        foreach (var existingLink in entity.MeasurementSubPrograms.ToList())
        {
            _linkRepository.Remove(existingLink);
        }
        entity.MeasurementSubPrograms = dto.SubProgramIds
            .Select(spId => new MeasurementSubProgram { MeasurementId = id, SubProgramId = spId })
            .ToList();

        var removedUnitIds = entity.MeasurementUnits
            .Select(ul => ul.UnitId)
            .Where(unitId => !dto.UnitIds.Contains(unitId))
            .ToList();

        if (removedUnitIds.Count > 0)
        {
            var valuesInRemovedUnits = await _valueRepository.FindAsync(
                x => x.MeasurementId == id && removedUnitIds.Contains(x.UnitId),
                cancellationToken);
            if (valuesInRemovedUnits.Count > 0)
            {
                throw new BusinessRuleException("لا يمكن إلغاء ربط الوحدة لوجود قيم مسجلة بها لهذا القياس");
            }
        }

        foreach (var existingUnitLink in entity.MeasurementUnits.ToList())
        {
            _unitLinkRepository.Remove(existingUnitLink);
        }
        entity.MeasurementUnits = dto.UnitIds
            .Select(uId => new MeasurementUnit { MeasurementId = id, UnitId = uId })
            .ToList();

        _measurementRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdOrThrowAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _measurementRepository.GetByIdWithSubProgramsAsync(id, cancellationToken)
            ?? throw new NotFoundException($"القياس رقم {id} غير موجود");

        var linkedValues = await _valueRepository.FindAsync(x => x.MeasurementId == id, cancellationToken);
        if (linkedValues.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف القياس لوجود قيم مسجلة عليه");
        }

        if (entity.MeasurementSubPrograms.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف القياس وهو مرتبط ببرامج فرعية — قم بإلغاء الربط أولًا");
        }

        foreach (var existingUnitLink in entity.MeasurementUnits.ToList())
        {
            _unitLinkRepository.Remove(existingUnitLink);
        }

        _measurementRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubProjectMeasurementValueDto>> GetValuesForSubProjectAsync(int subProjectId, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetByIdAsync(subProjectId, cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");

        var mainProject = await _mainProjectRepository.GetByIdAsync(subProject.MainProjectId, cancellationToken)
            ?? throw new NotFoundException("المشروع الرئيسي التابع له غير موجود");

        var applicable = await GetApplicableForSubProgramAsync(mainProject.SubProgramId, cancellationToken);

        var existingValues = await _valueRepository.FindAsync(x => x.SubProjectId == subProjectId, cancellationToken);
        var valuesByMeasurementId = existingValues.ToDictionary(x => x.MeasurementId);

        var units = await _unitRepository.GetAllAsync(cancellationToken);
        var unitNamesById = units.ToDictionary(u => u.Id, u => u.Name);

        // Measurement DEFINITIONS are shared across every sub-project in the sub-program (that's
        // the point - "عدد" reused instead of re-created per project), so "applicable" can list
        // dozens of measurements that have nothing to do with this specific sub-project. Only
        // return the ones this sub-project actually has a recorded VALUE for - the full applicable
        // list belongs to the "pick a name for a new row" datalist (GetApplicableForSubProgramAsync),
        // not to "what does this sub-project currently have".
        return applicable
            .Where(m => valuesByMeasurementId.ContainsKey(m.Id))
            .Select(m =>
            {
                var existing = valuesByMeasurementId[m.Id];
                return new SubProjectMeasurementValueDto
                {
                    MeasurementId = m.Id,
                    MeasurementName = m.Name,
                    UnitId = existing.UnitId,
                    UnitName = unitNamesById.TryGetValue(existing.UnitId, out var n) ? n : null,
                    Value = existing.Value,
                };
            })
            .ToList();
    }

    public async Task SetValuesForSubProjectAsync(int subProjectId, SetSubProjectMeasurementValuesDto dto, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetByIdAsync(subProjectId, cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");

        var mainProject = await _mainProjectRepository.GetByIdAsync(subProject.MainProjectId, cancellationToken)
            ?? throw new NotFoundException("المشروع الرئيسي التابع له غير موجود");

        var applicable = await GetApplicableForSubProgramAsync(mainProject.SubProgramId, cancellationToken);
        var applicableById = applicable.ToDictionary(m => m.Id);

        foreach (var entry in dto.Values)
        {
            if (!applicableById.TryGetValue(entry.MeasurementId, out var measurement))
            {
                throw new NotFoundException($"القياس رقم {entry.MeasurementId} غير مرتبط بالبرنامج الفرعي لهذا المشروع");
            }

            if (entry.Value != null && (entry.UnitId == null || !measurement.UnitIds.Contains(entry.UnitId.Value)))
            {
                throw new BusinessRuleException($"وحدة القياس غير صحيحة أو غير مرتبطة بالقياس «{measurement.Name}»");
            }
        }

        var existingValues = await _valueRepository.FindAsync(x => x.SubProjectId == subProjectId, cancellationToken);
        var existingByMeasurementId = existingValues.ToDictionary(x => x.MeasurementId);

        foreach (var entry in dto.Values)
        {
            if (entry.Value == null)
            {
                if (existingByMeasurementId.TryGetValue(entry.MeasurementId, out var toRemove))
                {
                    _valueRepository.Remove(toRemove);
                }
                continue;
            }

            if (existingByMeasurementId.TryGetValue(entry.MeasurementId, out var toUpdate))
            {
                toUpdate.Value = entry.Value.Value;
                toUpdate.UnitId = entry.UnitId!.Value;
                _valueRepository.Update(toUpdate);
            }
            else
            {
                await _valueRepository.AddAsync(new SubProjectMeasurementValue
                {
                    SubProjectId = subProjectId,
                    MeasurementId = entry.MeasurementId,
                    UnitId = entry.UnitId!.Value,
                    Value = entry.Value.Value,
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<MeasurementDto> GetByIdOrThrowAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _measurementRepository.GetByIdWithSubProgramsAsync(id, cancellationToken)
            ?? throw new NotFoundException($"القياس رقم {id} غير موجود");
        return _mapper.Map<MeasurementDto>(entity);
    }

    private async Task ValidateSubProgramIdsAsync(List<int> subProgramIds, CancellationToken cancellationToken)
    {
        foreach (var subProgramId in subProgramIds.Distinct())
        {
            var subProgram = await _subProgramRepository.GetByIdAsync(subProgramId, cancellationToken);
            if (subProgram == null)
            {
                throw new NotFoundException($"البرنامج الفرعي رقم {subProgramId} غير موجود");
            }
        }
    }

    private async Task ValidateUnitIdsAsync(List<int> unitIds, CancellationToken cancellationToken)
    {
        foreach (var unitId in unitIds.Distinct())
        {
            var unit = await _unitRepository.GetByIdAsync(unitId, cancellationToken);
            if (unit == null)
            {
                throw new NotFoundException($"الوحدة رقم {unitId} غير موجودة");
            }
        }
    }
}
