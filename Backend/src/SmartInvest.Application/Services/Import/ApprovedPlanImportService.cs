using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Enums;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services.Import;

public class ApprovedPlanImportService
{
    private readonly IMainProjectRepository _mainProjectRepository;
    private readonly ISubProjectRepository _subProjectRepository;
    private readonly IGenericRepository<FinancialYear> _financialYearRepository;
    private readonly IGenericRepository<PlanProject> _planProjectRepository;
    private readonly IGenericRepository<ProjectPriority> _priorityRepository;
    private readonly IGenericRepository<ProjectStatus> _statusRepository;
    private readonly IGenericRepository<Markaz> _markazRepository;
    private readonly IGenericRepository<ProjectLevel> _projectLevelRepository;
    private readonly IGenericRepository<ComponentType> _componentTypeRepository;
    private readonly IGenericRepository<AccountingUnit> _accountingUnitRepository;
    private readonly ISubProjectService _subProjectService;
    private readonly IPlanRepo _planRepo;
    private readonly IPlanService _planService;
    private readonly IUnitOfWork _unitOfWork;

    public ApprovedPlanImportService(
        IMainProjectRepository mainProjectRepository,
        ISubProjectRepository subProjectRepository,
        IGenericRepository<FinancialYear> financialYearRepository,
        IGenericRepository<PlanProject> planProjectRepository,
        IGenericRepository<ProjectPriority> priorityRepository,
        IGenericRepository<ProjectStatus> statusRepository,
        IGenericRepository<Markaz> markazRepository,
        IGenericRepository<ProjectLevel> projectLevelRepository,
        IGenericRepository<ComponentType> componentTypeRepository,
        IGenericRepository<AccountingUnit> accountingUnitRepository,
        ISubProjectService subProjectService,
        IPlanRepo planRepo,
        IPlanService planService,
        IUnitOfWork unitOfWork)
    {
        _mainProjectRepository = mainProjectRepository;
        _subProjectRepository = subProjectRepository;
        _financialYearRepository = financialYearRepository;
        _planProjectRepository = planProjectRepository;
        _priorityRepository = priorityRepository;
        _statusRepository = statusRepository;
        _markazRepository = markazRepository;
        _projectLevelRepository = projectLevelRepository;
        _componentTypeRepository = componentTypeRepository;
        _accountingUnitRepository = accountingUnitRepository;
        _subProjectService = subProjectService;
        _planRepo = planRepo;
        _planService = planService;
        _unitOfWork = unitOfWork;
    }

    private record MatchResult(int? SubProjectId, int? MainProjectId);

    public async Task<ApprovedImportPreviewDto> PreviewAsync(ParsedImportFile file, CancellationToken cancellationToken)
    {
        var dto = new ApprovedImportPreviewDto();

        foreach (var row in file.Rows)
        {
            var match = await MatchRowAsync(row, cancellationToken);
            if (match.SubProjectId.HasValue)
            {
                dto.MatchedCount++;
            }
            else
            {
                dto.UnresolvedRows.Add(new UnresolvedImportRowDto
                {
                    RowIndex = row.RowIndex,
                    MainProjectName = row.MainProjectName,
                    SubProjectName = row.SubProjectName,
                    Code = row.SubProjectCode,
                });
            }
        }

        return dto;
    }

    public async Task<ImportCommitResultDto> CommitAsync(ParsedImportFile file, ImportCommitDto dto, CancellationToken cancellationToken)
    {
        var financialYear = await _financialYearRepository.GetByIdAsync(dto.FinancialYearId, cancellationToken)
            ?? throw new NotFoundException($"السنة المالية رقم {dto.FinancialYearId} غير موجودة");

        var approvalDate = dto.ApprovalDate ?? throw new BusinessRuleException("تاريخ الاعتماد مطلوب");
        var rowResolutionByIndex = dto.RowResolutions.ToDictionary(r => r.RowIndex, r => r);

        var defaultPriorityId = (await _priorityRepository.FirstOrDefaultAsync(x => x.Priority == "منخفضة", cancellationToken))?.Id
            ?? throw new BusinessRuleException("أولوية «منخفضة» الافتراضية غير موجودة في قاعدة البيانات");
        var runningStatusId = (await _statusRepository.FirstOrDefaultAsync(x => x.StatusName == "قيد التنفيذ", cancellationToken))?.StatusId
            ?? throw new BusinessRuleException("حالة «قيد التنفيذ» الافتراضية غير موجودة في قاعدة البيانات");
        var unspecifiedMarkazId = (await _markazRepository.GetAllAsync(cancellationToken)).FirstOrDefault()?.MarkazId
            ?? throw new BusinessRuleException("لا يوجد أي مركز في قاعدة البيانات");
        var unspecifiedProjectLevelId = (await _projectLevelRepository.FirstOrDefaultAsync(x => x.Name == "غير محدد", cancellationToken))?.Id
            ?? throw new BusinessRuleException("مستوى «غير محدد» الافتراضي غير موجود في قاعدة البيانات");
        var unspecifiedComponentTypeId = (await _componentTypeRepository.FirstOrDefaultAsync(x => x.Name == "غير محدد", cancellationToken))?.Id
            ?? throw new BusinessRuleException("مكوّن عيني «غير محدد» الافتراضي غير موجود في قاعدة البيانات");
        var unspecifiedAccountingUnitId = (await _accountingUnitRepository.FirstOrDefaultAsync(x => x.Name == "غير محدد", cancellationToken))?.Id
            ?? throw new BusinessRuleException("وحدة حسابية «غير محدد» الافتراضية غير موجودة في قاعدة البيانات");

        var result = new ImportCommitResultDto { Mode = "Approved" };
        var approvedSubProjectIds = new List<int>();

        foreach (var row in file.Rows)
        {
            MainProject? mainProject = null;
            SubProject? subProject = null;
            bool mainProjectCreatedHere = false;
            try
            {
                var match = await MatchRowAsync(row, cancellationToken);
                int subProjectId;

                if (match.SubProjectId.HasValue)
                {
                    await _subProjectService.ApproveAsync(match.SubProjectId.Value, new ApproveSubProjectDto { Code = row.SubProjectCode.Trim() }, cancellationToken);
                    subProjectId = match.SubProjectId.Value;
                    result.SubProjectsApproved++;
                }
                else if (rowResolutionByIndex.TryGetValue(row.RowIndex, out var resolution) && !resolution.CreateNew && resolution.ExistingSubProjectId.HasValue)
                {
                    await _subProjectService.ApproveAsync(resolution.ExistingSubProjectId.Value, new ApproveSubProjectDto { Code = row.SubProjectCode.Trim() }, cancellationToken);
                    subProjectId = resolution.ExistingSubProjectId.Value;
                    result.SubProjectsApproved++;
                }
                else if (rowResolutionByIndex.TryGetValue(row.RowIndex, out var createResolution) && createResolution.CreateNew)
                {
                    var mainProjects = await _mainProjectRepository.FindByNameAsync(row.MainProjectName, cancellationToken);
                    // Only reuse an existing MainProject when the name match is unambiguous.
                    // If it matched multiple (ambiguous), MatchRowAsync already correctly rejected
                    // this row as unresolved; since staff explicitly chose "create new" anyway,
                    // create a genuinely new MainProject instead of guessing which one they meant.
                    mainProject = mainProjects.Count == 1 ? mainProjects[0] : null;
                    if (mainProject == null)
                    {
                        mainProject = new MainProject
                        {
                            MainProjectName = row.MainProjectName.Trim(),
                            MainProjectCode = null,
                            ExecutingAgency = string.Empty,
                            SubProgramId = (await _mainProjectRepository.GetAllWithDetailsAsync(cancellationToken)).FirstOrDefault()?.SubProgramId
                                ?? throw new BusinessRuleException("لا يوجد أي برنامج فرعي في قاعدة البيانات لإنشاء مشروع رئيسي جديد عليه"),
                            IsApproved = true,
                        };
                        await _mainProjectRepository.AddAsync(mainProject, cancellationToken);
                        mainProjectCreatedHere = true;
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        result.MainProjectsCreated++;
                    }

                    subProject = new SubProject
                    {
                        MainProjectId = mainProject.MainProjectId,
                        SubProjectName = row.SubProjectName.Trim(),
                        SubProjectCode = row.SubProjectCode.Trim(),
                        IsApproved = true,
                        ApprovedAt = approvalDate,
                        ProjectLevelId = unspecifiedProjectLevelId,
                        ComponentTypeId = unspecifiedComponentTypeId,
                        AccountingUnitId = unspecifiedAccountingUnitId,
                        ProjectNature = string.Empty,
                        MarkazId = unspecifiedMarkazId,
                        PriorityId = defaultPriorityId,
                        StatusId = runningStatusId,
                        BankFunding = 0,
                        SelfFunding = 0,
                    };
                    await _subProjectRepository.AddAsync(subProject, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    subProjectId = subProject.SubProjectId;
                    result.SubProjectsCreatedAndApproved++;
                }
                else
                {
                    throw new BusinessRuleException("الصف غير محلول ولم يتم تحديد إجراء له");
                }

                approvedSubProjectIds.Add(subProjectId);
            }
            catch (Exception ex)
            {
                result.Failed.Add(new ImportRowFailureDto { Name = row.SubProjectName, Reason = ex.Message });

                // SaveChangesAsync leaves a failed entity tracked as Added; if we don't detach it here,
                // the next AddAsync+SaveChangesAsync call will try to persist it again and fail again,
                // mislabeling the next row as failed for the same reason.
                // Only remove mainProject when THIS attempt created it - if it was reused from an
                // existing, already-persisted MainProject (Count == 1 match), it is tracked as
                // Unchanged and must not be transitioned to Deleted just because a later SubProject
                // insert failed; doing so would issue a real DELETE against an unrelated, valid row.
                if (mainProject is not null && mainProjectCreatedHere)
                {
                    _mainProjectRepository.Remove(mainProject);
                }

                if (subProject is not null)
                {
                    _subProjectRepository.Remove(subProject);
                }
            }
        }

        var approvedPlan = _planRepo.GetByFinancialYearAndStatus(dto.FinancialYearId, PlanStatus.Approved);
        var suggestedPlan = approvedPlan == null ? _planRepo.GetByFinancialYearAndStatus(dto.FinancialYearId, PlanStatus.Suggested) : null;

        Plan resultPlan;
        if (approvedPlan != null)
        {
            // Already approved for this financial year - reuse it rather than creating a duplicate.
            resultPlan = approvedPlan;
        }
        else if (suggestedPlan != null)
        {
            resultPlan = await _planService.ApproveAsync(suggestedPlan.PlanId, approvalDate);
        }
        else
        {
            resultPlan = new Plan
            {
                PlanName = $"الخطة المعتمدة – {financialYear.Name}",
                PlanStatus = PlanStatus.Approved,
                ApprovalDate = approvalDate,
                StartDate = financialYear.StartDate,
                EndDate = financialYear.EndDate,
                FinancialYearId = dto.FinancialYearId,
                SuggestionDate = DateTime.UtcNow,
            };
            await _planRepo.AddAsync(resultPlan, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var alreadyLinked = (await _planProjectRepository.FindAsync(x => x.PlanId == resultPlan.PlanId, cancellationToken))
            .Select(x => x.SubProjectId).ToHashSet();
        foreach (var subProjectId in approvedSubProjectIds.Where(id => !alreadyLinked.Contains(id)))
        {
            await _planProjectRepository.AddAsync(new PlanProject { PlanId = resultPlan.PlanId, SubProjectId = subProjectId }, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        result.PlanId = resultPlan.PlanId;
        result.PlanName = resultPlan.PlanName;
        result.PlanStatus = resultPlan.PlanStatus.ToString();

        return result;
    }

    private async Task<MatchResult> MatchRowAsync(ParsedImportRow row, CancellationToken cancellationToken)
    {
        var mainProjects = await _mainProjectRepository.FindByNameAsync(row.MainProjectName, cancellationToken);
        if (mainProjects.Count != 1)
        {
            return new MatchResult(null, null);
        }

        var subProjects = await _subProjectRepository.FindByNameWithinMainProjectAsync(row.SubProjectName, mainProjects[0].MainProjectId, cancellationToken);
        if (subProjects.Count != 1)
        {
            return new MatchResult(null, mainProjects[0].MainProjectId);
        }

        return new MatchResult(subProjects[0].SubProjectId, mainProjects[0].MainProjectId);
    }
}
