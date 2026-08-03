# Excel Project Import (Suggested + Approved) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let planning staff bulk-import projects from a single `.xlsx` file uploaded through "إضافة مشروع" → "استيراد من Excel". The system auto-detects whether the file is a **suggested plan** (no project codes) or an **approved plan** (every row coded): a suggested file creates new Main/Sub Projects (with staff reconciling any unmatched Markaz/MainProgram/SubProgram/Agency/ProjectLevel/ComponentType/AccountingUnit names) and links them to a Suggested `Plan`; an approved file matches existing projects by name, assigns their code, approves them, and flips the year's Suggested `Plan` to Approved (or creates one directly as Approved if none exists).

**Architecture:** A new `ImportController` exposes `POST /api/subprojects/import/preview` (multipart upload) and `POST /api/subprojects/import/commit` (JSON). Parsing (`ExcelImportParser`, via ClosedXML) reads the sheet, detects the mode from the "كود المشروع" column, and hands rows to one of two mode-specific services (`SuggestedPlanImportService` / `ApprovedPlanImportService`), both behind one `IImportService` facade. Parsed-but-uncommitted data is cached server-side (`ImportSessionStore`, wrapping `IMemoryCache`, 30-minute TTL) keyed by a short-lived `importId`, so commit doesn't require re-upload. Frontend gets a single wizard component with an internal step signal, auto-branching its Step 2/3 content by the detected mode, wired into the existing `projects.ts` add-menu as a 3rd option.

**Tech Stack:** .NET 10 (EF Core, AutoMapper, ClosedXML — new dependency), Angular 21 standalone components + Signals.

## Global Constraints

- No automated test suite exists anywhere in this repo. Each task's "test" step is build/type-check, then a manual check via the browser preview tool (or Swagger/curl for backend-only tasks).
- No AI/fuzzy matching anywhere in this feature — every match is an exact, trimmed string comparison. Confirmed explicitly with the user.
- This entire application is scoped to one governorate ("المنوفية") — `LookupSeeder.cs` seeds exactly one `Governorate` row, always. Any new `Markaz` created during reconciliation resolves its `GovernorateId` to that single existing row automatically — no governorate picker, no reconciliation category for it.
- `FluentValidation` validators exist in `Backend/src/SmartInvest.Application/Validators/` and are auto-discovered (`AddValidatorsFromAssembly`) but are **never actually invoked anywhere in this codebase** (confirmed: no `IValidator<T>.ValidateAsync` call, no `AddFluentValidationAutoValidation()`, no custom action filter). Do not add a validator for any new DTO in this plan and do not worry about `CreateExecutiveAgencyDtoValidator`'s `Phone`/`Email` `NotEmpty` rules blocking anything — they don't run. This is a pre-existing, unrelated quirk, not something this plan fixes.
- Bulk-created `SubProject`/`MainProject` rows in suggested-mode must bypass `SubProjectService`/`MainProjectService`'s own name/code-uniqueness checks — those services correctly reject duplicate names for the single-add form, but this importer's own base spec explicitly requires that re-running the same file a second time creates fresh duplicate rows without failing (matches real-world messy source data: 85 rows, 80 distinct codes). Construct `SubProject`/`MainProject` entities directly and add them via the repository, exactly mirroring the existing precedent at `Backend/src/SmartInvest.API/Controllers/PlansController.cs`'s `AddNewProjectToPlan` action (manual entity construction, bypassing AutoMapper/the service layer, with a comment explaining why).
- Design docs for this feature (read both before starting): `docs/superpowers/specs/2026-07-30-excel-project-import-design.md` (base mechanics: column mapping, ClosedXML choice, base preview/reconcile/commit flow) and `docs/superpowers/specs/2026-08-01-excel-import-suggested-approved-design.md` (supersedes/extends it: suggested-vs-approved detection, Plan integration, the 3 extra reconciliation categories). Every task below cites the exact section it implements.
- `npx tsc --noEmit -p tsconfig.app.json` does NOT type-check `.html` templateUrl files — only `npx ng build` catches template-binding errors. Run `ng build` as the final check of every frontend task.
- Never run dev servers via Bash — use the `preview_start` tool.
- **Known recurring issue:** a stray `SmartInvest.API.exe` process can hold the build output DLL locked. If `dotnet build` fails with a file-lock error, stop it first (`taskkill //F //IM SmartInvest.API.exe` via bash, or PowerShell `Get-Process -Name SmartInvest.API | Stop-Process -Force`), then rebuild.

---

### Task 1: Excel Parsing Infrastructure

**Files:**
- Modify: `Backend/src/SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj` (add ClosedXML)
- Modify: `Backend/src/SmartInvest.Application/SmartInvest.Application.csproj` (add Microsoft.Extensions.Caching.Abstractions)
- Create: `Backend/src/SmartInvest.Application/DTOs/ImportDtos.cs`
- Create: `Backend/src/SmartInvest.Application/Services/Import/ParsedImportRow.cs`
- Create: `Backend/src/SmartInvest.Application/Services/Import/ImportSessionStore.cs`
- Create: `Backend/src/SmartInvest.Infrastructure/Services/ExcelImportParser.cs`
- Create: `Backend/src/SmartInvest.Application/Interfaces/IExcelImportParser.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs` (register `AddMemoryCache()`, `IExcelImportParser`, `ImportSessionStore`)

**Interfaces:**
- Produces: `ImportMode` enum (`Suggested`/`Approved`), `ParsedImportRow`/`ParsedImportFile` (server-internal, cached), `ImportSessionStore.Save(ParsedImportFile) → string importId` / `Get(string importId) → ParsedImportFile?`, `IExcelImportParser.Parse(Stream) → ParsedImportFile` (throws `BusinessRuleException` on a mixed-code file). All the response/request DTOs (`ImportPreviewResultDto`, `ImportCommitDto`, `ImportCommitResultDto`, etc.) that Tasks 2-4 consume and populate.

- [ ] **Step 1: Add the ClosedXML and caching dependencies**

In `Backend/src/SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj`, add to the existing `<ItemGroup>` with the other `PackageReference`s:

```xml
    <PackageReference Include="ClosedXML" Version="0.104.2" />
```

In `Backend/src/SmartInvest.Application/SmartInvest.Application.csproj`, add to the existing `<ItemGroup>` with the other `PackageReference`s:

```xml
    <PackageReference Include="Microsoft.Extensions.Caching.Abstractions" Version="10.0.10" />
```

Run `cd Backend/src/SmartInvest.Infrastructure && dotnet restore` and `cd Backend/src/SmartInvest.Application && dotnet restore` to fetch both packages. Confirm no restore errors.

- [ ] **Step 2: Add the DTOs**

Create `Backend/src/SmartInvest.Application/DTOs/ImportDtos.cs`:

```csharp
namespace SmartInvest.Application.DTOs;

public class UnresolvedNameDto
{
    public string Name { get; set; } = string.Empty;
    public int RowCount { get; set; }
}

public class MainProjectCodeConflictOptionDto
{
    public string MainProjectName { get; set; } = string.Empty;
    public string MainProgramName { get; set; } = string.Empty;
}

public class MainProjectCodeConflictDto
{
    public string Code { get; set; } = string.Empty;
    public List<MainProjectCodeConflictOptionDto> Options { get; set; } = new();
}

public class SuggestedImportPreviewDto
{
    public int MainProjectCount { get; set; }
    public int SubProjectCount { get; set; }
    public List<UnresolvedNameDto> UnresolvedMarkaz { get; set; } = new();
    public List<UnresolvedNameDto> UnresolvedMainPrograms { get; set; } = new();
    public List<UnresolvedNameDto> UnresolvedSubPrograms { get; set; } = new();
    public List<UnresolvedNameDto> UnresolvedAgencies { get; set; } = new();
    public List<UnresolvedNameDto> UnresolvedProjectLevels { get; set; } = new();
    public List<UnresolvedNameDto> UnresolvedComponentTypes { get; set; } = new();
    public List<UnresolvedNameDto> UnresolvedAccountingUnits { get; set; } = new();
    public List<MainProjectCodeConflictDto> MainProjectCodeConflicts { get; set; } = new();
}

public class UnresolvedImportRowDto
{
    public int RowIndex { get; set; }
    public string MainProjectName { get; set; } = string.Empty;
    public string SubProjectName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ApprovedImportPreviewDto
{
    public int MatchedCount { get; set; }
    public List<UnresolvedImportRowDto> UnresolvedRows { get; set; } = new();
}

public class ImportPreviewResultDto
{
    public string ImportId { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public SuggestedImportPreviewDto? Suggested { get; set; }
    public ApprovedImportPreviewDto? Approved { get; set; }
}

public class ImportResolutionDto
{
    public string Name { get; set; } = string.Empty;
    public bool CreateNew { get; set; }
    public int? ExistingId { get; set; }
}

public class MainProjectCodeResolutionDto
{
    public string Code { get; set; } = string.Empty;
    public string ChosenMainProjectName { get; set; } = string.Empty;
    public string ChosenMainProgramName { get; set; } = string.Empty;
}

public class ImportRowResolutionDto
{
    public int RowIndex { get; set; }
    public bool CreateNew { get; set; }
    public int? ExistingSubProjectId { get; set; }
}

public class ImportCommitDto
{
    public string ImportId { get; set; } = string.Empty;
    public int FinancialYearId { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public List<ImportResolutionDto> MarkazResolutions { get; set; } = new();
    public List<ImportResolutionDto> MainProgramResolutions { get; set; } = new();
    public List<ImportResolutionDto> SubProgramResolutions { get; set; } = new();
    public List<ImportResolutionDto> AgencyResolutions { get; set; } = new();
    public List<ImportResolutionDto> ProjectLevelResolutions { get; set; } = new();
    public List<ImportResolutionDto> ComponentTypeResolutions { get; set; } = new();
    public List<ImportResolutionDto> AccountingUnitResolutions { get; set; } = new();
    public List<MainProjectCodeResolutionDto> MainProjectCodeResolutions { get; set; } = new();
    public List<ImportRowResolutionDto> RowResolutions { get; set; } = new();
}

public class ImportRowFailureDto
{
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class ImportCommitResultDto
{
    public string Mode { get; set; } = string.Empty;
    public int MainProjectsCreated { get; set; }
    public int SubProjectsCreated { get; set; }
    public int SubProjectsApproved { get; set; }
    public int SubProjectsCreatedAndApproved { get; set; }
    public List<ImportRowFailureDto> Failed { get; set; } = new();
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanStatus { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Add the internal parsed-row model**

Create `Backend/src/SmartInvest.Application/Services/Import/ParsedImportRow.cs`:

```csharp
namespace SmartInvest.Application.Services.Import;

public enum ImportMode
{
    Suggested,
    Approved,
}

public class ParsedImportRow
{
    public int RowIndex { get; set; }
    public string MainProgramName { get; set; } = string.Empty;
    public string SubProgramName { get; set; } = string.Empty;
    public string MainProjectCode { get; set; } = string.Empty;
    public string MainProjectName { get; set; } = string.Empty;
    public string ProjectLevelName { get; set; } = string.Empty;
    public string ExecutiveAgencyName { get; set; } = string.Empty;
    public string MarkazName { get; set; } = string.Empty;
    public string SubProjectCode { get; set; } = string.Empty;
    public string SubProjectName { get; set; } = string.Empty;
    public string ComponentTypeName { get; set; } = string.Empty;
    public decimal BankFunding { get; set; }
    public decimal SelfFunding { get; set; }
    public string AccountingUnitName { get; set; } = string.Empty;
}

public class ParsedImportFile
{
    public ImportMode Mode { get; set; }
    public List<ParsedImportRow> Rows { get; set; } = new();
}
```

- [ ] **Step 4: Add the import-session cache wrapper**

Create `Backend/src/SmartInvest.Application/Services/Import/ImportSessionStore.cs`:

```csharp
using Microsoft.Extensions.Caching.Memory;

namespace SmartInvest.Application.Services.Import;

public class ImportSessionStore
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    public ImportSessionStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Save(ParsedImportFile file)
    {
        var importId = Guid.NewGuid().ToString("N");
        _cache.Set(CacheKey(importId), file, Ttl);
        return importId;
    }

    public ParsedImportFile? Get(string importId)
    {
        return _cache.TryGetValue(CacheKey(importId), out ParsedImportFile? file) ? file : null;
    }

    public void Remove(string importId)
    {
        _cache.Remove(CacheKey(importId));
    }

    private static string CacheKey(string importId) => $"import-session:{importId}";
}
```

- [ ] **Step 5: Add the parser interface**

Create `Backend/src/SmartInvest.Application/Interfaces/IExcelImportParser.cs`:

```csharp
using SmartInvest.Application.Services.Import;

namespace SmartInvest.Application.Interfaces;

public interface IExcelImportParser
{
    ParsedImportFile Parse(Stream fileStream);
}
```

- [ ] **Step 6: Implement the parser**

Create `Backend/src/SmartInvest.Infrastructure/Services/ExcelImportParser.cs`. Column headers (exact Arabic text) match the base spec's §2 table. The header row is located by scanning the first 10 rows of the sheet for a row containing every expected header (handles a title row above the real header row, common in real-world exports).

```csharp
using ClosedXML.Excel;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.Interfaces;
using SmartInvest.Application.Services.Import;

namespace SmartInvest.Infrastructure.Services;

public class ExcelImportParser : IExcelImportParser
{
    private static readonly string[] ExpectedHeaders =
    {
        "البرنامج الرئيسي", "البرنامج الفرعي", "كود المشروع الرئيسى", "المشروع الرئيسى",
        "مستوى المشروع", "الجهة المنفذة", "المركز", "كود المشروع", "المشروع الفرعى",
        "المكوّن العيني", "بنك", "ذاتي", "الوحدة الحسابية",
    };

    public ParsedImportFile Parse(Stream fileStream)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.First();

        var headerRowNumber = FindHeaderRow(worksheet);
        if (headerRowNumber == -1)
        {
            throw new BusinessRuleException("تعذّر التعرف على أعمدة الملف — تأكد من رفع ملف الخطة الصحيح");
        }

        var columnIndexByHeader = new Dictionary<string, int>();
        var headerRow = worksheet.Row(headerRowNumber);
        foreach (var cell in headerRow.CellsUsed())
        {
            var text = cell.GetString().Trim();
            if (ExpectedHeaders.Contains(text) && !columnIndexByHeader.ContainsKey(text))
            {
                columnIndexByHeader[text] = cell.Address.ColumnNumber;
            }
        }

        var rows = new List<ParsedImportRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRowNumber;

        for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty())
            {
                continue;
            }

            var mainProjectName = GetText(row, columnIndexByHeader, "المشروع الرئيسى");
            var subProjectName = GetText(row, columnIndexByHeader, "المشروع الفرعى");
            if (string.IsNullOrWhiteSpace(mainProjectName) && string.IsNullOrWhiteSpace(subProjectName))
            {
                continue;
            }

            rows.Add(new ParsedImportRow
            {
                RowIndex = rowNumber,
                MainProgramName = GetText(row, columnIndexByHeader, "البرنامج الرئيسي"),
                SubProgramName = GetText(row, columnIndexByHeader, "البرنامج الفرعي"),
                MainProjectCode = GetText(row, columnIndexByHeader, "كود المشروع الرئيسى"),
                MainProjectName = mainProjectName,
                ProjectLevelName = GetText(row, columnIndexByHeader, "مستوى المشروع"),
                ExecutiveAgencyName = GetText(row, columnIndexByHeader, "الجهة المنفذة"),
                MarkazName = GetText(row, columnIndexByHeader, "المركز"),
                SubProjectCode = GetText(row, columnIndexByHeader, "كود المشروع"),
                SubProjectName = subProjectName,
                ComponentTypeName = GetText(row, columnIndexByHeader, "المكوّن العيني"),
                BankFunding = GetDecimal(row, columnIndexByHeader, "بنك"),
                SelfFunding = GetDecimal(row, columnIndexByHeader, "ذاتي"),
                AccountingUnitName = GetText(row, columnIndexByHeader, "الوحدة الحسابية"),
            });
        }

        if (rows.Count == 0)
        {
            throw new BusinessRuleException("لم يتم العثور على أي صفوف بيانات في الملف");
        }

        var codedCount = rows.Count(r => !string.IsNullOrWhiteSpace(r.SubProjectCode));
        ImportMode mode;
        if (codedCount == 0)
        {
            mode = ImportMode.Suggested;
        }
        else if (codedCount == rows.Count)
        {
            mode = ImportMode.Approved;
        }
        else
        {
            throw new BusinessRuleException(
                "الملف يحتوي على مشروعات بأكواد وأخرى بدون أكواد — يجب أن يكون الملف إما خطة مقترحة (بدون أكواد) أو خطة معتمدة (بكل الأكواد)");
        }

        return new ParsedImportFile { Mode = mode, Rows = rows };
    }

    private static int FindHeaderRow(IXLWorksheet worksheet)
    {
        var lastRowToScan = Math.Min(10, worksheet.LastRowUsed()?.RowNumber() ?? 1);
        for (var rowNumber = 1; rowNumber <= lastRowToScan; rowNumber++)
        {
            var texts = worksheet.Row(rowNumber).CellsUsed().Select(c => c.GetString().Trim()).ToHashSet();
            if (ExpectedHeaders.All(h => texts.Contains(h)))
            {
                return rowNumber;
            }
        }

        return -1;
    }

    private static string GetText(IXLRow row, Dictionary<string, int> columnIndexByHeader, string header)
    {
        if (!columnIndexByHeader.TryGetValue(header, out var columnIndex))
        {
            return string.Empty;
        }

        return row.Cell(columnIndex).GetString().Trim();
    }

    private static decimal GetDecimal(IXLRow row, Dictionary<string, int> columnIndexByHeader, string header)
    {
        if (!columnIndexByHeader.TryGetValue(header, out var columnIndex))
        {
            return 0m;
        }

        var cell = row.Cell(columnIndex);
        return cell.TryGetValue<decimal>(out var value) ? value : 0m;
    }
}
```

- [ ] **Step 7: Register the new services**

In `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`, add `using SmartInvest.Application.Services.Import;` and `using SmartInvest.Infrastructure.Services;` to the top of the file, then add inside `AddInfrastructure`, right after the existing `services.AddScoped<IUnitOfWork, UnitOfWork>();` line:

```csharp
        services.AddMemoryCache();
        services.AddSingleton<ImportSessionStore>();
        services.AddScoped<IExcelImportParser, ExcelImportParser>();
```

- [ ] **Step 8: Build**

```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 9: Commit**

```bash
git add Backend/src/SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj Backend/src/SmartInvest.Application/SmartInvest.Application.csproj Backend/src/SmartInvest.Application/DTOs/ImportDtos.cs Backend/src/SmartInvest.Application/Services/Import/ Backend/src/SmartInvest.Application/Interfaces/IExcelImportParser.cs Backend/src/SmartInvest.Infrastructure/Services/ExcelImportParser.cs Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs
git commit -m "feat: add Excel parsing infrastructure for project import

ClosedXML-based parser reads the plan Excel's fixed column layout,
detects suggested-vs-approved mode from whether كود المشروع is filled
(rejecting a mixed file outright), and caches the parsed rows server-
side behind a short-lived importId so the commit step doesn't need
the file re-uploaded. Nothing wires into it yet."
```

---

### Task 2: Suggested-Mode Preview + Commit

**Files:**
- Create: `Backend/src/SmartInvest.Application/Services/Import/SuggestedPlanImportService.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `ParsedImportFile`/`ParsedImportRow`/`ImportMode` (Task 1), `ImportSessionStore` (Task 1), all `ImportDtos.cs` types (Task 1), `ILookupService` (existing — `GetMarkazAsync` not needed, uses `IGenericRepository<Markaz>` directly for reads + `CreateMarkazAsync`/`CreateMainProgramAsync`/`CreateSubProgramAsync`/`CreateComponentTypeAsync`/`CreateProjectLevelAsync`/`CreateAccountingUnitAsync` for "new" creations), `IExecutiveAgencyService.CreateAsync` (existing), `IMainProjectRepository`/`ISubProjectRepository` (existing, `AddAsync` inherited from `IGenericRepository<T>`), `IPlanRepo` (existing, extended below), `IGenericRepository<PlanProject>`/`IGenericRepository<FinancialYear>`/`IGenericRepository<Governorate>`/`IGenericRepository<ProjectPriority>`/`IGenericRepository<ProjectStatus>` (existing).
- Produces: `SuggestedPlanImportService.PreviewAsync(ParsedImportFile, CancellationToken) → SuggestedImportPreviewDto`, `SuggestedPlanImportService.CommitAsync(ParsedImportFile, ImportCommitDto, CancellationToken) → ImportCommitResultDto`. Task 4's `ImportService` facade calls both.

- [ ] **Step 1: Extend `IPlanRepo` with a financial-year+status lookup**

In `Backend/src/SmartInvest.Domain/Interfaces/IPlanRepo.cs`, add one method to the interface:

```csharp
        Plan? GetByFinancialYearAndStatus(int financialYearId, PlanStatus status);
```

Full file after the change:

```csharp
namespace SmartInvest.Domain.Interfaces
{
    public interface IPlanRepo : IGenericRepository<Plan>
    {
        Plan? GetPlanWithProjectsById(int planId);
        Plan? GetCurrentPlan();
        List<Plan>? GetPlanByStatusAndName(PlanStatus? Status, string? PlanName);
        Plan? GetByFinancialYearAndStatus(int financialYearId, PlanStatus status);
        Task AddExistingProject(int PlanId, int ProjectId);
        Task AddProject(int PlanId, SubProject project);
        void DeleteProjectFromPlan(int PlanId, int ProjectId);
    }
}
```

In `Backend/src/SmartInvest.Infrastructure/Repositories/PlanRepo.cs`, add the implementation right after `GetPlanByStatusAndName`'s closing brace:

```csharp
            public Plan? GetByFinancialYearAndStatus(int financialYearId, PlanStatus status)
            {
                return Context.Plans
                    .Include(p => p.FinancialYear)
                    .FirstOrDefault(p => p.FinancialYearId == financialYearId && p.PlanStatus == status);
            }
```

- [ ] **Step 2: Implement `SuggestedPlanImportService`**

Create `Backend/src/SmartInvest.Application/Services/Import/SuggestedPlanImportService.cs`:

```csharp
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

        var mainProjectGroups = GroupByMainProject(file.Rows, dto.MainProjectCodeConflicts, new List<MainProjectCodeResolutionDto>());
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
            dto.MainProgramResolutions, _mainProgramRepository, x => x.ProgramName,
            async name => (await _lookupService.CreateMainProgramAsync(new CreateNamedLookupDto { Name = name }, cancellationToken)).Id,
            cancellationToken);
        var subProgramIdByName = await ResolveSubProgramAsync(dto.SubProgramResolutions, mainProgramIdByName, cancellationToken);
        var agencyIdByName = await ResolveNamedLookupAsync(
            dto.AgencyResolutions, _agencyRepository, x => x.AgencyName,
            async name => (await _agencyService.CreateAsync(new CreateExecutiveAgencyDto { AgencyName = name, Phone = string.Empty, Email = string.Empty, Address = string.Empty }, cancellationToken)).Id,
            cancellationToken);
        var projectLevelIdByName = await ResolveNamedLookupAsync(
            dto.ProjectLevelResolutions, _projectLevelRepository, x => x.Name,
            async name => (await _lookupService.CreateProjectLevelAsync(new CreateNamedLookupDto { Name = name }, cancellationToken)).Id,
            cancellationToken);
        var componentTypeIdByName = await ResolveNamedLookupAsync(
            dto.ComponentTypeResolutions, _componentTypeRepository, x => x.Name,
            async name => (await _lookupService.CreateComponentTypeAsync(new CreateNamedLookupDto { Name = name }, cancellationToken)).Id,
            cancellationToken);
        var accountingUnitIdByName = await ResolveNamedLookupAsync(
            dto.AccountingUnitResolutions, _accountingUnitRepository, x => x.Name,
            async name => (await _lookupService.CreateAccountingUnitAsync(new CreateNamedLookupDto { Name = name }, cancellationToken)).Id,
            cancellationToken);

        var defaultPriorityId = (await _priorityRepository.FirstOrDefaultAsync(x => x.Priority == "منخفضة", cancellationToken))?.Id
            ?? throw new BusinessRuleException("أولوية «منخفضة» الافتراضية غير موجودة في قاعدة البيانات");
        var defaultStatusId = (await _statusRepository.FirstOrDefaultAsync(x => x.StatusName == "جديد", cancellationToken))?.StatusId
            ?? throw new BusinessRuleException("حالة «جديد» الافتراضية غير موجودة في قاعدة البيانات");

        var mainProjectGroups = GroupByMainProject(file.Rows, DetectCodeConflicts(file.Rows), dto.MainProjectCodeResolutions);

        var result = new ImportCommitResultDto { Mode = "Suggested" };
        var createdSubProjects = new List<SubProject>();

        foreach (var group in mainProjectGroups)
        {
            if (!mainProgramIdByName.TryGetValue(group.Rows[0].MainProgramName.Trim(), out var mainProgramId))
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

            var mainProject = new MainProject
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

            foreach (var row in group.Rows)
            {
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

                    var subProject = new SubProject
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
        public List<ParsedImportRow> Rows { get; set; } = new();
    }

    private static List<MainProjectGroup> GroupByMainProject(
        List<ParsedImportRow> rows,
        List<MainProjectCodeConflictDto> conflicts,
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

                return new MainProjectGroup { Code = code, MainProjectName = name, Rows = g.ToList() };
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
        List<ImportResolutionDto> resolutions, Dictionary<string, int> mainProgramIdByName, CancellationToken cancellationToken)
    {
        var existing = await _subProgramRepository.GetAllAsync(cancellationToken);
        var result = existing.ToDictionary(x => (x.ProgramId, x.SubProgramName), x => x.SubProgramId);

        foreach (var resolution in resolutions.Where(r => r.CreateNew))
        {
            var name = resolution.Name.Trim();
            foreach (var mainProgramId in mainProgramIdByName.Values.Distinct())
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
            if (match != null)
            {
                result[(match.ProgramId, resolution.Name.Trim())] = match.SubProgramId;
            }
        }

        return result;
    }

    private static async Task<Dictionary<string, int>> ResolveNamedLookupAsync<T>(
        List<ImportResolutionDto> resolutions,
        IGenericRepository<T> repository,
        Func<T, string> nameSelector,
        Func<string, Task<int>> createNew,
        CancellationToken cancellationToken)
        where T : class
    {
        var existing = await repository.GetAllAsync(cancellationToken);
        var idSelector = typeof(T).GetProperty("Id") ?? typeof(T).GetProperty($"{typeof(T).Name}Id");
        var result = new Dictionary<string, int>();
        foreach (var item in existing)
        {
            var id = (int)idSelector!.GetValue(item)!;
            result[nameSelector(item)] = id;
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
            var match = existing.FirstOrDefault(x => (int)idSelector!.GetValue(x)! == resolution.ExistingId!.Value);
            if (match != null)
            {
                result[resolution.Name.Trim()] = resolution.ExistingId!.Value;
            }
        }

        return result;
    }
}
```

Note on `ResolveNamedLookupAsync`'s reflection-based id lookup: `ComponentType`/`ProjectLevel`/`AccountingUnit` all expose `Id` (confirmed identical shape to `Unit`/`AccountingUnit` seen in the measurement-units work), while `ExecutiveAgency` exposes `ExecutiveAgencyId` — the `?? typeof(T).GetProperty($"{typeof(T).Name}Id")` fallback covers that. This keeps one generic helper instead of 4 near-identical copies; if a future entity uses a third naming convention, extend the fallback chain rather than duplicating the method.

- [ ] **Step 3: Register `SuggestedPlanImportService`**

In `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`, add right after the `IExcelImportParser` line added in Task 1 Step 7:

```csharp
        services.AddScoped<SmartInvest.Application.Services.Import.SuggestedPlanImportService>();
```

- [ ] **Step 4: Build**

```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.` (There is no controller wired up yet — this task is not independently testable via HTTP; Task 4 wires the controller and is where this task's behavior gets its first live check. Confirm only that it compiles cleanly here.)

- [ ] **Step 5: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Interfaces/IPlanRepo.cs Backend/src/SmartInvest.Infrastructure/Repositories/PlanRepo.cs Backend/src/SmartInvest.Application/Services/Import/SuggestedPlanImportService.cs Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs
git commit -m "feat: suggested-mode Excel import — 7-category reconciliation + commit

Preview detects unmatched Markaz/MainProgram/SubProgram/Agency/
ProjectLevel/ComponentType/AccountingUnit names plus main-project-code
conflicts. Commit creates the reconciled lookups, then Main Projects
(grouped by code+name, never merged unless staff explicitly resolves
a conflict) and Sub Projects (always new, entities built directly to
bypass SubProjectService's name-uniqueness guard — re-importing the
same file must produce fresh duplicates, not fail), best-effort per
row, then finds-or-creates a Suggested Plan for the financial year and
links every created sub-project to it. Not yet reachable via HTTP."
```

---

### Task 3: Approved-Mode Preview + Commit

**Files:**
- Modify: `Backend/src/SmartInvest.Domain/Interfaces/IMainProjectRepository.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Repositories/MainProjectRepository.cs`
- Modify: `Backend/src/SmartInvest.Domain/Interfaces/ISubProjectRepository.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Repositories/SubProjectRepository.cs`
- Create: `Backend/src/SmartInvest.Application/Services/Import/ApprovedPlanImportService.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: everything Task 2 consumes, plus `ISubProjectService.ApproveAsync` (existing, reused as-is to approve a matched row — not duplicated), `IPlanService.ApproveAsync` (existing, reused to flip an existing Suggested plan). New repository methods this task adds: `IMainProjectRepository.FindByNameAsync(string name) → IReadOnlyList<MainProject>`, `ISubProjectRepository.FindByNameWithinMainProjectAsync(string name, int mainProjectId) → IReadOnlyList<SubProject>`.
- Produces: `ApprovedPlanImportService.PreviewAsync(ParsedImportFile, CancellationToken) → ApprovedImportPreviewDto`, `ApprovedPlanImportService.CommitAsync(ParsedImportFile, ImportCommitDto, CancellationToken) → ImportCommitResultDto`. Task 4's facade calls both.

- [ ] **Step 1: Add the main-project name-lookup method**

In `Backend/src/SmartInvest.Domain/Interfaces/IMainProjectRepository.cs`, add:

```csharp
    Task<IReadOnlyList<MainProject>> FindByNameAsync(string name, CancellationToken cancellationToken = default);
```

Full file after the change:

```csharp
using SmartInvest.Domain.Entities;

namespace SmartInvest.Domain.Interfaces;

public interface IMainProjectRepository : IGenericRepository<MainProject>
{
    Task<IReadOnlyList<MainProject>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);

    Task<MainProject?> GetWithSubProjectsAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string? code, int? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MainProject>> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
```

In `Backend/src/SmartInvest.Infrastructure/Repositories/MainProjectRepository.cs`, add the implementation after `CodeExistsAsync`:

```csharp
    public async Task<IReadOnlyList<MainProject>> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        return await DbSet
            .Where(x => x.MainProjectName == trimmed)
            .ToListAsync(cancellationToken);
    }
```

(Add `using Microsoft.EntityFrameworkCore;` if not already present in that file — it already is, per the file's existing `.Include`/`.ToListAsync` usage.)

- [ ] **Step 2: Add the sub-project name-within-main-project lookup method**

In `Backend/src/SmartInvest.Domain/Interfaces/ISubProjectRepository.cs`, add:

```csharp
    Task<IReadOnlyList<SubProject>> FindByNameWithinMainProjectAsync(string name, int mainProjectId, CancellationToken cancellationToken = default);
```

Full file after the change:

```csharp
using SmartInvest.Domain.Entities;

namespace SmartInvest.Domain.Interfaces;

public interface ISubProjectRepository : IGenericRepository<SubProject>
{
    Task<SubProject?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SubProject> Items, int TotalCount)> SearchAsync(int? mainProjectId, int? mainProgramId, int? subProgramId, int? markazId, int? priorityId, int? statusId, int? financialYearId, string? searchTerm, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubProject>> GetByExecutiveAgencyAsync(int executiveAgencyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubProject>> FindByNameWithinMainProjectAsync(string name, int mainProjectId, CancellationToken cancellationToken = default);
}
```

In `Backend/src/SmartInvest.Infrastructure/Repositories/SubProjectRepository.cs`, add the implementation after `NameExistsAsync`:

```csharp
    public async Task<IReadOnlyList<SubProject>> FindByNameWithinMainProjectAsync(string name, int mainProjectId, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        return await DbSet
            .Where(x => x.SubProjectName == trimmed && x.MainProjectId == mainProjectId)
            .ToListAsync(cancellationToken);
    }
```

- [ ] **Step 3: Implement `ApprovedPlanImportService`**

Create `Backend/src/SmartInvest.Application/Services/Import/ApprovedPlanImportService.cs`. Per the design doc §4.1: a row is matched only when its main-project name resolves to exactly one `MainProject`, and its sub-project name resolves to exactly one `SubProject` within that main project — zero or multiple matches on either side is an unresolved row (§6's explicit "reject as unresolved rather than guessing" tie-break). Per §4.1/§4.2/§4.3: on commit, only `SubProjectCode`/`IsApproved`/`ApprovedAt`/`StatusId` change (via the existing `ISubProjectService.ApproveAsync`, unchanged), and creating a new row from an unresolved reconciliation choice creates it already-approved directly (bypassing the suggested state).

```csharp
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
                    var mainProject = mainProjects.FirstOrDefault();
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
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        result.MainProjectsCreated++;
                    }

                    var subProject = new SubProject
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
            }
        }

        var plan = _planRepo.GetByFinancialYearAndStatus(dto.FinancialYearId, PlanStatus.Suggested);

        Plan resultPlan;
        if (plan != null)
        {
            resultPlan = await ApprovePlanAsync(plan.PlanId, approvalDate, cancellationToken);
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

    private async Task<Plan> ApprovePlanAsync(int planId, DateTime approvalDate, CancellationToken cancellationToken)
    {
        var plan = _planRepo.GetPlanWithProjectsById(planId) ?? throw new NotFoundException($"الخطة رقم {planId} غير موجودة");
        if (plan.ApprovalDate.HasValue)
        {
            return plan;
        }

        plan.ApprovalDate = approvalDate;
        plan.PlanStatus = PlanStatus.Approved;
        _planRepo.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return plan;
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
```

- [ ] **Step 4: Register `ApprovedPlanImportService`**

In `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`, add right after the `SuggestedPlanImportService` line added in Task 2 Step 3:

```csharp
        services.AddScoped<SmartInvest.Application.Services.Import.ApprovedPlanImportService>();
```

- [ ] **Step 5: Build**

```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Interfaces/IMainProjectRepository.cs Backend/src/SmartInvest.Infrastructure/Repositories/MainProjectRepository.cs Backend/src/SmartInvest.Domain/Interfaces/ISubProjectRepository.cs Backend/src/SmartInvest.Infrastructure/Repositories/SubProjectRepository.cs Backend/src/SmartInvest.Application/Services/Import/ApprovedPlanImportService.cs Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs
git commit -m "feat: approved-mode Excel import — name-match + approve + plan flip

Preview matches each row's main+sub project name against existing
records (exact match; zero or multiple hits is unresolved, never
guessed). Commit approves matches via the existing SubProjectService.
ApproveAsync (only code/approval fields change, reusing tested logic
rather than duplicating it), creates already-approved rows for
reconciled create-new choices, then approves the financial year's
existing Suggested plan via the existing PlanService approval logic
or creates one directly as Approved if none exists. Not yet reachable
via HTTP."
```

---

### Task 4: Import Controller + Facade

**Files:**
- Create: `Backend/src/SmartInvest.Application/Services/Import/ImportService.cs`
- Modify: `Backend/src/SmartInvest.Application/Interfaces/IImportService.cs` (create — listed here since it's the facade's own contract, small enough to fold into this task rather than Task 1)
- Create: `Backend/src/SmartInvest.API/Controllers/ImportController.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `IExcelImportParser`, `ImportSessionStore`, `SuggestedPlanImportService`, `ApprovedPlanImportService` (all prior tasks).
- Produces: `POST api/subprojects/import/preview` (multipart form: `file`, `financialYearId`) → `ImportPreviewResultDto`. `POST api/subprojects/import/commit` (JSON body `ImportCommitDto`) → `ImportCommitResultDto`. Task 5 (frontend service) consumes these two routes and both DTO shapes exactly.

- [ ] **Step 1: Add the facade interface**

Create `Backend/src/SmartInvest.Application/Interfaces/IImportService.cs`:

```csharp
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IImportService
{
    Task<ImportPreviewResultDto> PreviewAsync(Stream fileStream, CancellationToken cancellationToken = default);

    Task<ImportCommitResultDto> CommitAsync(ImportCommitDto dto, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Implement the facade**

Create `Backend/src/SmartInvest.Application/Services/Import/ImportService.cs`:

```csharp
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;

namespace SmartInvest.Application.Services.Import;

public class ImportService : IImportService
{
    private readonly IExcelImportParser _parser;
    private readonly ImportSessionStore _sessionStore;
    private readonly SuggestedPlanImportService _suggestedService;
    private readonly ApprovedPlanImportService _approvedService;

    public ImportService(
        IExcelImportParser parser,
        ImportSessionStore sessionStore,
        SuggestedPlanImportService suggestedService,
        ApprovedPlanImportService approvedService)
    {
        _parser = parser;
        _sessionStore = sessionStore;
        _suggestedService = suggestedService;
        _approvedService = approvedService;
    }

    public async Task<ImportPreviewResultDto> PreviewAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        var file = _parser.Parse(fileStream);
        var importId = _sessionStore.Save(file);

        var result = new ImportPreviewResultDto
        {
            ImportId = importId,
            Mode = file.Mode.ToString(),
        };

        if (file.Mode == ImportMode.Suggested)
        {
            result.Suggested = await _suggestedService.PreviewAsync(file, cancellationToken);
        }
        else
        {
            result.Approved = await _approvedService.PreviewAsync(file, cancellationToken);
        }

        return result;
    }

    public async Task<ImportCommitResultDto> CommitAsync(ImportCommitDto dto, CancellationToken cancellationToken = default)
    {
        var file = _sessionStore.Get(dto.ImportId)
            ?? throw new BusinessRuleException("انتهت صلاحية جلسة الاستيراد — برجاء رفع الملف مرة أخرى");

        var result = file.Mode == ImportMode.Suggested
            ? await _suggestedService.CommitAsync(file, dto, cancellationToken)
            : await _approvedService.CommitAsync(file, dto, cancellationToken);

        _sessionStore.Remove(dto.ImportId);

        return result;
    }
}
```

- [ ] **Step 3: Add the controller**

Create `Backend/src/SmartInvest.API/Controllers/ImportController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/subprojects/import")]
[Authorize(Roles = Roles.PlanningStaff)]
public class ImportController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private readonly IImportService _importService;

    public ImportController(IImportService importService)
    {
        _importService = importService;
    }

    [HttpPost("preview")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<ActionResult<ImportPreviewResultDto>> Preview(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "برجاء اختيار ملف Excel" });
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "يجب أن يكون الملف بصيغة .xlsx" });
        }

        await using var stream = file.OpenReadStream();
        var result = await _importService.PreviewAsync(stream, cancellationToken);
        return Ok(result);
    }

    [HttpPost("commit")]
    public async Task<ActionResult<ImportCommitResultDto>> Commit(ImportCommitDto dto, CancellationToken cancellationToken)
    {
        var result = await _importService.CommitAsync(dto, cancellationToken);
        return Ok(result);
    }
}
```

- [ ] **Step 4: Register `IImportService`**

In `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`, add right after the `ApprovedPlanImportService` line added in Task 3 Step 4:

```csharp
        services.AddScoped<IImportService, ImportService>();
```

- [ ] **Step 5: Build**

```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 6: Manual check via Swagger — suggested mode**

Using the real sample file (`نسخة من الخطة المعتمدة.xlsx`, `data` sheet — or any `.xlsx` with the same 13-column header set and every `كود المشروع` cell blank): `POST api/subprojects/import/preview` (multipart, field name `file`) → confirm `mode: "Suggested"` and a populated `suggested` object with `mainProjectCount`/`subProjectCount` and any unresolved-name lists (all 7 categories) that apply to your test file. Then `POST api/subprojects/import/commit` with `{ importId, financialYearId, markazResolutions: [...], mainProgramResolutions: [...], ... }` (an empty array for any category with no unresolved names) — confirm `mainProjectsCreated`/`subProjectsCreated` are non-zero, `planId`/`planName`/`planStatus: "Suggested"` are populated, and `GET api/plans/{planId}` shows the newly created sub-projects linked. Re-run the exact same preview+commit a second time with the same file: confirm it succeeds again with fresh duplicate Main/Sub Projects (not a failure, not a silent no-op) and the SAME plan id is reused (not a second Suggested plan for the same financial year).

- [ ] **Step 7: Manual check via Swagger — approved mode**

Build (or hand-edit) a version of the same file with every `كود المشروع` cell filled with a real code, using main+sub project names matching what Step 6 just created. `POST api/subprojects/import/preview` → confirm `mode: "Approved"` and `approved.matchedCount` reflects how many rows matched. `POST api/subprojects/import/commit` with `{ importId, financialYearId, approvalDate: "2026-08-01", rowResolutions: [...] }` (empty array if every row matched) → confirm `subProjectsApproved` is non-zero, and `GET api/subprojects/{id}` on one of the affected sub-projects shows `isApproved: true` with the assigned code. Confirm the SAME plan id from Step 6 now shows `planStatus: "Approved"` with an `approvalDate` set (not a second, separate plan).

- [ ] **Step 8: Manual check — mixed file rejected**

Upload a file with some rows coded and some not. Confirm `POST .../preview` returns 400 with the exact Arabic mixed-file message from the design doc §2, before any reconciliation data is returned.

- [ ] **Step 9: Commit**

```bash
git add Backend/src/SmartInvest.Application/Interfaces/IImportService.cs Backend/src/SmartInvest.Application/Services/Import/ImportService.cs Backend/src/SmartInvest.API/Controllers/ImportController.cs Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs
git commit -m "feat: wire Excel import preview/commit endpoints

api/subprojects/import/preview (multipart upload, 10MB limit) and
api/subprojects/import/commit (JSON) are now live, both PlanningStaff-
gated. The facade auto-branches by the parser's detected mode -
callers never need to know which mode a file is until the preview
response tells them."
```

---

### Task 5: Frontend Models + Import Service

**Files:**
- Modify: `Frontend/src/app/core/models/project.models.ts`
- Create: `Frontend/src/app/core/services/import.service.ts`

**Interfaces:**
- Consumes: nothing (leaf models/service layer).
- Produces: every TypeScript interface mirroring Task 1/4's backend DTOs, plus `ImportService.preview(file: File, financialYearId: number)` / `ImportService.commit(dto: ImportCommit)`. Task 6 (wizard component) consumes both.

- [ ] **Step 1: Add the import models**

In `Frontend/src/app/core/models/project.models.ts`, add at the end of the file:

```typescript
export interface UnresolvedName {
  name: string;
  rowCount: number;
}

export interface MainProjectCodeConflictOption {
  mainProjectName: string;
  mainProgramName: string;
}

export interface MainProjectCodeConflict {
  code: string;
  options: MainProjectCodeConflictOption[];
}

export interface SuggestedImportPreview {
  mainProjectCount: number;
  subProjectCount: number;
  unresolvedMarkaz: UnresolvedName[];
  unresolvedMainPrograms: UnresolvedName[];
  unresolvedSubPrograms: UnresolvedName[];
  unresolvedAgencies: UnresolvedName[];
  unresolvedProjectLevels: UnresolvedName[];
  unresolvedComponentTypes: UnresolvedName[];
  unresolvedAccountingUnits: UnresolvedName[];
  mainProjectCodeConflicts: MainProjectCodeConflict[];
}

export interface UnresolvedImportRow {
  rowIndex: number;
  mainProjectName: string;
  subProjectName: string;
  code: string;
}

export interface ApprovedImportPreview {
  matchedCount: number;
  unresolvedRows: UnresolvedImportRow[];
}

export interface ImportPreviewResult {
  importId: string;
  mode: 'Suggested' | 'Approved';
  suggested: SuggestedImportPreview | null;
  approved: ApprovedImportPreview | null;
}

export interface ImportResolution {
  name: string;
  createNew: boolean;
  existingId: number | null;
}

export interface MainProjectCodeResolution {
  code: string;
  chosenMainProjectName: string;
  chosenMainProgramName: string;
}

export interface ImportRowResolution {
  rowIndex: number;
  createNew: boolean;
  existingSubProjectId: number | null;
}

export interface ImportCommit {
  importId: string;
  financialYearId: number;
  approvalDate?: string | null;
  markazResolutions: ImportResolution[];
  mainProgramResolutions: ImportResolution[];
  subProgramResolutions: ImportResolution[];
  agencyResolutions: ImportResolution[];
  projectLevelResolutions: ImportResolution[];
  componentTypeResolutions: ImportResolution[];
  accountingUnitResolutions: ImportResolution[];
  mainProjectCodeResolutions: MainProjectCodeResolution[];
  rowResolutions: ImportRowResolution[];
}

export interface ImportRowFailure {
  name: string;
  reason: string;
}

export interface ImportCommitResult {
  mode: 'Suggested' | 'Approved';
  mainProjectsCreated: number;
  subProjectsCreated: number;
  subProjectsApproved: number;
  subProjectsCreatedAndApproved: number;
  failed: ImportRowFailure[];
  planId: number;
  planName: string;
  planStatus: string;
}
```

- [ ] **Step 2: Add the import service**

Create `Frontend/src/app/core/services/import.service.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ImportCommit, ImportCommitResult, ImportPreviewResult } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class ImportService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/subprojects/import`;

  preview(file: File): Observable<ImportPreviewResult> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ImportPreviewResult>(`${this.base}/preview`, formData);
  }

  commit(dto: ImportCommit): Observable<ImportCommitResult> {
    return this.http.post<ImportCommitResult>(`${this.base}/commit`, dto);
  }
}
```

(No `financialYearId` is sent to `preview` — the parser doesn't need it, only `commit` does, per the DTO shapes in Task 1/4. `HttpClient` sets the `multipart/form-data` boundary header automatically from `FormData`; do not set `Content-Type` manually.)

- [ ] **Step 3: Type-check**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Frontend/src/app/core/models/project.models.ts Frontend/src/app/core/services/import.service.ts
git commit -m "feat: frontend models + service for Excel project import"
```

---

### Task 6: Import Wizard Component + Wire Into Projects Page

**Files:**
- Create: `Frontend/src/app/features/projects/excel-import-wizard.ts`
- Modify: `Frontend/src/app/features/projects/projects.ts`
- Modify: `Frontend/src/app/features/projects/projects.html`
- Modify: `Frontend/src/app/features/projects/projects.css`

**Interfaces:**
- Consumes: `ImportService` (Task 5), `FinancialYearsService`/`FinancialYear` (existing, already used by `projects.ts`), all import models (Task 5).
- Produces: `ExcelImportWizard` standalone component with `[open]`/`[financialYearId]` inputs and `(close)`/`(saved)` outputs, matching the exact declarative pattern `app-sub-project-form`/`app-main-project-form` already use in `projects.html`.

- [ ] **Step 1: Create the wizard component**

Create `Frontend/src/app/features/projects/excel-import-wizard.ts`. Single component, internal step signal (`'upload' | 'reconcile' | 'confirm' | 'result'`), branches its Step 2/3 content by `preview()?.mode` once the preview call returns — matching the design doc §5's "single entry point, auto-branching" requirement and this codebase's established inline-template, Signals-based modal pattern (e.g. `sub-project-form.ts`).

```typescript
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ImportService } from '../../core/services/import.service';
import {
  ImportCommit,
  ImportCommitResult,
  ImportPreviewResult,
  ImportResolution,
  ImportRowResolution,
  MainProjectCodeResolution,
} from '../../core/models/project.models';

type Step = 'upload' | 'reconcile' | 'confirm' | 'result';

@Component({
  selector: 'app-excel-import-wizard',
  imports: [FormsModule],
  template: `
    @if (open()) {
      <div class="si-overlay" (click)="close.emit()">
        <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(720px,100%)">
          <div class="si-modal-head">
            <div class="grow"><h3>استيراد مشروعات من Excel</h3></div>
            <button class="si-x" (click)="close.emit()" aria-label="إغلاق">×</button>
          </div>
          <div class="si-modal-body">
            @if (error()) { <div class="si-err">{{ error() }}</div> }

            @if (step() === 'upload') {
              <div class="si-fld full">
                <label>ملف الخطة (Excel) <span class="req">*</span></label>
                <input type="file" accept=".xlsx" (change)="onFileSelected($event)" />
                <p class="hint">ارفع ملف بصيغة .xlsx يحتوي على ورقة بيانات واحدة فقط للمشروعات (بدون أوراق إضافية مكررة أو غير مرتبطة).</p>
              </div>
            }

            @if (step() === 'reconcile' || step() === 'confirm') {
              <div class="mode-banner">
                @if (preview()?.mode === 'Suggested') { تم اكتشاف: خطة مقترحة (لا يوجد أكواد مشروعات) }
                @else { تم اكتشاف: خطة معتمدة (كل الصفوف تحتوي على كود مشروع) }
              </div>
            }

            @if (step() === 'reconcile' && preview()?.mode === 'Suggested' && preview()?.suggested; as s) {
              @for (group of suggestedCategories(s); track group.key) {
                @if (group.items.length > 0) {
                  <div class="si-fld full">
                    <label>{{ group.label }}</label>
                    @for (item of group.items; track item.name) {
                      <div class="recon-row">
                        <span class="recon-name">{{ item.name }}</span>
                        <span class="recon-count">({{ item.rowCount }} صف)</span>
                        <label class="recon-choice"><input type="radio" [name]="group.key + '-' + item.name" [checked]="isNew(group.key, item.name)" (change)="setNew(group.key, item.name)" /> جديد</label>
                        <label class="recon-choice"><input type="radio" [name]="group.key + '-' + item.name" [checked]="!isNew(group.key, item.name)" (change)="setNew(group.key, item.name, false)" /> نفس القيمة</label>
                      </div>
                    }
                  </div>
                }
              }
              @if (s.mainProjectCodeConflicts.length > 0) {
                <div class="si-fld full">
                  <label>تعارض أكواد مشروعات رئيسية</label>
                  @for (conflict of s.mainProjectCodeConflicts; track conflict.code) {
                    <div class="recon-row">
                      <span class="recon-name">كود {{ conflict.code }}</span>
                      @for (opt of conflict.options; track opt.mainProjectName) {
                        <label class="recon-choice">
                          <input type="radio" [name]="'code-' + conflict.code" (change)="chooseCodeOption(conflict.code, opt)" />
                          {{ opt.mainProjectName }} ({{ opt.mainProgramName }})
                        </label>
                      }
                      <label class="recon-choice"><input type="radio" [name]="'code-' + conflict.code" [checked]="true" (change)="clearCodeChoice(conflict.code)" /> إبقاؤهما منفصلين</label>
                    </div>
                  }
                </div>
              }
            }

            @if (step() === 'reconcile' && preview()?.mode === 'Approved' && preview()?.approved; as a) {
              @if (a.unresolvedRows.length === 0) {
                <p>تم مطابقة جميع الصفوف بنجاح.</p>
              } @else {
                @for (row of a.unresolvedRows; track row.rowIndex) {
                  <div class="recon-row">
                    <span class="recon-name">{{ row.mainProjectName }} / {{ row.subProjectName }} (كود {{ row.code }})</span>
                    <label class="recon-choice"><input type="radio" [name]="'row-' + row.rowIndex" [checked]="true" (change)="setRowCreateNew(row.rowIndex)" /> إنشاء جديد (معتمد)</label>
                    <label class="recon-choice"><input type="radio" [name]="'row-' + row.rowIndex" (change)="setRowExisting(row.rowIndex, existingSubProjectId(row.rowIndex))" /> ربط بمشروع موجود، رقم:</label>
                    <input type="number" [ngModel]="existingSubProjectId(row.rowIndex)" (ngModelChange)="setRowExisting(row.rowIndex, $event)" style="width:90px" />
                  </div>
                }
              }
            }

            @if (step() === 'confirm') {
              <p>
                @if (preview()?.mode === 'Suggested') {
                  سيتم إنشاء {{ preview()?.suggested?.mainProjectCount }} مشروع رئيسي و{{ preview()?.suggested?.subProjectCount }} مشروع فرعي ضمن خطة مقترحة للسنة المالية المحددة.
                } @else {
                  سيتم اعتماد {{ preview()?.approved?.matchedCount }} مشروع مطابق.
                }
              </p>
              @if (preview()?.mode === 'Approved') {
                <div class="si-fld"><label>تاريخ الاعتماد <span class="req">*</span></label><input type="date" [ngModel]="approvalDate()" (ngModelChange)="approvalDate.set($event)" /></div>
              }
            }

            @if (step() === 'result' && result(); as r) {
              <p>تم الاستيراد بنجاح — الخطة: {{ r.planName }} ({{ r.planStatus }})</p>
              @if (r.mode === 'Suggested') {
                <p>مشروعات رئيسية: {{ r.mainProjectsCreated }} — مشروعات فرعية: {{ r.subProjectsCreated }}</p>
              } @else {
                <p>معتمدة: {{ r.subProjectsApproved }} — جديدة ومعتمدة: {{ r.subProjectsCreatedAndApproved }}</p>
              }
              @if (r.failed.length > 0) {
                <div class="si-err">
                  فشل استيراد {{ r.failed.length }} صف:
                  @for (f of r.failed; track f.name) { <div>{{ f.name }}: {{ f.reason }}</div> }
                </div>
              }
            }
          </div>
          <div class="si-modal-foot">
            @if (step() === 'upload') {
              <button class="si-btn primary" [disabled]="!selectedFile() || uploading()" (click)="submitUpload()">
                @if (uploading()) { جاري الرفع… } @else { رفع ومتابعة }
              </button>
            }
            @if (step() === 'reconcile') {
              <button class="si-btn primary" (click)="step.set('confirm')">التالي</button>
            }
            @if (step() === 'confirm') {
              <button class="si-btn primary" [disabled]="committing()" (click)="submitCommit()">
                @if (committing()) { جاري الحفظ… } @else { تأكيد الاستيراد }
              </button>
            }
            @if (step() === 'result') {
              <button class="si-btn primary" (click)="finish()">تم</button>
            }
            <button class="si-btn" (click)="close.emit()">إلغاء</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .hint { font-size: 12px; color: var(--muted); margin: 6px 0 0; }
    .mode-banner { background: var(--surface-2); border-radius: 9px; padding: 10px 12px; font-weight: 700; font-size: 13px; margin-bottom: 14px; }
    .recon-row { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; padding: 8px 0; border-bottom: 1px solid var(--line); font-size: 13px; }
    .recon-name { font-weight: 700; }
    .recon-count { color: var(--muted); font-size: 12px; }
    .recon-choice { display: flex; align-items: center; gap: 5px; font-size: 12.5px; }
  `],
})
export class ExcelImportWizard {
  private readonly importService = inject(ImportService);

  readonly open = input(false);
  readonly financialYearId = input.required<number | null>();
  readonly close = output<void>();
  readonly saved = output<void>();

  protected readonly step = signal<Step>('upload');
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly uploading = signal(false);
  protected readonly committing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly preview = signal<ImportPreviewResult | null>(null);
  protected readonly result = signal<ImportCommitResult | null>(null);
  protected readonly approvalDate = signal<string>(new Date().toISOString().slice(0, 10));

  private readonly resolutions: Record<string, Map<string, ImportResolution>> = {
    markaz: new Map(), mainProgram: new Map(), subProgram: new Map(), agency: new Map(),
    projectLevel: new Map(), componentType: new Map(), accountingUnit: new Map(),
  };
  private readonly codeResolutions = new Map<string, MainProjectCodeResolution>();
  private readonly rowResolutions = new Map<number, ImportRowResolution>();

  protected suggestedCategories(s: NonNullable<ImportPreviewResult['suggested']>) {
    return [
      { key: 'markaz', label: 'مراكز غير معروفة', items: s.unresolvedMarkaz },
      { key: 'mainProgram', label: 'برامج رئيسية غير معروفة', items: s.unresolvedMainPrograms },
      { key: 'subProgram', label: 'برامج فرعية غير معروفة', items: s.unresolvedSubPrograms },
      { key: 'agency', label: 'جهات منفذة غير معروفة', items: s.unresolvedAgencies },
      { key: 'projectLevel', label: 'مستويات مشروع غير معروفة', items: s.unresolvedProjectLevels },
      { key: 'componentType', label: 'مكوّنات عينية غير معروفة', items: s.unresolvedComponentTypes },
      { key: 'accountingUnit', label: 'وحدات حسابية غير معروفة', items: s.unresolvedAccountingUnits },
    ];
  }

  protected isNew(category: string, name: string): boolean {
    return this.resolutions[category].get(name)?.createNew ?? true;
  }

  protected setNew(category: string, name: string, createNew = true): void {
    this.resolutions[category].set(name, { name, createNew, existingId: null });
  }

  protected chooseCodeOption(code: string, opt: { mainProjectName: string; mainProgramName: string }): void {
    this.codeResolutions.set(code, { code, chosenMainProjectName: opt.mainProjectName, chosenMainProgramName: opt.mainProgramName });
  }

  protected clearCodeChoice(code: string): void {
    this.codeResolutions.delete(code);
  }

  protected existingSubProjectId(rowIndex: number): number | null {
    return this.rowResolutions.get(rowIndex)?.existingSubProjectId ?? null;
  }

  protected setRowCreateNew(rowIndex: number): void {
    this.rowResolutions.set(rowIndex, { rowIndex, createNew: true, existingSubProjectId: null });
  }

  protected setRowExisting(rowIndex: number, subProjectId: number | null): void {
    this.rowResolutions.set(rowIndex, { rowIndex, createNew: false, existingSubProjectId: subProjectId });
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
  }

  protected submitUpload(): void {
    const file = this.selectedFile();
    if (!file || this.uploading()) return;

    this.uploading.set(true);
    this.error.set(null);
    this.importService.preview(file).subscribe({
      next: (result) => {
        this.uploading.set(false);
        this.preview.set(result);
        this.step.set('reconcile');
      },
      error: (err) => {
        this.uploading.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر معالجة الملف');
      },
    });
  }

  protected submitCommit(): void {
    const preview = this.preview();
    const yearId = this.financialYearId();
    if (!preview || yearId == null || this.committing()) return;

    if (preview.mode === 'Approved' && !this.approvalDate()) {
      this.error.set('برجاء إدخال تاريخ الاعتماد');
      return;
    }

    this.committing.set(true);
    this.error.set(null);

    const dto: ImportCommit = {
      importId: preview.importId,
      financialYearId: yearId,
      approvalDate: preview.mode === 'Approved' ? this.approvalDate() : null,
      markazResolutions: [...this.resolutions['markaz'].values()],
      mainProgramResolutions: [...this.resolutions['mainProgram'].values()],
      subProgramResolutions: [...this.resolutions['subProgram'].values()],
      agencyResolutions: [...this.resolutions['agency'].values()],
      projectLevelResolutions: [...this.resolutions['projectLevel'].values()],
      componentTypeResolutions: [...this.resolutions['componentType'].values()],
      accountingUnitResolutions: [...this.resolutions['accountingUnit'].values()],
      mainProjectCodeResolutions: [...this.codeResolutions.values()],
      rowResolutions: [...this.rowResolutions.values()],
    };

    this.importService.commit(dto).subscribe({
      next: (result) => {
        this.committing.set(false);
        this.result.set(result);
        this.step.set('result');
      },
      error: (err) => {
        this.committing.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر إتمام الاستيراد');
      },
    });
  }

  protected finish(): void {
    this.reset();
    this.saved.emit();
  }

  private reset(): void {
    this.step.set('upload');
    this.selectedFile.set(null);
    this.preview.set(null);
    this.result.set(null);
    this.error.set(null);
    for (const map of Object.values(this.resolutions)) map.clear();
    this.codeResolutions.clear();
    this.rowResolutions.clear();
  }
}
```

- [ ] **Step 2: Wire the wizard into `projects.ts`**

In `Frontend/src/app/features/projects/projects.ts`, add the import and a `showImportWizard` signal + open/close handlers. Add near the top with the other imports:

```typescript
import { ExcelImportWizard } from './excel-import-wizard';
```

Add `ExcelImportWizard` to the `@Component` decorator's `imports` array (alongside `MainProjectForm, SubProjectForm`):

```typescript
  imports: [FormsModule, RouterLink, MainProjectForm, SubProjectForm, ExcelImportWizard],
```

Add a new signal next to `showSubForm`/`showMainForm` (around line 179):

```typescript
  protected readonly showImportWizard = signal(false);
```

Add handlers next to `openAddSub`/`closeModals` (around line 261-268):

```typescript
  protected openImportExcel(): void { this.addMenuOpen.set(false); this.showImportWizard.set(true); }
  protected closeImportWizard(): void { this.showImportWizard.set(false); }
  protected onImportSaved(): void { this.showImportWizard.set(false); this.load(); }
```

- [ ] **Step 3: Add the menu option + wizard element to the template**

In `Frontend/src/app/features/projects/projects.html`, extend the add-menu block (lines 29-36):

```html
      @if (addMenuOpen()) {
        <div class="menu">
          <button (click)="openAddMain()"><span class="mi main">◆</span> مشروع رئيسي</button>
          <button (click)="openAddSub()"><span class="mi sub">◆</span> مشروع فرعي</button>
          <button (click)="openImportExcel()"><span class="mi import">◆</span> استيراد من Excel</button>
        </div>
      }
```

Add the wizard element next to the other modal declarations at the bottom of the file (after the `app-sub-project-form` line):

```html
  <app-excel-import-wizard [open]="showImportWizard()" [financialYearId]="selectedYearId()" (close)="closeImportWizard()" (saved)="onImportSaved()" />
```

- [ ] **Step 4: Add the menu icon color**

In `Frontend/src/app/features/projects/projects.css`, add after the existing `.menu .mi.sub` rule:

```css
.menu .mi.import { background: #2F6FED; color: #fff; }
```

- [ ] **Step 5: Type-check and build**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
cd Frontend && npx ng build
```
Expected: both clean.

- [ ] **Step 6: Manual check via browser**

With `frontend-dev` and `backend-api` running: navigate to `/app/projects`, click "إضافة مشروع" → confirm "استيراد من Excel" appears as a 3rd option. Click it, confirm the upload step renders. Upload a suggested-mode file (no codes): confirm the mode banner reads "خطة مقترحة", confirm any unresolved names render grouped by category with جديد/نفس القيمة choices, confirm the confirm step shows the expected counts, confirm commit succeeds and the results screen shows created counts, confirm the projects table refreshes and the new projects appear. Repeat with an approved-mode file (all codes): confirm the mode banner reads "خطة معتمدة", confirm the approval-date field appears only in this mode, confirm commit approves the matching projects and the projects table reflects the new codes/approved state.

- [ ] **Step 7: Commit**

```bash
git add Frontend/src/app/features/projects/excel-import-wizard.ts Frontend/src/app/features/projects/projects.ts Frontend/src/app/features/projects/projects.html Frontend/src/app/features/projects/projects.css
git commit -m "feat: Excel import wizard, wired into the projects add-menu

Single wizard modal auto-branches its reconciliation and confirm steps
by the backend-detected suggested/approved mode after the upload step
returns — staff never picks a mode up front."
```

---

### Task 7: Final End-to-End Verification

**Files:** none (verification only).

- [ ] **Step 1: Full regression pass in the browser**

With `frontend-dev` and `backend-api` running:
1. Import a suggested-mode file end-to-end (per Task 6 Step 6) — confirm the created Main/Sub Projects appear correctly on `/app/projects`, confirm the new Suggested plan appears on `/app/plans` with the correct sub-project count.
2. Re-run the exact same suggested file a second time — confirm it succeeds again with fresh duplicates (per the base spec's explicit "no dedup" requirement) and reuses the same Suggested plan (not a second one).
3. Import a matching approved-mode file — confirm the matched sub-projects show their new code and approved state on `/app/projects`, confirm the plan from step 1 now shows as Approved with the correct approval date on `/app/plans`.
4. Confirm a mixed-code file is rejected cleanly at the upload step with the exact Arabic message, before any reconciliation UI shows.
5. Confirm an unresolved ProjectLevel/ComponentType/AccountingUnit name during a suggested import is surfaced for reconciliation (not silently defaulted) and choosing "جديد" creates a real, reusable lookup entry.

- [ ] **Step 2: Confirm no stray console errors**

Use `read_console_messages` (`onlyErrors: true`) during the pass above.

- [ ] **Step 3: Final backend + frontend build**

```bash
cd Backend/src/SmartInvest.API && dotnet build
cd Frontend && npx ng build
```
Expected: both succeed with no errors.

- [ ] **Step 4: Final `git status`**

```bash
git status
```
Confirm only files touched by Tasks 1-6 show as modified/new.
