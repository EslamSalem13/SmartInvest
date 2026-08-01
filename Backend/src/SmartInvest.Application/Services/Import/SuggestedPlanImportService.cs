using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Enums;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services.Import;

public class SuggestedPlanImportService
{
    private readonly IGenericRepository<Markaz> _markazRepository;
    private readonly IGenericRepository<Governorate> _governorateRepository;
    private readonly IGenericRepository<MainProgram> _mainProgramRepository;
    private readonly IGenericRepository<SubProgram> _subProgramRepository;
    private readonly IGenericRepository<ExecutiveAgency> _agencyRepository;
    private readonly IGenericRepository<ProjectLevel> _projectLevelRepository;
    private readonly IGenericRepository<ComponentType> _componentTypeRepository;
    private readonly IGenericRepository<AccountingUnit> _accountingUnitRepository;
    private readonly IGenericRepository<ProjectPriority> _priorityRepository;
    private readonly IGenericRepository<ProjectStatus> _statusRepository;
    private readonly IGenericRepository<FinancialYear> _financialYearRepository;
    private readonly IGenericRepository<PlanProject> _planProjectRepository;
    private readonly ILookupService _lookupService;
    private readonly IExecutiveAgencyService _agencyService;
    private readonly IMainProjectRepository _mainProjectRepository;
    private readonly ISubProjectRepository _subProjectRepository;
    private readonly IPlanRepo _planRepo;
    private readonly IUnitOfWork _unitOfWork;

    public SuggestedPlanImportService(
        IGenericRepository<Markaz> markazRepository,
        IGenericRepository<Governorate> governorateRepository,
        IGenericRepository<MainProgram> mainProgramRepository,
        IGenericRepository<SubProgram> subProgramRepository,
        IGenericRepository<ExecutiveAgency> agencyRepository,
        IGenericRepository<ProjectLevel> projectLevelRepository,
        IGenericRepository<ComponentType> componentTypeRepository,
        IGenericRepository<AccountingUnit> accountingUnitRepository,
        IGenericRepository<ProjectPriority> priorityRepository,
        IGenericRepository<ProjectStatus> statusRepository,
        IGenericRepository<FinancialYear> financialYearRepository,
        IGenericRepository<PlanProject> planProjectRepository,
        ILookupService lookupService,
        IExecutiveAgencyService agencyService,
        IMainProjectRepository mainProjectRepository,
        ISubProjectRepository subProjectRepository,
        IPlanRepo planRepo,
        IUnitOfWork unitOfWork)
    {
        _markazRepository = markazRepository;
        _governorateRepository = governorateRepository;
        _mainProgramRepository = mainProgramRepository;
        _subProgramRepository = subProgramRepository;
        _agencyRepository = agencyRepository;
        _projectLevelRepository = projectLevelRepository;
        _componentTypeRepository = componentTypeRepository;
        _accountingUnitRepository = accountingUnitRepository;
        _priorityRepository = priorityRepository;
        _statusRepository = statusRepository;
        _financialYearRepository = financialYearRepository;
        _planProjectRepository = planProjectRepository;
        _lookupService = lookupService;
        _agencyService = agencyService;
        _mainProjectRepository = mainProjectRepository;
        _subProjectRepository = subProjectRepository;
        _planRepo = planRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<SuggestedImportPreviewDto> PreviewAsync(ParsedImportFile file, CancellationToken cancellationToken)
    {
        var markazNames = (await _markazRepository.GetAllAsync(cancellationToken)).Select(x => x.MarkazName).ToHashSet();
        var mainProgramNames = (await _mainProgramRepository.GetAllAsync(cancellationToken)).Select(x => x.ProgramName).ToHashSet();
        var subProgramNames = (await _subProgramRepository.GetAllAsync(cancellationToken)).Select(x => x.SubProgramName).ToHashSet();
        var agencyNames = (await _agencyRepository.GetAllAsync(cancellationToken)).Select(x => x.AgencyName).ToHashSet();
        var projectLevelNames = (await _projectLevelRepository.GetAllAsync(cancellationToken)).Select(x => x.Name).ToHashSet();
        var componentTypeNames = (await _componentTypeRepository.GetAllAsync(cancellationToken)).Select(x => x.Name).ToHashSet();
        var accountingUnitNames = (await _accountingUnitRepository.GetAllAsync(cancellationToken)).Select(x => x.Name).ToHashSet();

        var dto = new SuggestedImportPreviewDto
        {
            UnresolvedMarkaz = Unresolved(file.Rows, r => r.MarkazName, markazNames),
            UnresolvedMainPrograms = Unresolved(file.Rows, r => r.MainProgramName, mainProgramNames),
            UnresolvedSubPrograms = Unresolved(file.Rows, r => r.SubProgramName, subProgramNames),
            UnresolvedAgencies = Unresolved(file.Rows, r => r.ExecutiveAgencyName, agencyNames),
            UnresolvedProjectLevels = Unresolved(file.Rows, r => r.ProjectLevelName, projectLevelNames),
            UnresolvedComponentTypes = Unresolved(file.Rows, r => r.ComponentTypeName, componentTypeNames),
            UnresolvedAccountingUnits = Unresolved(file.Rows, r => r.AccountingUnitName, accountingUnitNames),
            MainProjectCodeConflicts = DetectCodeConflicts(file.Rows),
        };

        var mainProjectGroups = GroupByMainProject(file.Rows, new List<MainProjectCodeResolutionDto>());
        dto.MainProjectCount = mainProjectGroups.Count;
        dto.SubProjectCount = file.Rows.Count;

        return dto;
    }

    public async Task<ImportCommitResultDto> CommitAsync(ParsedImportFile file, ImportCommitDto dto, CancellationToken cancellationToken)
    {
        var financialYear = await _financialYearRepository.GetByIdAsync(dto.FinancialYearId, cancellationToken)
            ?? throw new NotFoundException($"السنة المالية رقم {dto.FinancialYearId} غير موجودة");

        var markazIdByName = await ResolveMarkazAsync(dto.MarkazResolutions, cancellationToken);
        var mainProgramIdByName = await ResolveNamedLookupAsync(
            dto.MainProgramResolutions, _mainProgramRepository, x => x.ProgramName, x => x.ProgramId,
            async name => (await _lookupService.CreateMainProgramAsync(new CreateNamedLookupDto { Name = name }, cancellationToken)).Id,
            cancellationToken);
        var mainProjectGroups = GroupByMainProject(file.Rows, dto.MainProjectCodeResolutions);
        var neededSubProgramPairs = mainProjectGroups
            .Where(g => mainProgramIdByName.ContainsKey(g.MainProgramName))
            .Select(g => (MainProgramId: mainProgramIdByName[g.MainProgramName], SubProgramName: g.Rows[0].SubProgramName.Trim()))
            .Distinct()
            .ToList();
        var subProgramIdByName = await ResolveSubProgramAsync(dto.SubProgramResolutions, neededSubProgramPairs, cancellationToken);
        var agencyIdByName = await ResolveNamedLookupAsync(
            dto.AgencyResolutions, _agencyRepository, x => x.AgencyName, x => x.ExecutiveAgencyId,
            async name => (await _agencyService.CreateAsync(new CreateExecutiveAgencyDto { AgencyName = name, Phone = string.Empty, Email = string.Empty, Address = string.Empty }, cancellationToken)).Id,
            cancellationToken);
        var projectLevelIdByName = await ResolveNamedLookupAsync(
            dto.ProjectLevelResolutions, _projectLevelRepository, x => x.Name, x => x.Id,
            async name => (await _lookupService.CreateProjectLevelAsync(new CreateNamedLookupDto { Name = name }, cancellationToken)).Id,
            cancellationToken);
        var componentTypeIdByName = await ResolveNamedLookupAsync(
            dto.ComponentTypeResolutions, _componentTypeRepository, x => x.Name, x => x.Id,
            async name => (await _lookupService.CreateComponentTypeAsync(new CreateNamedLookupDto { Name = name }, cancellationToken)).Id,
            cancellationToken);
        var accountingUnitIdByName = await ResolveNamedLookupAsync(
            dto.AccountingUnitResolutions, _accountingUnitRepository, x => x.Name, x => x.Id,
            async name => (await _lookupService.CreateAccountingUnitAsync(new CreateNamedLookupDto { Name = name }, cancellationToken)).Id,
            cancellationToken);

        var defaultPriorityId = (await _priorityRepository.FirstOrDefaultAsync(x => x.Priority == "منخفضة", cancellationToken))?.Id
            ?? throw new BusinessRuleException("أولوية «منخفضة» الافتراضية غير موجودة في قاعدة البيانات");
        var defaultStatusId = (await _statusRepository.FirstOrDefaultAsync(x => x.StatusName == "جديد", cancellationToken))?.StatusId
            ?? throw new BusinessRuleException("حالة «جديد» الافتراضية غير موجودة في قاعدة البيانات");

        var result = new ImportCommitResultDto { Mode = "Suggested" };
        var createdSubProjects = new List<SubProject>();

        foreach (var group in mainProjectGroups)
        {
            if (!mainProgramIdByName.TryGetValue(group.MainProgramName, out var mainProgramId))
            {
                foreach (var row in group.Rows)
                {
                    result.Failed.Add(new ImportRowFailureDto { Name = row.SubProjectName, Reason = $"البرنامج الرئيسي «{row.MainProgramName}» غير محلول" });
                }
                continue;
            }

            var subProgramName = group.Rows[0].SubProgramName.Trim();
            if (!subProgramIdByName.TryGetValue((mainProgramId, subProgramName), out var subProgramId))
            {
                foreach (var row in group.Rows)
                {
                    result.Failed.Add(new ImportRowFailureDto { Name = row.SubProjectName, Reason = $"البرنامج الفرعي «{row.SubProgramName}» غير محلول" });
                }
                continue;
            }

            MainProject? mainProject = null;
            try
            {
                mainProject = new MainProject
                {
                    MainProjectCode = string.IsNullOrWhiteSpace(group.Code) ? null : group.Code,
                    MainProjectName = group.MainProjectName,
                    ExecutingAgency = string.Empty,
                    SubProgramId = subProgramId,
                    IsApproved = false,
                };

                await _mainProjectRepository.AddAsync(mainProject, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                result.MainProjectsCreated++;
            }
            catch (Exception ex)
            {
                foreach (var row in group.Rows)
                {
                    result.Failed.Add(new ImportRowFailureDto { Name = row.SubProjectName, Reason = ex.Message });
                }

                // SaveChangesAsync leaves a failed entity tracked as Added; if we don't detach it here,
                // the next AddAsync+SaveChangesAsync call will try to persist it again and fail again,
                // mislabeling the next group as failed for the same reason.
                if (mainProject is not null)
                {
                    _mainProjectRepository.Remove(mainProject);
                }

                continue;
            }

            foreach (var row in group.Rows)
            {
                SubProject? subProject = null;
                try
                {
                    if (!markazIdByName.TryGetValue(row.MarkazName.Trim(), out var markazId))
                    {
                        throw new BusinessRuleException($"المركز «{row.MarkazName}» غير محلول");
                    }

                    if (!agencyIdByName.TryGetValue(row.ExecutiveAgencyName.Trim(), out var agencyId))
                    {
                        throw new BusinessRuleException($"الجهة المنفذة «{row.ExecutiveAgencyName}» غير محلولة");
                    }

                    if (!projectLevelIdByName.TryGetValue(row.ProjectLevelName.Trim(), out var projectLevelId))
                    {
                        throw new BusinessRuleException($"مستوى المشروع «{row.ProjectLevelName}» غير محلول");
                    }

                    if (!componentTypeIdByName.TryGetValue(row.ComponentTypeName.Trim(), out var componentTypeId))
                    {
                        throw new BusinessRuleException($"المكوّن العيني «{row.ComponentTypeName}» غير محلول");
                    }

                    if (!accountingUnitIdByName.TryGetValue(row.AccountingUnitName.Trim(), out var accountingUnitId))
                    {
                        throw new BusinessRuleException($"الوحدة الحسابية «{row.AccountingUnitName}» غير محلولة");
                    }

                    subProject = new SubProject
                    {
                        MainProjectId = mainProject.MainProjectId,
                        SubProjectName = row.SubProjectName.Trim(),
                        SubProjectCode = null,
                        IsApproved = false,
                        ProjectLevelId = projectLevelId,
                        ComponentTypeId = componentTypeId,
                        AccountingUnitId = accountingUnitId,
                        ProjectNature = string.Empty,
                        MarkazId = markazId,
                        PriorityId = defaultPriorityId,
                        StatusId = defaultStatusId,
                        ExecutiveAgencyId = agencyId,
                        BankFunding = row.BankFunding,
                        SelfFunding = row.SelfFunding,
                    };

                    await _subProjectRepository.AddAsync(subProject, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    createdSubProjects.Add(subProject);
                    result.SubProjectsCreated++;
                }
                catch (Exception ex)
                {
                    result.Failed.Add(new ImportRowFailureDto { Name = row.SubProjectName, Reason = ex.Message });

                    // SaveChangesAsync leaves a failed entity tracked as Added; if we don't detach it here,
                    // the next AddAsync+SaveChangesAsync call will try to persist it again and fail again,
                    // mislabeling the next row as failed for the same reason.
                    if (subProject is not null)
                    {
                        _subProjectRepository.Remove(subProject);
                    }
                }
            }
        }

        var plan = _planRepo.GetByFinancialYearAndStatus(dto.FinancialYearId, PlanStatus.Suggested);
        if (plan == null)
        {
            plan = new Plan
            {
                PlanName = $"الخطة المقترحة – {financialYear.Name}",
                PlanStatus = PlanStatus.Suggested,
                StartDate = financialYear.StartDate,
                EndDate = financialYear.EndDate,
                FinancialYearId = dto.FinancialYearId,
                SuggestionDate = DateTime.UtcNow,
            };
            await _planRepo.AddAsync(plan, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        foreach (var subProject in createdSubProjects)
        {
            await _planProjectRepository.AddAsync(new PlanProject { PlanId = plan.PlanId, SubProjectId = subProject.SubProjectId }, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        result.PlanId = plan.PlanId;
        result.PlanName = plan.PlanName;
        result.PlanStatus = plan.PlanStatus.ToString();

        return result;
    }

    private static List<UnresolvedNameDto> Unresolved(List<ParsedImportRow> rows, Func<ParsedImportRow, string> selector, HashSet<string> existingNames)
    {
        return rows
            .Select(r => selector(r).Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name) && !existingNames.Contains(name))
            .GroupBy(name => name)
            .Select(g => new UnresolvedNameDto { Name = g.Key, RowCount = g.Count() })
            .ToList();
    }

    private static List<MainProjectCodeConflictDto> DetectCodeConflicts(List<ParsedImportRow> rows)
    {
        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.MainProjectCode))
            .GroupBy(r => r.MainProjectCode.Trim())
            .Select(g => new
            {
                Code = g.Key,
                Pairs = g.Select(r => (Name: r.MainProjectName.Trim(), Program: r.MainProgramName.Trim())).Distinct().ToList(),
            })
            .Where(x => x.Pairs.Count > 1)
            .Select(x => new MainProjectCodeConflictDto
            {
                Code = x.Code,
                Options = x.Pairs.Select(p => new MainProjectCodeConflictOptionDto { MainProjectName = p.Name, MainProgramName = p.Program }).ToList(),
            })
            .ToList();
    }

    private class MainProjectGroup
    {
        public string Code { get; set; } = string.Empty;
        public string MainProjectName { get; set; } = string.Empty;
        public string MainProgramName { get; set; } = string.Empty;
        public List<ParsedImportRow> Rows { get; set; } = new();
    }

    private static List<MainProjectGroup> GroupByMainProject(
        List<ParsedImportRow> rows,
        List<MainProjectCodeResolutionDto> resolutions)
    {
        var resolutionByCode = resolutions.ToDictionary(r => r.Code.Trim(), r => r);

        string GroupKey(ParsedImportRow row)
        {
            var code = row.MainProjectCode.Trim();
            if (resolutionByCode.TryGetValue(code, out var resolution))
            {
                return $"{code}|{resolution.ChosenMainProjectName.Trim()}";
            }

            return $"{code}|{row.MainProjectName.Trim()}";
        }

        return rows
            .GroupBy(GroupKey)
            .Select(g =>
            {
                var first = g.First();
                var code = first.MainProjectCode.Trim();
                var name = resolutionByCode.TryGetValue(code, out var resolution)
                    ? resolution.ChosenMainProjectName.Trim()
                    : first.MainProjectName.Trim();
                var programName = resolutionByCode.TryGetValue(code, out var programResolution)
                    ? programResolution.ChosenMainProgramName.Trim()
                    : first.MainProgramName.Trim();

                return new MainProjectGroup { Code = code, MainProjectName = name, MainProgramName = programName, Rows = g.ToList() };
            })
            .ToList();
    }

    private async Task<Dictionary<string, int>> ResolveMarkazAsync(List<ImportResolutionDto> resolutions, CancellationToken cancellationToken)
    {
        var governorates = await _governorateRepository.GetAllAsync(cancellationToken);
        if (governorates.Count != 1)
        {
            throw new BusinessRuleException("تعذّر تحديد المحافظة الافتراضية — يجب أن توجد محافظة واحدة بالضبط في النظام");
        }
        var governorateId = governorates[0].GovernorateId;

        var existing = await _markazRepository.GetAllAsync(cancellationToken);
        var result = existing.ToDictionary(x => x.MarkazName, x => x.MarkazId);

        foreach (var resolution in resolutions.Where(r => r.CreateNew))
        {
            var name = resolution.Name.Trim();
            if (result.ContainsKey(name))
            {
                continue;
            }

            var created = await _lookupService.CreateMarkazAsync(new CreateMarkazDto { Name = name, GovernorateId = governorateId }, cancellationToken);
            result[name] = created.Id;
        }

        foreach (var resolution in resolutions.Where(r => !r.CreateNew && r.ExistingId.HasValue))
        {
            var match = existing.FirstOrDefault(x => x.MarkazId == resolution.ExistingId!.Value);
            if (match != null)
            {
                result[resolution.Name.Trim()] = match.MarkazId;
            }
        }

        return result;
    }

    private async Task<Dictionary<(int MainProgramId, string SubProgramName), int>> ResolveSubProgramAsync(
        List<ImportResolutionDto> resolutions, List<(int MainProgramId, string SubProgramName)> neededPairs, CancellationToken cancellationToken)
    {
        var existing = await _subProgramRepository.GetAllAsync(cancellationToken);
        var result = existing.ToDictionary(x => (x.ProgramId, x.SubProgramName), x => x.SubProgramId);

        foreach (var resolution in resolutions.Where(r => r.CreateNew))
        {
            var name = resolution.Name.Trim();
            // Only create under the main program(s) this import file actually pairs this
            // sub-program name with - not every main program in the database (that used to
            // fan out one duplicate sub-program row per unrelated existing main program).
            foreach (var mainProgramId in neededPairs.Where(p => p.SubProgramName == name).Select(p => p.MainProgramId).Distinct())
            {
                if (result.ContainsKey((mainProgramId, name)))
                {
                    continue;
                }
                var created = await _lookupService.CreateSubProgramAsync(new CreateSubProgramDto { Name = name, MainProgramId = mainProgramId }, cancellationToken);
                result[(mainProgramId, name)] = created.Id;
            }
        }

        foreach (var resolution in resolutions.Where(r => !r.CreateNew && r.ExistingId.HasValue))
        {
            var match = existing.FirstOrDefault(x => x.SubProgramId == resolution.ExistingId!.Value);
            if (match == null)
            {
                continue;
            }
            var name = resolution.Name.Trim();
            // Staff explicitly chose to map this unresolved name to an existing sub-program
            // regardless of that sub-program's own parent main program - key the mapping by
            // every main program this file's rows actually need it under (falling back to the
            // match's own parent if the file happens not to reference any group needing it,
            // e.g. all rows for that name were rejected earlier for an unrelated reason).
            var targetProgramIds = neededPairs.Where(p => p.SubProgramName == name).Select(p => p.MainProgramId).Distinct().ToList();
            if (targetProgramIds.Count == 0)
            {
                targetProgramIds.Add(match.ProgramId);
            }
            foreach (var mainProgramId in targetProgramIds)
            {
                result[(mainProgramId, name)] = match.SubProgramId;
            }
        }

        return result;
    }

    private static async Task<Dictionary<string, int>> ResolveNamedLookupAsync<T>(
        List<ImportResolutionDto> resolutions,
        IGenericRepository<T> repository,
        Func<T, string> nameSelector,
        Func<T, int> idSelector,
        Func<string, Task<int>> createNew,
        CancellationToken cancellationToken)
        where T : class
    {
        var existing = await repository.GetAllAsync(cancellationToken);
        var result = new Dictionary<string, int>();
        foreach (var item in existing)
        {
            result[nameSelector(item)] = idSelector(item);
        }

        foreach (var resolution in resolutions.Where(r => r.CreateNew))
        {
            var name = resolution.Name.Trim();
            if (result.ContainsKey(name))
            {
                continue;
            }
            result[name] = await createNew(name);
        }

        foreach (var resolution in resolutions.Where(r => !r.CreateNew && r.ExistingId.HasValue))
        {
            var match = existing.FirstOrDefault(x => idSelector(x) == resolution.ExistingId!.Value);
            if (match != null)
            {
                result[resolution.Name.Trim()] = resolution.ExistingId!.Value;
            }
        }

        return result;
    }
}
