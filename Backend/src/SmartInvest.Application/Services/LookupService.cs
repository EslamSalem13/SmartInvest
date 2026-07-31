using AutoMapper;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services;

public class LookupService : ILookupService
{
    private readonly IGenericRepository<ProjectPriority> _priorityRepository;
    private readonly IGenericRepository<ProjectStatus> _statusRepository;
    private readonly IGenericRepository<MainProgram> _mainProgramRepository;
    private readonly IGenericRepository<SubProgram> _subProgramRepository;
    private readonly IGenericRepository<Governorate> _governorateRepository;
    private readonly IGenericRepository<Markaz> _markazRepository;
    private readonly IGenericRepository<Village> _villageRepository;
    private readonly IGenericRepository<MainProject> _mainProjectRepository;
    private readonly IGenericRepository<SubProject> _subProjectRepository;
    private readonly IGenericRepository<ProjectFollowUp> _followUpRepository;
    private readonly IGenericRepository<ComponentType> _componentTypeRepository;
    private readonly IGenericRepository<ProjectLevel> _projectLevelRepository;
    private readonly IGenericRepository<AccountingUnit> _accountingUnitRepository;
    private readonly IGenericRepository<Unit> _unitRepository;
    private readonly IGenericRepository<MeasurementUnit> _measurementUnitRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public LookupService(
        IGenericRepository<ProjectPriority> priorityRepository,
        IGenericRepository<ProjectStatus> statusRepository,
        IGenericRepository<MainProgram> mainProgramRepository,
        IGenericRepository<SubProgram> subProgramRepository,
        IGenericRepository<Governorate> governorateRepository,
        IGenericRepository<Markaz> markazRepository,
        IGenericRepository<Village> villageRepository,
        IGenericRepository<MainProject> mainProjectRepository,
        IGenericRepository<SubProject> subProjectRepository,
        IGenericRepository<ProjectFollowUp> followUpRepository,
        IGenericRepository<ComponentType> componentTypeRepository,
        IGenericRepository<ProjectLevel> projectLevelRepository,
        IGenericRepository<AccountingUnit> accountingUnitRepository,
        IGenericRepository<Unit> unitRepository,
        IGenericRepository<MeasurementUnit> measurementUnitRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _priorityRepository = priorityRepository;
        _statusRepository = statusRepository;
        _mainProgramRepository = mainProgramRepository;
        _subProgramRepository = subProgramRepository;
        _governorateRepository = governorateRepository;
        _markazRepository = markazRepository;
        _villageRepository = villageRepository;
        _mainProjectRepository = mainProjectRepository;
        _subProjectRepository = subProjectRepository;
        _followUpRepository = followUpRepository;
        _componentTypeRepository = componentTypeRepository;
        _projectLevelRepository = projectLevelRepository;
        _accountingUnitRepository = accountingUnitRepository;
        _unitRepository = unitRepository;
        _measurementUnitRepository = measurementUnitRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<LookupDto>> GetPrioritiesAsync(CancellationToken cancellationToken = default)
    {
        var priorities = await _priorityRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(priorities);
    }

    public async Task<IReadOnlyList<LookupDto>> GetStatusesAsync(CancellationToken cancellationToken = default)
    {
        var statuses = await _statusRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(statuses);
    }

    public async Task<IReadOnlyList<LookupDto>> GetMainProgramsAsync(CancellationToken cancellationToken = default)
    {
        var mainPrograms = await _mainProgramRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(mainPrograms);
    }

    public async Task<IReadOnlyList<SubProgramLookupDto>> GetSubProgramsAsync(int? mainProgramId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SubProgram> subPrograms;

        if (mainProgramId.HasValue)
        {
            subPrograms = await _subProgramRepository.FindAsync(x => x.ProgramId == mainProgramId.Value, cancellationToken);
        }
        else
        {
            subPrograms = await _subProgramRepository.GetAllAsync(cancellationToken);
        }

        return _mapper.Map<List<SubProgramLookupDto>>(subPrograms);
    }

    public async Task<IReadOnlyList<LookupDto>> GetGovernoratesAsync(CancellationToken cancellationToken = default)
    {
        var governorates = await _governorateRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(governorates);
    }

    public async Task<IReadOnlyList<MarkazLookupDto>> GetMarkazAsync(int? governorateId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Markaz> markazList;

        if (governorateId.HasValue)
        {
            markazList = await _markazRepository.FindAsync(x => x.GovernorateId == governorateId.Value, cancellationToken);
        }
        else
        {
            markazList = await _markazRepository.GetAllAsync(cancellationToken);
        }

        return _mapper.Map<List<MarkazLookupDto>>(markazList);
    }

    public async Task<IReadOnlyList<VillageLookupDto>> GetVillagesAsync(int? markazId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Village> villages;

        if (markazId.HasValue)
        {
            villages = await _villageRepository.FindAsync(x => x.MarkazId == markazId.Value, cancellationToken);
        }
        else
        {
            villages = await _villageRepository.GetAllAsync(cancellationToken);
        }

        return _mapper.Map<List<VillageLookupDto>>(villages);
    }

    public async Task<LookupDto> CreatePriorityAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        var duplicates = await _priorityRepository.FindAsync(x => x.Priority == name, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم الأولوية «{name}» مستخدم بالفعل");
        }

        var entity = new ProjectPriority { Priority = name };
        await _priorityRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdatePriorityAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _priorityRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الأولوية رقم {id} غير موجودة");

        var name = dto.Name.Trim();
        var duplicates = await _priorityRepository.FindAsync(x => x.Priority == name && x.Id != id, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم الأولوية «{name}» مستخدم بالفعل");
        }

        entity.Priority = name;
        _priorityRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeletePriorityAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _priorityRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الأولوية رقم {id} غير موجودة");

        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.PriorityId == id, cancellationToken);
        if (linkedSubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف الأولوية لوجود مشروعات فرعية تستخدمها");
        }

        _priorityRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<LookupDto> CreateStatusAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        var duplicates = await _statusRepository.FindAsync(x => x.StatusName == name, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم الحالة «{name}» مستخدم بالفعل");
        }

        var entity = new ProjectStatus { StatusName = name };
        await _statusRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateStatusAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _statusRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الحالة رقم {id} غير موجودة");

        var name = dto.Name.Trim();
        var duplicates = await _statusRepository.FindAsync(x => x.StatusName == name && x.StatusId != id, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم الحالة «{name}» مستخدم بالفعل");
        }

        entity.StatusName = name;
        _statusRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteStatusAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _statusRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الحالة رقم {id} غير موجودة");

        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.StatusId == id, cancellationToken);
        var linkedFollowUps = await _followUpRepository.FindAsync(x => x.StatusId == id, cancellationToken);
        if (linkedSubProjects.Count > 0 || linkedFollowUps.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف الحالة لوجود مشروعات فرعية أو متابعات تستخدمها");
        }

        _statusRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<LookupDto> CreateMainProgramAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        var duplicates = await _mainProgramRepository.FindAsync(x => x.ProgramName == name, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم البرنامج الرئيسي «{name}» مستخدم بالفعل");
        }

        var entity = new MainProgram { ProgramName = name };
        await _mainProgramRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateMainProgramAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _mainProgramRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"البرنامج الرئيسي رقم {id} غير موجود");

        var name = dto.Name.Trim();
        var duplicates = await _mainProgramRepository.FindAsync(x => x.ProgramName == name && x.ProgramId != id, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم البرنامج الرئيسي «{name}» مستخدم بالفعل");
        }

        entity.ProgramName = name;
        _mainProgramRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteMainProgramAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _mainProgramRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"البرنامج الرئيسي رقم {id} غير موجود");

        var linkedSubPrograms = await _subProgramRepository.FindAsync(x => x.ProgramId == id, cancellationToken);
        if (linkedSubPrograms.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف البرنامج الرئيسي لوجود برامج فرعية تابعة له");
        }

        _mainProgramRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SubProgramLookupDto> CreateSubProgramAsync(CreateSubProgramDto dto, CancellationToken cancellationToken = default)
    {
        var mainProgram = await _mainProgramRepository.GetByIdAsync(dto.MainProgramId, cancellationToken)
            ?? throw new NotFoundException("البرنامج الرئيسي المحدد غير موجود");

        var name = dto.Name.Trim();
        var duplicates = await _subProgramRepository.FindAsync(x => x.SubProgramName == name && x.ProgramId == mainProgram.ProgramId, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم البرنامج الفرعي «{name}» مستخدم بالفعل");
        }

        var entity = new SubProgram { SubProgramName = name, ProgramId = mainProgram.ProgramId };
        await _subProgramRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<SubProgramLookupDto>(entity);
    }

    public async Task<SubProgramLookupDto> UpdateSubProgramAsync(int id, UpdateSubProgramDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _subProgramRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"البرنامج الفرعي رقم {id} غير موجود");

        var mainProgram = await _mainProgramRepository.GetByIdAsync(dto.MainProgramId, cancellationToken)
            ?? throw new NotFoundException("البرنامج الرئيسي المحدد غير موجود");

        var name = dto.Name.Trim();
        var duplicates = await _subProgramRepository.FindAsync(x => x.SubProgramName == name && x.SubProgramId != id && x.ProgramId == mainProgram.ProgramId, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم البرنامج الفرعي «{name}» مستخدم بالفعل");
        }

        entity.SubProgramName = name;
        entity.ProgramId = mainProgram.ProgramId;
        _subProgramRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<SubProgramLookupDto>(entity);
    }

    public async Task DeleteSubProgramAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _subProgramRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"البرنامج الفرعي رقم {id} غير موجود");

        var linkedMainProjects = await _mainProjectRepository.FindAsync(x => x.SubProgramId == id, cancellationToken);
        if (linkedMainProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف البرنامج الفرعي لوجود مشروعات رئيسية تابعة له");
        }

        _subProgramRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<LookupDto> CreateGovernorateAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        var duplicates = await _governorateRepository.FindAsync(x => x.GovernorateName == name, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم المحافظة «{name}» مستخدم بالفعل");
        }

        var entity = new Governorate { GovernorateName = name };
        await _governorateRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateGovernorateAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _governorateRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المحافظة رقم {id} غير موجودة");

        var name = dto.Name.Trim();
        var duplicates = await _governorateRepository.FindAsync(x => x.GovernorateName == name && x.GovernorateId != id, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم المحافظة «{name}» مستخدم بالفعل");
        }

        entity.GovernorateName = name;
        _governorateRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteGovernorateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _governorateRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المحافظة رقم {id} غير موجودة");

        var linkedMarkaz = await _markazRepository.FindAsync(x => x.GovernorateId == id, cancellationToken);
        if (linkedMarkaz.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف المحافظة لوجود مراكز تابعة لها");
        }

        _governorateRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<MarkazLookupDto> CreateMarkazAsync(CreateMarkazDto dto, CancellationToken cancellationToken = default)
    {
        var governorate = await _governorateRepository.GetByIdAsync(dto.GovernorateId, cancellationToken)
            ?? throw new NotFoundException("المحافظة المحددة غير موجودة");

        var name = dto.Name.Trim();
        var duplicates = await _markazRepository.FindAsync(x => x.MarkazName == name && x.GovernorateId == governorate.GovernorateId, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم المركز «{name}» مستخدم بالفعل");
        }

        var entity = new Markaz { MarkazName = name, GovernorateId = governorate.GovernorateId };
        await _markazRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<MarkazLookupDto>(entity);
    }

    public async Task<MarkazLookupDto> UpdateMarkazAsync(int id, UpdateMarkazDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _markazRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المركز رقم {id} غير موجود");

        var governorate = await _governorateRepository.GetByIdAsync(dto.GovernorateId, cancellationToken)
            ?? throw new NotFoundException("المحافظة المحددة غير موجودة");

        var name = dto.Name.Trim();
        var duplicates = await _markazRepository.FindAsync(x => x.MarkazName == name && x.MarkazId != id && x.GovernorateId == governorate.GovernorateId, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم المركز «{name}» مستخدم بالفعل");
        }

        entity.MarkazName = name;
        entity.GovernorateId = governorate.GovernorateId;
        _markazRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<MarkazLookupDto>(entity);
    }

    public async Task DeleteMarkazAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _markazRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المركز رقم {id} غير موجود");

        var linkedVillages = await _villageRepository.FindAsync(x => x.MarkazId == id, cancellationToken);
        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.MarkazId == id, cancellationToken);
        if (linkedVillages.Count > 0 || linkedSubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف المركز لوجود قرى أو مشروعات فرعية تابعة له");
        }

        _markazRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<VillageLookupDto> CreateVillageAsync(CreateVillageDto dto, CancellationToken cancellationToken = default)
    {
        var markaz = await _markazRepository.GetByIdAsync(dto.MarkazId, cancellationToken)
            ?? throw new NotFoundException("المركز المحدد غير موجود");

        var name = dto.Name.Trim();
        var duplicates = await _villageRepository.FindAsync(x => x.VillageName == name && x.MarkazId == markaz.MarkazId, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم القرية «{name}» مستخدم بالفعل");
        }

        var entity = new Village { VillageName = name, MarkazId = markaz.MarkazId };
        await _villageRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<VillageLookupDto>(entity);
    }

    public async Task<VillageLookupDto> UpdateVillageAsync(int id, UpdateVillageDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _villageRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"القرية رقم {id} غير موجودة");

        var markaz = await _markazRepository.GetByIdAsync(dto.MarkazId, cancellationToken)
            ?? throw new NotFoundException("المركز المحدد غير موجود");

        var name = dto.Name.Trim();
        var duplicates = await _villageRepository.FindAsync(x => x.VillageName == name && x.VillageId != id && x.MarkazId == markaz.MarkazId, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم القرية «{name}» مستخدم بالفعل");
        }

        entity.VillageName = name;
        entity.MarkazId = markaz.MarkazId;
        _villageRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<VillageLookupDto>(entity);
    }

    public async Task DeleteVillageAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _villageRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"القرية رقم {id} غير موجودة");

        _villageRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupDto>> GetComponentTypesAsync(CancellationToken cancellationToken = default)
    {
        var items = await _componentTypeRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(items);
    }

    public async Task<LookupDto> CreateComponentTypeAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        var duplicates = await _componentTypeRepository.FindAsync(x => x.Name == name, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم المكوّن العيني «{name}» مستخدم بالفعل");
        }

        var entity = new ComponentType { Name = name };
        await _componentTypeRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateComponentTypeAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _componentTypeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المكوّن العيني رقم {id} غير موجود");

        var name = dto.Name.Trim();
        var duplicates = await _componentTypeRepository.FindAsync(x => x.Name == name && x.Id != id, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم المكوّن العيني «{name}» مستخدم بالفعل");
        }

        entity.Name = name;
        _componentTypeRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteComponentTypeAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _componentTypeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المكوّن العيني رقم {id} غير موجود");

        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.ComponentTypeId == id, cancellationToken);
        if (linkedSubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف المكوّن العيني لوجود مشروعات فرعية تستخدمه");
        }

        _componentTypeRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupDto>> GetProjectLevelsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _projectLevelRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(items);
    }

    public async Task<LookupDto> CreateProjectLevelAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        var duplicates = await _projectLevelRepository.FindAsync(x => x.Name == name, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم مستوى المشروع «{name}» مستخدم بالفعل");
        }

        var entity = new ProjectLevel { Name = name };
        await _projectLevelRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateProjectLevelAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _projectLevelRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"مستوى المشروع رقم {id} غير موجود");

        var name = dto.Name.Trim();
        var duplicates = await _projectLevelRepository.FindAsync(x => x.Name == name && x.Id != id, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم مستوى المشروع «{name}» مستخدم بالفعل");
        }

        entity.Name = name;
        _projectLevelRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteProjectLevelAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _projectLevelRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"مستوى المشروع رقم {id} غير موجود");

        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.ProjectLevelId == id, cancellationToken);
        if (linkedSubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف مستوى المشروع لوجود مشروعات فرعية تستخدمه");
        }

        _projectLevelRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupDto>> GetAccountingUnitsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _accountingUnitRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(items);
    }

    public async Task<LookupDto> CreateAccountingUnitAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        var duplicates = await _accountingUnitRepository.FindAsync(x => x.Name == name, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم الوحدة الحسابية «{name}» مستخدم بالفعل");
        }

        var entity = new AccountingUnit { Name = name };
        await _accountingUnitRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateAccountingUnitAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _accountingUnitRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الوحدة الحسابية رقم {id} غير موجودة");

        var name = dto.Name.Trim();
        var duplicates = await _accountingUnitRepository.FindAsync(x => x.Name == name && x.Id != id, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم الوحدة الحسابية «{name}» مستخدم بالفعل");
        }

        entity.Name = name;
        _accountingUnitRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteAccountingUnitAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _accountingUnitRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الوحدة الحسابية رقم {id} غير موجودة");

        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.AccountingUnitId == id, cancellationToken);
        if (linkedSubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف الوحدة الحسابية لوجود مشروعات فرعية تستخدمها");
        }

        _accountingUnitRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupDto>> GetUnitsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _unitRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(items);
    }

    public async Task<LookupDto> CreateUnitAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        var duplicates = await _unitRepository.FindAsync(x => x.Name == name, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم الوحدة «{name}» مستخدم بالفعل");
        }

        var entity = new Unit { Name = name };
        await _unitRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateUnitAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _unitRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الوحدة رقم {id} غير موجودة");

        var name = dto.Name.Trim();
        var duplicates = await _unitRepository.FindAsync(x => x.Name == name && x.Id != id, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم الوحدة «{name}» مستخدم بالفعل");
        }

        entity.Name = name;
        _unitRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteUnitAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الوحدة رقم {id} غير موجودة");

        var linkedMeasurementUnits = await _measurementUnitRepository.FindAsync(x => x.UnitId == id, cancellationToken);
        if (linkedMeasurementUnits.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف الوحدة لارتباطها بقياسات مستخدمة");
        }

        _unitRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
