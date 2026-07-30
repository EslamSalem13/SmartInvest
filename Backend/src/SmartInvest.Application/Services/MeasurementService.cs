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
    private readonly IGenericRepository<SubProjectMeasurementValue> _valueRepository;
    private readonly IGenericRepository<SubProject> _subProjectRepository;
    private readonly IGenericRepository<MainProject> _mainProjectRepository;
    private readonly IGenericRepository<SubProgram> _subProgramRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public MeasurementService(
        IMeasurementRepository measurementRepository,
        IGenericRepository<MeasurementSubProgram> linkRepository,
        IGenericRepository<SubProjectMeasurementValue> valueRepository,
        IGenericRepository<SubProject> subProjectRepository,
        IGenericRepository<MainProject> mainProjectRepository,
        IGenericRepository<SubProgram> subProgramRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _measurementRepository = measurementRepository;
        _linkRepository = linkRepository;
        _valueRepository = valueRepository;
        _subProjectRepository = subProjectRepository;
        _mainProjectRepository = mainProjectRepository;
        _subProgramRepository = subProgramRepository;
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
        await ValidateSubProgramIdsAsync(dto.SubProgramIds, cancellationToken);

        var entity = new Measurement
        {
            Name = dto.Name.Trim(),
            Unit = dto.Unit.Trim(),
            MeasurementSubPrograms = dto.SubProgramIds
                .Select(spId => new MeasurementSubProgram { SubProgramId = spId })
                .ToList(),
        };

        await _measurementRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdOrThrowAsync(entity.Id, cancellationToken);
    }

    public async Task<MeasurementDto> UpdateAsync(int id, UpdateMeasurementDto dto, CancellationToken cancellationToken = default)
    {
        await ValidateSubProgramIdsAsync(dto.SubProgramIds, cancellationToken);

        var entity = await _measurementRepository.GetByIdWithSubProgramsAsync(id, cancellationToken)
            ?? throw new NotFoundException($"القياس رقم {id} غير موجود");

        entity.Name = dto.Name.Trim();
        entity.Unit = dto.Unit.Trim();

        foreach (var existingLink in entity.MeasurementSubPrograms.ToList())
        {
            _linkRepository.Remove(existingLink);
        }
        entity.MeasurementSubPrograms = dto.SubProgramIds
            .Select(spId => new MeasurementSubProgram { MeasurementId = id, SubProgramId = spId })
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
        var valuesByMeasurementId = existingValues.ToDictionary(x => x.MeasurementId, x => x.Value);

        return applicable
            .Select(m => new SubProjectMeasurementValueDto
            {
                MeasurementId = m.Id,
                MeasurementName = m.Name,
                Unit = m.Unit,
                Value = valuesByMeasurementId.TryGetValue(m.Id, out var v) ? v : null,
            })
            .ToList();
    }

    public async Task SetValuesForSubProjectAsync(int subProjectId, SetSubProjectMeasurementValuesDto dto, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetByIdAsync(subProjectId, cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");

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
                _valueRepository.Update(toUpdate);
            }
            else
            {
                await _valueRepository.AddAsync(new SubProjectMeasurementValue
                {
                    SubProjectId = subProjectId,
                    MeasurementId = entry.MeasurementId,
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
}
