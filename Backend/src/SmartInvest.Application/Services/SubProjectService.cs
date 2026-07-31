using AutoMapper;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.DTOs.Common;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services;

public class SubProjectService : ISubProjectService
{
    private readonly ISubProjectRepository _subProjectRepository;
    private readonly IMainProjectRepository _mainProjectRepository;
    private readonly IGenericRepository<Markaz> _markazRepository;
    private readonly IGenericRepository<ProjectPriority> _priorityRepository;
    private readonly IGenericRepository<ProjectStatus> _statusRepository;
    private readonly IGenericRepository<ExecutiveAgency> _agencyRepository;
    private readonly IGenericRepository<ProjectAssignment> _assignmentRepository;
    private readonly IGenericRepository<ProjectLevel> _projectLevelRepository;
    private readonly IGenericRepository<ComponentType> _componentTypeRepository;
    private readonly IGenericRepository<AccountingUnit> _accountingUnitRepository;
    private readonly IGenericRepository<SubProjectFinancialYear> _financialYearLinkRepository;
    private readonly IGenericRepository<ProjectFollowUp> _followUpRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SubProjectService(
        ISubProjectRepository subProjectRepository,
        IMainProjectRepository mainProjectRepository,
        IGenericRepository<Markaz> markazRepository,
        IGenericRepository<ProjectPriority> priorityRepository,
        IGenericRepository<ProjectStatus> statusRepository,
        IGenericRepository<ExecutiveAgency> agencyRepository,
        IGenericRepository<ProjectAssignment> assignmentRepository,
        IGenericRepository<ProjectLevel> projectLevelRepository,
        IGenericRepository<ComponentType> componentTypeRepository,
        IGenericRepository<AccountingUnit> accountingUnitRepository,
        IGenericRepository<SubProjectFinancialYear> financialYearLinkRepository,
        IGenericRepository<ProjectFollowUp> followUpRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _subProjectRepository = subProjectRepository;
        _mainProjectRepository = mainProjectRepository;
        _markazRepository = markazRepository;
        _priorityRepository = priorityRepository;
        _statusRepository = statusRepository;
        _agencyRepository = agencyRepository;
        _assignmentRepository = assignmentRepository;
        _projectLevelRepository = projectLevelRepository;
        _componentTypeRepository = componentTypeRepository;
        _accountingUnitRepository = accountingUnitRepository;
        _financialYearLinkRepository = financialYearLinkRepository;
        _followUpRepository = followUpRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<SubProjectListItemDto>> SearchAsync(int? mainProjectId, int? mainProgramId, int? subProgramId, int? markazId, int? priorityId, int? statusId, int? financialYearId, string? searchTerm, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var result = await _subProjectRepository.SearchAsync(mainProjectId, mainProgramId, subProgramId, markazId,
            priorityId, statusId, financialYearId, searchTerm, page, pageSize, cancellationToken);

        var pagedResult = new PagedResultDto<SubProjectListItemDto>
        {
            Items = _mapper.Map<List<SubProjectListItemDto>>(result.Items),
            TotalCount = result.TotalCount,
            Page = page,
            PageSize = pageSize
        };

        return pagedResult;
    }

    public async Task<SubProjectDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetWithDetailsAsync(id, cancellationToken);
        if (subProject == null)
        {
            throw new NotFoundException($"المشروع الفرعي رقم {id} غير موجود");
        }

        return _mapper.Map<SubProjectDetailDto>(subProject);
    }

    public async Task<SubProjectDetailDto> CreateAsync(CreateSubProjectDto dto, CancellationToken cancellationToken = default)
    {
        await ValidateReferencesAsync(dto.MainProjectId, dto.MarkazId, dto.PriorityId, dto.StatusId, dto.ProjectLevelId, dto.ComponentTypeId, dto.AccountingUnitId, cancellationToken);

        var name = (dto.Name ?? string.Empty).Trim();
        if (await _subProjectRepository.NameExistsAsync(name, null, cancellationToken))
        {
            throw new BusinessRuleException("اسم المشروع الفرعي مستخدم بالفعل");
        }

        var code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();

        var subProject = _mapper.Map<SubProject>(dto);
        subProject.SubProjectName = name;
        subProject.SubProjectCode = code;
        subProject.IsApproved = code != null;

        await _subProjectRepository.AddAsync(subProject, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _subProjectRepository.GetWithDetailsAsync(subProject.SubProjectId, cancellationToken);
        return _mapper.Map<SubProjectDetailDto>(created);
    }

    public async Task<SubProjectDetailDto> UpdateAsync(int id, UpdateSubProjectDto dto, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetByIdAsync(id, cancellationToken);
        if (subProject == null)
        {
            throw new NotFoundException($"المشروع الفرعي رقم {id} غير موجود");
        }

        await ValidateReferencesAsync(subProject.MainProjectId, dto.MarkazId, dto.PriorityId, dto.StatusId, dto.ProjectLevelId, dto.ComponentTypeId, dto.AccountingUnitId, cancellationToken);

        var name = (dto.Name ?? string.Empty).Trim();
        if (await _subProjectRepository.NameExistsAsync(name, id, cancellationToken))
        {
            throw new BusinessRuleException("اسم المشروع الفرعي مستخدم بالفعل");
        }

        var code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();

        subProject.SubProjectName = name;
        subProject.SubProjectCode = code;
        subProject.IsApproved = code != null;
        subProject.ProjectLevelId = dto.ProjectLevelId;
        subProject.ComponentTypeId = dto.ComponentTypeId;
        subProject.AccountingUnitId = dto.AccountingUnitId;
        subProject.ProjectNature = dto.ProjectNature;
        subProject.MarkazId = dto.MarkazId;
        subProject.PriorityId = dto.PriorityId;
        subProject.StatusId = dto.StatusId;
        subProject.BankFunding = dto.BankFunding;
        subProject.SelfFunding = dto.SelfFunding;
        subProject.Latitude = dto.Latitude;
        subProject.Longitude = dto.Longitude;
        subProject.ProjectDescription = dto.Description;
        subProject.ProjectGoal = dto.Goal;
        subProject.SocialImpact = dto.SocialImpact;
        subProject.EconomicImpact = dto.EconomicImpact;
        subProject.EnvironmentalImpact = dto.EnvironmentalImpact;
        subProject.GreenInvestmentLink = dto.GreenInvestmentLink;

        _subProjectRepository.Update(subProject);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _subProjectRepository.GetWithDetailsAsync(id, cancellationToken);
        return _mapper.Map<SubProjectDetailDto>(updated);
    }

    public async Task<SubProjectDetailDto> ApproveAsync(int id, ApproveSubProjectDto dto, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetByIdAsync(id, cancellationToken);
        if (subProject == null)
        {
            throw new NotFoundException($"المشروع الفرعي رقم {id} غير موجود");
        }

        if (subProject.IsApproved)
        {
            throw new BusinessRuleException("المشروع الفرعي معتمد بالفعل");
        }

        var code = (dto.Code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessRuleException("كود المشروع الفرعي مطلوب للاعتماد");
        }

        // الكود مسموح تكراره — لا يوجد فحص uniqueness عليه
        subProject.SubProjectCode = code;
        subProject.IsApproved = true;
        subProject.ApprovalCancellationReason = null;
        subProject.ApprovalCancelledAt = null;
        subProject.ApprovedAt = DateTime.UtcNow;
        subProject.StatusId = await ResolveStatusIdByNameAsync("قيد التنفيذ", cancellationToken);

        _subProjectRepository.Update(subProject);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var approved = await _subProjectRepository.GetWithDetailsAsync(id, cancellationToken);
        return _mapper.Map<SubProjectDetailDto>(approved);
    }

    private async Task<int> ResolveStatusIdByNameAsync(string statusName, CancellationToken cancellationToken)
    {
        var status = await _statusRepository.FirstOrDefaultAsync(x => x.StatusName == statusName, cancellationToken);
        if (status == null)
        {
            throw new BusinessRuleException($"حالة «{statusName}» غير موجودة في قاعدة البيانات");
        }

        return status.StatusId;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetByIdAsync(id, cancellationToken);
        if (subProject == null)
        {
            throw new NotFoundException($"المشروع الفرعي رقم {id} غير موجود");
        }

        var financialYearLinks = await _financialYearLinkRepository.FindAsync(x => x.SubProjectId == id, cancellationToken);
        foreach (var link in financialYearLinks)
        {
            var followUps = await _followUpRepository.FindAsync(x => x.SubProjectFinancialYearId == link.SubProjectFinancialYearId, cancellationToken);
            if (followUps.Count > 0)
            {
                throw new BusinessRuleException("لا يمكن حذف المشروع الفرعي لوجود بيانات متابعة مسجلة عليه");
            }
        }

        foreach (var link in financialYearLinks)
        {
            _financialYearLinkRepository.Remove(link);
        }

        _subProjectRepository.Remove(subProject);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateReferencesAsync(int mainProjectId, int markazId, int priorityId, int statusId, int projectLevelId, int componentTypeId, int accountingUnitId, CancellationToken cancellationToken)
    {
        var mainProject = await _mainProjectRepository.GetByIdAsync(mainProjectId, cancellationToken);
        if (mainProject == null)
        {
            throw new NotFoundException("المشروع الرئيسي المحدد غير موجود");
        }

        var markaz = await _markazRepository.GetByIdAsync(markazId, cancellationToken);
        if (markaz == null)
        {
            throw new NotFoundException("المركز المحدد غير موجود");
        }

        var priority = await _priorityRepository.GetByIdAsync(priorityId, cancellationToken);
        if (priority == null)
        {
            throw new NotFoundException("الأولوية المحددة غير موجودة");
        }

        var status = await _statusRepository.GetByIdAsync(statusId, cancellationToken);
        if (status == null)
        {
            throw new NotFoundException("حالة المشروع المحددة غير موجودة");
        }

        var projectLevel = await _projectLevelRepository.GetByIdAsync(projectLevelId, cancellationToken);
        if (projectLevel == null)
        {
            throw new NotFoundException("مستوى المشروع المحدد غير موجود");
        }

        var componentType = await _componentTypeRepository.GetByIdAsync(componentTypeId, cancellationToken);
        if (componentType == null)
        {
            throw new NotFoundException("المكوّن العيني المحدد غير موجود");
        }

        var accountingUnit = await _accountingUnitRepository.GetByIdAsync(accountingUnitId, cancellationToken);
        if (accountingUnit == null)
        {
            throw new NotFoundException("الوحدة الحسابية المحددة غير موجودة");
        }
    }

    public async Task<SubProjectDetailDto> AssignExecutiveAgencyAsync(int id, int executiveAgencyId, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetByIdAsync(id, cancellationToken);
        if (subProject == null)
        {
            throw new NotFoundException($"المشروع الفرعي رقم {id} غير موجود");
        }

        var agency = await _agencyRepository.GetByIdAsync(executiveAgencyId, cancellationToken);
        if (agency == null)
        {
            throw new NotFoundException("الجهة التنفيذية المحددة غير موجودة");
        }

        var isAgencyChanging = subProject.ExecutiveAgencyId.HasValue && subProject.ExecutiveAgencyId != executiveAgencyId;
        if (isAgencyChanging)
        {
            var existingAssignments = await _assignmentRepository.FindAsync(x => x.SubProjectId == id, cancellationToken);
            foreach (var assignment in existingAssignments)
            {
                assignment.IsLocked = true;
                _assignmentRepository.Update(assignment);
            }
        }

        subProject.ExecutiveAgencyId = executiveAgencyId;
        _subProjectRepository.Update(subProject);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _subProjectRepository.GetWithDetailsAsync(id, cancellationToken);
        return _mapper.Map<SubProjectDetailDto>(updated);
    }
}
