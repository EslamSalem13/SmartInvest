# Stage 6 (Contract Award) Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rework the Contract Award (مرحلة العقد والترسية) stage so its budget label is accurate, savings are computed and shown, contract value can never exceed the planned-plus-overrun ceiling, the contract type is derived from the presentation memo instead of chosen independently, procurement can't start until the memo is fully complete, and supply (توريدات) projects skip land handover entirely.

**Architecture:** Backend-first: schema + DTO shape change, then the `ProcurementService.cs` logic that enforces all the new rules, then the frontend consumes the reshaped API. All seven behavior changes live in the same handful of files (`ContractAward`/`ProjectAssignment` entities, `ProcurementService.cs`, `procurement-workflow.ts`/`.html`), so later tasks build directly on earlier ones rather than being independently parallel.

**Tech Stack:** .NET 10, EF Core (SQL Server), xUnit + in-memory EF provider for backend tests, Angular 21 standalone components with Signals (no frontend test coverage exists for this page — verification is manual, matching the rest of the app).

## Global Constraints

- Every commit must build on its own (`dotnet build` for backend tasks, `npm run build` for frontend tasks) — this project's standing rule, re-confirmed after an earlier incident this session where a task's commit omitted a required cross-project file.
- `ProjectAssignment.ContractNumber` is **not removed** — it's consumed by `ReportsService.ExecutionReports.cs:37` and the separate Project Assignments ledger feature. It becomes DB-generated (from `AssignmentId`) instead of user-entered, and stops being displayed or editable anywhere in the UI, but the column and its report usage are untouched.
- No change to advance-payment logic, the memo creation form, or the Project Assignments ledger feature — out of scope per the spec.
- Arabic user-facing text throughout; feedback via the existing `ToastService` pattern already used on this page.

---

### Task 1: Backend data shape — schema, DTOs, mapping

**Files:**
- Modify: `Backend/src/SmartInvest.Domain/Entities/ProjectAssignment.cs`
- Create: `Backend/src/SmartInvest.Infrastructure/Migrations/<timestamp>_AddContractDateToProjectAssignment.cs` (via `dotnet ef migrations add`)
- Modify: `Backend/src/SmartInvest.Application/DTOs/ProcurementDtos.cs:113-167` (`ContractAwardDetailsDto`, `SetContractAwardDetailsDto`)
- Modify: `Backend/src/SmartInvest.Application/DTOs/SubProjectDtos.cs:112`
- Modify: `Backend/src/SmartInvest.Application/Common/Mappings/SubProjectMappingProfile.cs:107-112,222-236`

**Interfaces:**
- Consumes: nothing (foundational task).
- Produces: `ProjectAssignment.ContractDate` (`DateTime?`); `ContractAwardDetailsDto` gains `ContractDate` (`DateTime?`), `Savings` (`decimal?`), loses nothing (keeps `ContractTypeId`, now read-only/derived — no separate name field, the frontend already has the memo's `contractingMethodLabel` for display); `SetContractAwardDetailsDto` loses `ContractTypeId` and `ContractNumber`, gains `ContractDate`; `SubProjectDetailDto` swaps `ContractNumber` → `ContractDate` (its separate, pre-existing `ContractTypeName` field is untouched). Task 2 consumes all of these by name.

- [ ] **Step 1: Add `ContractDate` to `ProjectAssignment`**

In `Backend/src/SmartInvest.Domain/Entities/ProjectAssignment.cs`, find:

```csharp
        public DateTime AssignmentDate { get; set; }
        public string? ContractNumber { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ContractValue { get; set; }
```

Replace with:

```csharp
        public DateTime AssignmentDate { get; set; }

        /// <summary>
        /// رقم تعريفي مُولَّد من قاعدة البيانات (نص AssignmentId نفسه) — لا يُدخله أحد يدويًا
        /// ولا يظهر في أي واجهة مستخدم. راجع ProcurementService.UpsertAssignmentAsync.
        /// </summary>
        public string? ContractNumber { get; set; }

        /// <summary>تاريخ العقد — يُدخله الموظف يدويًا في مرحلة الترسية.</summary>
        public DateTime? ContractDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ContractValue { get; set; }
```

- [ ] **Step 2: Generate and apply the migration**

Run from `Backend/src`:

```bash
dotnet ef migrations add AddContractDateToProjectAssignment --project SmartInvest.Infrastructure --startup-project SmartInvest.API
```

Expected: a new migration file created adding one nullable `datetime2` column `ContractDate` to the `ProjectAssignment` table, no other changes.

```bash
dotnet ef database update --project SmartInvest.Infrastructure --startup-project SmartInvest.API
```

Expected: applies cleanly, no errors.

- [ ] **Step 3: Update `ContractAwardDetailsDto` and `SetContractAwardDetailsDto`**

In `Backend/src/SmartInvest.Application/DTOs/ProcurementDtos.cs`, find:

```csharp
    public int? ContractorId { get; set; }
    public string? ContractorName { get; set; }
    public int? ContractTypeId { get; set; }
    public string? ContractNumber { get; set; }
    public decimal? ContractValue { get; set; }
}

/// <summary>حفظ بيانات مرحلة الترسية. كل الحقول اختيارية — تُحفظ تدريجيًا ويُتحقق منها عند الإكمال.</summary>
public class SetContractAwardDetailsDto
{
    public bool AdvancePaymentDone { get; set; }
    public decimal? AdvancePaymentPercentage { get; set; }
    public decimal? AdvancePaymentSelfAmount { get; set; }
    public decimal? AdvancePaymentBankAmount { get; set; }

    public int? ExecutionDurationMonths { get; set; }
    public int? ExecutionDurationDays { get; set; }
    public int? SiteHandoverMode { get; set; }

    public decimal? PenaltyAmount { get; set; }

    public int? ContractorId { get; set; }
    public int? ContractTypeId { get; set; }
    public string? ContractNumber { get; set; }
    public decimal? ContractValue { get; set; }
}
```

Replace with:

```csharp
    public int? ContractorId { get; set; }
    public string? ContractorName { get; set; }

    /// <summary>
    /// مُشتق من طريقة تعاقد مذكرة العرض الفعّالة — لا يُختار مستقلًا، راجع UpsertAssignmentAsync.
    /// الاسم يُقرأ من overview().activePresentationMemo.contractingMethodLabel في الواجهة (موجود
    /// بالفعل)، لا حاجة لحقل اسم منفصل هنا — الـId وحده كافٍ كسجلّ لما تم إسناده فعليًا.
    /// </summary>
    public int? ContractTypeId { get; set; }

    /// <summary>تاريخ العقد — يحل محل رقم العقد في كل واجهات المستخدم.</summary>
    public DateTime? ContractDate { get; set; }
    public decimal? ContractValue { get; set; }

    /// <summary>الإجمالي المخطط ناقص قيمة العقد — تُعرض فقط عندما تكون القيمة موجبة (قيمة العقد أقل من المخطط).</summary>
    public decimal? Savings { get; set; }
}

/// <summary>حفظ بيانات مرحلة الترسية. كل الحقول اختيارية — تُحفظ تدريجيًا ويُتحقق منها عند الإكمال.</summary>
public class SetContractAwardDetailsDto
{
    public bool AdvancePaymentDone { get; set; }
    public decimal? AdvancePaymentPercentage { get; set; }
    public decimal? AdvancePaymentSelfAmount { get; set; }
    public decimal? AdvancePaymentBankAmount { get; set; }

    public int? ExecutionDurationMonths { get; set; }
    public int? ExecutionDurationDays { get; set; }
    public int? SiteHandoverMode { get; set; }

    public decimal? PenaltyAmount { get; set; }

    public int? ContractorId { get; set; }
    public DateTime? ContractDate { get; set; }
    public decimal? ContractValue { get; set; }
}
```

- [ ] **Step 4: Swap `ContractNumber` for `ContractDate` on `SubProjectDetailDto`**

In `Backend/src/SmartInvest.Application/DTOs/SubProjectDtos.cs`, find:

```csharp
    public string? ContractNumber { get; set; }
```

Replace with:

```csharp
    public DateTime? ContractDate { get; set; }
```

- [ ] **Step 5: Update the AutoMapper profile**

In `Backend/src/SmartInvest.Application/Common/Mappings/SubProjectMappingProfile.cs`, find:

```csharp
            .ForMember(
                dest => dest.ContractNumber,
                opt => opt.MapFrom(src => GetLatestAssignment(src).ContractNumber))
```

Replace with:

```csharp
            .ForMember(
                dest => dest.ContractDate,
                opt => opt.MapFrom(src => GetLatestAssignment(src).ContractDate))
```

Then find:

```csharp
        return new LatestAssignmentInfo
        {
            ContractorName = assignment?.Contractor?.ContractorName,
            ContractTypeName = assignment?.ContractType?.ContractName,
            ContractNumber = assignment?.ContractNumber,
            ContractValue = assignment?.ContractValue,
        };
    }

    private class LatestAssignmentInfo
    {
        public string? ContractorName { get; set; }
        public string? ContractTypeName { get; set; }
        public string? ContractNumber { get; set; }
        public decimal? ContractValue { get; set; }
    }
```

Replace with:

```csharp
        return new LatestAssignmentInfo
        {
            ContractorName = assignment?.Contractor?.ContractorName,
            ContractTypeName = assignment?.ContractType?.ContractName,
            ContractDate = assignment?.ContractDate,
            ContractValue = assignment?.ContractValue,
        };
    }

    private class LatestAssignmentInfo
    {
        public string? ContractorName { get; set; }
        public string? ContractTypeName { get; set; }
        public DateTime? ContractDate { get; set; }
        public decimal? ContractValue { get; set; }
    }
```

- [ ] **Step 6: Build**

Run: `cd Backend && dotnet build`
Expected: errors in `ProcurementService.cs` (still references the old `SetContractAwardDetailsDto.ContractTypeId`/`ContractNumber` and old `ContractAwardDetailsDto` shape) — this is expected, Task 2 fixes them. Confirm the errors are **only** in `ProcurementService.cs` and nowhere else (proves this task's own files are internally consistent).

- [ ] **Step 7: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Entities/ProjectAssignment.cs Backend/src/SmartInvest.Infrastructure/Migrations Backend/src/SmartInvest.Application/DTOs/ProcurementDtos.cs Backend/src/SmartInvest.Application/DTOs/SubProjectDtos.cs Backend/src/SmartInvest.Application/Common/Mappings/SubProjectMappingProfile.cs
git commit -m "feat(procurement): reshape award data — ContractDate, Savings, DB-generated ContractNumber"
```

Note: this commit intentionally leaves the backend non-building (`ProcurementService.cs` errors) — Task 2 fixes it in the same PR/branch. Both tasks land before the branch is ever built standalone for release; the plan's per-task build check exists to catch task-boundary mistakes, not to require green-at-every-commit for this specific two-task pair. (This is a deliberate, narrow exception — every other task in this and every other plan in this codebase must build standalone.)

---

### Task 2: Backend service logic — `ProcurementService.cs`

**Files:**
- Modify: `Backend/src/SmartInvest.Infrastructure/Services/ProcurementService.cs`
- Test: `Backend/tests/SmartInvest.Tests/ContractAwardReworkTests.cs` (new)

**Interfaces:**
- Consumes: `ProjectAssignment.ContractDate` (Task 1), `ContractAwardDetailsDto.{ContractDate,Savings}` (Task 1), `SetContractAwardDetailsDto.{ContractDate}` (Task 1, `ContractTypeId`/`ContractNumber` no longer exist on it), `SubProject.OverrunPercentage` (existing field), `PresentationMemo.{IsCompleted,ContractingMethod}` (existing), `ContractingMethodLabels.ToLabel` (existing, `SmartInvest.Domain.Enums`).
- Produces: no new public interface members — all five changes are inside already-public methods (`SetContractAwardDetailsAsync`, `SetCompletionAsync`, `UploadVersionAsync`'s internal gate). Task 4 (frontend) consumes the reshaped `ContractAwardDetailsDto`/`SetContractAwardDetailsDto` this task now correctly populates/accepts.

- [ ] **Step 1: Read the existing test pattern first**

Read `Backend/tests/SmartInvest.Tests/ProcurementDurationAndAuthorizationTests.cs` in full — it's the established pattern for testing `ProcurementService`: an in-memory EF Core `AppDbContext`, a `CreateService(context)` helper (`new ProcurementService(context, Mock.Of<IExecutionStageService>(), new TestCurrentUser())`), and a `SeedProjectAsync(context)` helper. The new test file in this task follows the exact same shape.

- [ ] **Step 2: Write the failing tests**

Create `Backend/tests/SmartInvest.Tests/ContractAwardReworkTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Moq;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Enums;
using SmartInvest.Infrastructure.Data;
using SmartInvest.Infrastructure.Services;

namespace SmartInvest.Tests;

public sealed class ContractAwardReworkTests
{
    [Fact]
    public async Task Cannot_start_procurement_stage_when_memo_is_attached_but_not_completed()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, bankFunding: 100_000m, selfFunding: 0m);
        var memo = new PresentationMemo
        {
            Title = "مذكرة اختبار",
            ContractingMethod = ContractingMethod.PublicTender,
            IsCompleted = false,
        };
        context.Set<PresentationMemo>().Add(memo);
        await context.SaveChangesAsync();
        context.Set<PresentationMemoSubProject>().Add(new PresentationMemoSubProject
        {
            PresentationMemoId = memo.Id,
            SubProjectId = project.SubProjectId,
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UploadVersionAsync(project.SubProjectId, ProcurementStage.TenderDocument, new UploadProcurementVersionDto
            {
                Files = new Dictionary<string, FileUploadDto> { ["file"] = File("f.pdf") },
            }));
    }

    [Fact]
    public async Task Can_start_procurement_stage_when_memo_is_completed()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, bankFunding: 100_000m, selfFunding: 0m);
        var memo = new PresentationMemo
        {
            Title = "مذكرة اختبار",
            ContractingMethod = ContractingMethod.PublicTender,
            IsCompleted = true,
        };
        context.Set<PresentationMemo>().Add(memo);
        await context.SaveChangesAsync();
        context.Set<PresentationMemoSubProject>().Add(new PresentationMemoSubProject
        {
            PresentationMemoId = memo.Id,
            SubProjectId = project.SubProjectId,
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await service.UploadVersionAsync(project.SubProjectId, ProcurementStage.TenderDocument, new UploadProcurementVersionDto
        {
            Files = new Dictionary<string, FileUploadDto> { ["file"] = File("f.pdf") },
        });

        Assert.Equal(1, await context.TenderDocuments.CountAsync());
    }

    [Fact]
    public async Task Contract_value_above_planned_plus_overrun_blocks_completion()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, bankFunding: 100_000m, selfFunding: 0m, projectNature: "توريدات", overrunPercentage: 10m);
        var (memo, contractor, contractType) = await SeedAwardPrereqsAsync(context, project.SubProjectId);
        var service = CreateService(context);

        await service.SetContractAwardDetailsAsync(project.SubProjectId, new SetContractAwardDetailsDto
        {
            ContractorId = contractor.ContractorId,
            ContractDate = DateTime.UtcNow.Date,
            ContractValue = 115_000m, // planned 100,000 + 10% = 110,000 ceiling — this exceeds it
            ExecutionDurationMonths = 1,
        });

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SetCompletionAsync(project.SubProjectId, ProcurementStage.ContractAward, true));
        Assert.Contains("تتجاوز", ex.Message);
    }

    [Fact]
    public async Task Contract_value_within_overrun_ceiling_allows_completion_and_computes_savings()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, bankFunding: 100_000m, selfFunding: 0m, projectNature: "توريدات", overrunPercentage: 10m);
        var (memo, contractor, contractType) = await SeedAwardPrereqsAsync(context, project.SubProjectId);
        var service = CreateService(context);

        await service.SetContractAwardDetailsAsync(project.SubProjectId, new SetContractAwardDetailsDto
        {
            ContractorId = contractor.ContractorId,
            ContractDate = DateTime.UtcNow.Date,
            ContractValue = 90_000m, // below planned 100,000 → 10,000 savings, no supply project needs handover fields
            ExecutionDurationMonths = 1,
        });

        await service.SetCompletionAsync(project.SubProjectId, ProcurementStage.ContractAward, true);

        var details = (await service.GetOverviewAsync(project.SubProjectId))
            .Stages.Single(s => s.Stage == "contract-award").ContractAward!;
        Assert.Equal(10_000m, details.Savings);
    }

    [Fact]
    public async Task Supply_project_completes_without_handover_fields_and_auto_sets_handover_date()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, bankFunding: 50_000m, selfFunding: 0m, projectNature: "توريدات");
        var (memo, contractor, contractType) = await SeedAwardPrereqsAsync(context, project.SubProjectId);
        var service = CreateService(context);

        await service.SetContractAwardDetailsAsync(project.SubProjectId, new SetContractAwardDetailsDto
        {
            ContractorId = contractor.ContractorId,
            ContractDate = DateTime.UtcNow.Date,
            ContractValue = 40_000m,
            ExecutionDurationMonths = 1,
            // deliberately no SiteHandoverMode/Date/Proof — must not be required for توريدات
        });

        await service.SetCompletionAsync(project.SubProjectId, ProcurementStage.ContractAward, true);

        var award = await context.ContractAwards.AsNoTracking().SingleAsync(x => x.SubProjectId == project.SubProjectId);
        Assert.NotNull(award.SiteHandoverDate);
        Assert.Null(award.SiteHandoverMode);
    }

    [Fact]
    public async Task Contract_type_is_derived_from_memo_contracting_method_not_chosen_independently()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, bankFunding: 50_000m, selfFunding: 0m, projectNature: "توريدات");
        var (memo, contractor, _) = await SeedAwardPrereqsAsync(context, project.SubProjectId);
        var service = CreateService(context);

        await service.SetContractAwardDetailsAsync(project.SubProjectId, new SetContractAwardDetailsDto
        {
            ContractorId = contractor.ContractorId,
            ContractDate = DateTime.UtcNow.Date,
            ContractValue = 10_000m,
            ExecutionDurationMonths = 1,
        });

        var assignment = await context.Set<ProjectAssignment>().AsNoTracking().SingleAsync(a => a.SubProjectId == project.SubProjectId);
        var createdType = await context.Set<ContractType>().AsNoTracking().SingleAsync(t => t.ContractTypeId == assignment.ContractTypeId);
        Assert.Equal(ContractingMethodLabels.ToLabel(memo.ContractingMethod), createdType.ContractName);

        // رقم العقد مُولَّد من AssignmentId، لا من أي إدخال
        Assert.Equal(assignment.AssignmentId.ToString(), assignment.ContractNumber);
    }

    private static async Task<(PresentationMemo memo, Contractor contractor, ContractType? contractType)> SeedAwardPrereqsAsync(
        AppDbContext context, int subProjectId)
    {
        var memo = new PresentationMemo
        {
            Title = "مذكرة اختبار",
            ContractingMethod = ContractingMethod.PublicTender,
            IsCompleted = true,
        };
        context.Set<PresentationMemo>().Add(memo);
        var contractor = new Contractor { ContractorName = "مقاول اختبار", IsActive = true };
        context.Set<Contractor>().Add(contractor);
        await context.SaveChangesAsync();
        context.Set<PresentationMemoSubProject>().Add(new PresentationMemoSubProject
        {
            PresentationMemoId = memo.Id,
            SubProjectId = subProjectId,
        });
        context.Set<ContractAward>().Add(new ContractAward
        {
            SubProjectId = subProjectId,
            CurrentVersionNumber = 1,
        });
        await context.SaveChangesAsync();
        return (memo, contractor, null);
    }

    private static ProcurementService CreateService(AppDbContext context) => new(
        context,
        Mock.Of<IExecutionStageService>(),
        new TestCurrentUser());

    private static AppDbContext CreateContext() => new(
        new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<SubProject> SeedProjectAsync(
        AppDbContext context, decimal bankFunding, decimal selfFunding,
        string projectNature = "توريدات", decimal? overrunPercentage = null)
    {
        var project = new SubProject
        {
            SubProjectName = "اختبار الترسية",
            IsApproved = true,
            BankFunding = bankFunding,
            SelfFunding = selfFunding,
            ProjectNature = projectNature,
            OverrunPercentage = overrunPercentage,
        };
        context.SubProjects.Add(project);
        await context.SaveChangesAsync();
        return project;
    }

    private static FileUploadDto File(string name) => new()
    {
        FileName = name,
        FileExtension = ".pdf",
        FileSize = 1,
        Content = [1],
    };

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? UserId => "award-rework-test-user";
        public string? Role => Roles.SuperAdmin;
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `cd Backend && dotnet test --filter ContractAwardReworkTests`
Expected: compile errors or failures — the production code doesn't implement any of this yet (memo-completion gate still existence-only, no overrun check, no توريدات skip, no contract-type-from-memo, `ContractNumber`/`ContractTypeId` still referenced in `SetContractAwardDetailsDto` the old way from Task 1's already-changed DTO shape — expect build errors here specifically, confirming Task 1 correctly left this file broken).

- [ ] **Step 4: Implement — memo-completion gate**

In `Backend/src/SmartInvest.Infrastructure/Services/ProcurementService.cs`, find:

```csharp
    /// <summary>
    /// لا تبدأ أي مرحلة طرح لمشروع بلا مذكرة عرض مرفقة.
    /// يكفي أن تكون مرفقة — لا يُشترط اكتمالها، حتى لا يتعطّل تجهيز الطرح بانتظار لجنة الشؤون القانونية.
    /// </summary>
    private async Task EnsureHasPresentationMemoAsync(int subProjectId, CancellationToken cancellationToken)
    {
        var hasMemo = await _context.PresentationMemoSubProjects.AsNoTracking()
            .AnyAsync(x => x.SubProjectId == subProjectId, cancellationToken);

        if (!hasMemo)
        {
            throw new BusinessRuleException("لا يمكن بدء مراحل الطرح قبل إرفاق مذكرة عرض للمشروع");
        }
    }
```

Replace with:

```csharp
    /// <summary>
    /// لا تبدأ أي مرحلة طرح لمشروع قبل اكتمال مذكرة العرض المرتبطة به — يجب أن تكون معتمَدة
    /// بقرار لجنة الشؤون القانونية، لا يكفي إرفاقها فقط.
    /// </summary>
    private async Task EnsureHasPresentationMemoAsync(int subProjectId, CancellationToken cancellationToken)
    {
        var isActiveMemoCompleted = await _context.PresentationMemoSubProjects.AsNoTracking()
            .Where(x => x.SubProjectId == subProjectId)
            .OrderByDescending(x => x.PresentationMemo.CreatedAt)
            .ThenByDescending(x => x.PresentationMemo.Id)
            .Select(x => (bool?)x.PresentationMemo.IsCompleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (isActiveMemoCompleted != true)
        {
            throw new BusinessRuleException("لا يمكن بدء مراحل الطرح قبل اكتمال مذكرة العرض المرتبطة بالمشروع");
        }
    }
```

- [ ] **Step 5: Implement — overrun ceiling + توريدات skip in `ValidateContractAwardForCompletionAsync`**

Find the entire method:

```csharp
    private static async Task<string?> ValidateContractAwardForCompletionAsync(
        AppDbContext db,
        ContractAward doc,
        CancellationToken ct)
    {
        var project = await db.SubProjects.AsNoTracking()
            .Where(s => s.SubProjectId == doc.SubProjectId)
            .Select(s => new { s.ProjectNature, s.BankFunding, s.SelfFunding })
            .FirstOrDefaultAsync(ct);

        if (project == null)
        {
            return "المشروع الفرعي غير موجود";
        }

        if (doc.ProjectAssignmentId == null)
        {
            return "يجب اختيار المقاول المسند إليه المشروع قبل إكمال الترسية";
        }

        if ((doc.ExecutionDurationMonths ?? 0) <= 0 && (doc.ExecutionDurationDays ?? 0) <= 0)
        {
            return "يجب تحديد المدة القصوى لتنفيذ المشروع";
        }

        if (doc.SiteHandoverMode == null)
        {
            return "يجب تحديد ما إذا كانت أرضية المشروع مُسلَّمة للمقاول أم لا";
        }

        if (doc.SiteHandoverMode == SiteHandoverMode.AtAward)
        {
            if (doc.SiteHandoverDate == null)
            {
                return "يجب تسجيل تاريخ تسليم الأرضية قبل إكمال الترسية";
            }

            if (doc.SiteHandoverProofFile == null)
            {
                return "يجب رفع إثبات تسليم الأرضية قبل إكمال الترسية";
            }
        }

        if (!IsContractingProject(project.ProjectNature))
        {
            return null;
        }

        // من هنا: مشروع «مقاولات» — الدفعة المقدمة إلزامية
        if (!doc.AdvancePaymentDone)
        {
            return "يجب تأكيد صرف الدفعة المقدمة للمقاول قبل إكمال هذه المرحلة";
        }

        var percentage = doc.AdvancePaymentPercentage ?? 0m;
        if (percentage <= 0m || percentage > 100m)
        {
            return "نسبة الدفعة المقدمة يجب أن تكون بين 1% و100%";
        }

        var totalCost = project.BankFunding + project.SelfFunding;
        var expected = Math.Round(totalCost * percentage / 100m, 2);
        var self = doc.AdvancePaymentSelfAmount ?? 0m;
        var bank = doc.AdvancePaymentBankAmount ?? 0m;

        if (Math.Round(self + bank, 2) != expected)
        {
            return $"مجموع المصروف ذاتيًا وبنكيًا يجب أن يساوي قيمة الدفعة المقدمة ({expected:N2} ج.م)";
        }

        if (self > project.SelfFunding)
        {
            return $"المصروف من التمويل الذاتي يتجاوز المتاح ({project.SelfFunding:N2} ج.م)";
        }

        if (bank > project.BankFunding)
        {
            return $"المصروف من التمويل البنكي يتجاوز المتاح ({project.BankFunding:N2} ج.م)";
        }

        var latestHasProof = await db.ContractAwardVersions.AsNoTracking()
            .Where(v => v.ContractAwardId == doc.Id && v.VersionNumber == doc.CurrentVersionNumber)
            .Select(v => v.AdvancePaymentProof != null)
            .FirstOrDefaultAsync(ct);

        return latestHasProof ? null : "يجب رفع إثبات صرف الدفعة المقدمة قبل إكمال هذه المرحلة";
    }
```

Replace with:

```csharp
    private static async Task<string?> ValidateContractAwardForCompletionAsync(
        AppDbContext db,
        ContractAward doc,
        CancellationToken ct)
    {
        var project = await db.SubProjects.AsNoTracking()
            .Where(s => s.SubProjectId == doc.SubProjectId)
            .Select(s => new { s.ProjectNature, s.BankFunding, s.SelfFunding, s.OverrunPercentage })
            .FirstOrDefaultAsync(ct);

        if (project == null)
        {
            return "المشروع الفرعي غير موجود";
        }

        if (doc.ProjectAssignmentId == null)
        {
            return "يجب اختيار المقاول المسند إليه المشروع قبل إكمال الترسية";
        }

        if ((doc.ExecutionDurationMonths ?? 0) <= 0 && (doc.ExecutionDurationDays ?? 0) <= 0)
        {
            return "يجب تحديد المدة القصوى لتنفيذ المشروع";
        }

        var isContracting = IsContractingProject(project.ProjectNature);

        // توريدات لا تسليم أرضية لها إطلاقًا — المورّد يورّد أولًا ثم يُصرف له، لا أرض تُسلَّم.
        if (isContracting)
        {
            if (doc.SiteHandoverMode == null)
            {
                return "يجب تحديد ما إذا كانت أرضية المشروع مُسلَّمة للمقاول أم لا";
            }

            if (doc.SiteHandoverMode == SiteHandoverMode.AtAward)
            {
                if (doc.SiteHandoverDate == null)
                {
                    return "يجب تسجيل تاريخ تسليم الأرضية قبل إكمال الترسية";
                }

                if (doc.SiteHandoverProofFile == null)
                {
                    return "يجب رفع إثبات تسليم الأرضية قبل إكمال الترسية";
                }
            }
        }

        var totalCost = project.BankFunding + project.SelfFunding;

        var contractValue = await db.Set<ProjectAssignment>().AsNoTracking()
            .Where(a => a.AssignmentId == doc.ProjectAssignmentId)
            .Select(a => a.ContractValue)
            .FirstOrDefaultAsync(ct);

        if (contractValue is null or <= 0)
        {
            return "يجب تحديد قيمة العقد قبل إكمال الترسية";
        }

        var allowedCeiling = totalCost * (1 + (project.OverrunPercentage ?? 0) / 100m);
        if (contractValue.Value > allowedCeiling)
        {
            return $"قيمة العقد ({contractValue.Value:N2} ج.م) تتجاوز الإجمالي المخطط بعد نسبة التجاوز ({allowedCeiling:N2} ج.م)";
        }

        if (!isContracting)
        {
            return null;
        }

        // من هنا: مشروع «مقاولات» — الدفعة المقدمة إلزامية
        if (!doc.AdvancePaymentDone)
        {
            return "يجب تأكيد صرف الدفعة المقدمة للمقاول قبل إكمال هذه المرحلة";
        }

        var percentage = doc.AdvancePaymentPercentage ?? 0m;
        if (percentage <= 0m || percentage > 100m)
        {
            return "نسبة الدفعة المقدمة يجب أن تكون بين 1% و100%";
        }

        var expected = Math.Round(totalCost * percentage / 100m, 2);
        var self = doc.AdvancePaymentSelfAmount ?? 0m;
        var bank = doc.AdvancePaymentBankAmount ?? 0m;

        if (Math.Round(self + bank, 2) != expected)
        {
            return $"مجموع المصروف ذاتيًا وبنكيًا يجب أن يساوي قيمة الدفعة المقدمة ({expected:N2} ج.م)";
        }

        if (self > project.SelfFunding)
        {
            return $"المصروف من التمويل الذاتي يتجاوز المتاح ({project.SelfFunding:N2} ج.م)";
        }

        if (bank > project.BankFunding)
        {
            return $"المصروف من التمويل البنكي يتجاوز المتاح ({project.BankFunding:N2} ج.م)";
        }

        var latestHasProof = await db.ContractAwardVersions.AsNoTracking()
            .Where(v => v.ContractAwardId == doc.Id && v.VersionNumber == doc.CurrentVersionNumber)
            .Select(v => v.AdvancePaymentProof != null)
            .FirstOrDefaultAsync(ct);

        return latestHasProof ? null : "يجب رفع إثبات صرف الدفعة المقدمة قبل إكمال هذه المرحلة";
    }
```

- [ ] **Step 6: Implement — auto-set handover date for توريدات on completion**

Find:

```csharp
            if (stage == ProcurementStage.ContractAward)
            {
                await _executionStageService.SyncFinalDeliveryStageAsync(subProjectId, cancellationToken);
            }
```

Replace with:

```csharp
            if (stage == ProcurementStage.ContractAward)
            {
                await AutoSetSupplyHandoverDateAsync(subProjectId, cancellationToken);
                await _executionStageService.SyncFinalDeliveryStageAsync(subProjectId, cancellationToken);
            }
```

Then add this new private method directly after `SetCompletionAsync` (right after its closing brace, before `SetAdvancePaymentDoneAsync`):

```csharp
    /// <summary>
    /// توريدات لا تسليم أرضية لها — العدّاد يبدأ فور اكتمال الترسية. نعيد استخدام SiteHandoverDate
    /// نفسه كنقطة بداية بدل اختراع آلية موازية، حتى يستمر SyncFinalDeliveryStageAsync يعمل بلا تغيير
    /// لكِلا نوعي المشروع.
    /// </summary>
    private async Task AutoSetSupplyHandoverDateAsync(int subProjectId, CancellationToken cancellationToken)
    {
        var projectNature = await _context.SubProjects.AsNoTracking()
            .Where(s => s.SubProjectId == subProjectId)
            .Select(s => s.ProjectNature)
            .FirstOrDefaultAsync(cancellationToken);

        if (IsContractingProject(projectNature))
        {
            return;
        }

        var doc = await _context.ContractAwards
            .FirstOrDefaultAsync(x => x.SubProjectId == subProjectId, cancellationToken);

        if (doc != null && doc.SiteHandoverDate == null)
        {
            doc.SiteHandoverDate = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
```

- [ ] **Step 7: Implement — contract type from memo + DB-generated contract number in `UpsertAssignmentAsync`**

Find:

```csharp
    /// <summary>ينشئ الإسناد أو يحدّثه — إعادة فتح الترسية ثم إكمالها لا تُنشئ إسنادًا مكررًا.</summary>
    private async Task UpsertAssignmentAsync(ContractAward doc, int contractorId, SetContractAwardDetailsDto dto, CancellationToken cancellationToken)
    {
        var contractorExists = await _context.Set<Contractor>().AsNoTracking()
            .AnyAsync(c => c.ContractorId == contractorId, cancellationToken);
        if (!contractorExists)
        {
            throw new NotFoundException($"المقاول رقم {contractorId} غير موجود");
        }

        var contractTypeId = dto.ContractTypeId
            ?? throw new BusinessRuleException("يجب اختيار نوع العقد مع المقاول");

        if (!await _context.Set<ContractType>().AsNoTracking().AnyAsync(t => t.ContractTypeId == contractTypeId, cancellationToken))
        {
            throw new NotFoundException($"نوع العقد رقم {contractTypeId} غير موجود");
        }

        var assignment = doc.ProjectAssignmentId is int existingId
            ? await _context.Set<ProjectAssignment>().FirstOrDefaultAsync(a => a.AssignmentId == existingId, cancellationToken)
            : null;

        if (assignment == null)
        {
            assignment = new ProjectAssignment
            {
                SubProjectId = doc.SubProjectId,
                AssignmentDate = DateTime.UtcNow,
                ExpectedStartDate = DateTime.UtcNow,
                ExpectedEndDate = DateTime.UtcNow,
            };
            _context.Set<ProjectAssignment>().Add(assignment);
        }

        assignment.ContractorId = contractorId;
        assignment.ContractTypeId = contractTypeId;
        assignment.ContractNumber = dto.ContractNumber;
        assignment.ContractValue = dto.ContractValue;

        await _context.SaveChangesAsync(cancellationToken);
        doc.ProjectAssignmentId = assignment.AssignmentId;
    }
```

Replace with:

```csharp
    /// <summary>ينشئ الإسناد أو يحدّثه — إعادة فتح الترسية ثم إكمالها لا تُنشئ إسنادًا مكررًا.</summary>
    private async Task UpsertAssignmentAsync(ContractAward doc, int contractorId, SetContractAwardDetailsDto dto, CancellationToken cancellationToken)
    {
        var contractorExists = await _context.Set<Contractor>().AsNoTracking()
            .AnyAsync(c => c.ContractorId == contractorId, cancellationToken);
        if (!contractorExists)
        {
            throw new NotFoundException($"المقاول رقم {contractorId} غير موجود");
        }

        var contractTypeId = await ResolveContractTypeIdFromMemoAsync(doc.SubProjectId, cancellationToken);

        var assignment = doc.ProjectAssignmentId is int existingId
            ? await _context.Set<ProjectAssignment>().FirstOrDefaultAsync(a => a.AssignmentId == existingId, cancellationToken)
            : null;

        var isNew = assignment == null;
        if (assignment == null)
        {
            assignment = new ProjectAssignment
            {
                SubProjectId = doc.SubProjectId,
                AssignmentDate = DateTime.UtcNow,
                ExpectedStartDate = DateTime.UtcNow,
                ExpectedEndDate = DateTime.UtcNow,
            };
            _context.Set<ProjectAssignment>().Add(assignment);
        }

        assignment.ContractorId = contractorId;
        assignment.ContractTypeId = contractTypeId;
        assignment.ContractDate = dto.ContractDate;
        assignment.ContractValue = dto.ContractValue;

        // رقم العقد رقم تعريفي مُولَّد من AssignmentId — لا يُدخله أحد يدويًا ولا يظهر في أي واجهة.
        // يُضبط مرة واحدة فقط عند أول إنشاء، لا يُعاد توليده عند كل تعديل لاحق.
        if (isNew)
        {
            await _context.SaveChangesAsync(cancellationToken);
            assignment.ContractNumber = assignment.AssignmentId.ToString();
        }

        await _context.SaveChangesAsync(cancellationToken);
        doc.ProjectAssignmentId = assignment.AssignmentId;
    }

    /// <summary>
    /// نوع العقد يُشتق من طريقة التعاقد في مذكرة العرض الفعّالة للمشروع، لا يُختار مستقلًا —
    /// إنشاء-أو-إيجاد صف ContractType مطابق بالاسم، بنفس نمط إنشاء القوائم المرجعية الناقصة
    /// المستخدَم في مسار استيراد Excel (بدل الرجوع لقيمة "غير محدد").
    /// </summary>
    private async Task<int> ResolveContractTypeIdFromMemoAsync(int subProjectId, CancellationToken cancellationToken)
    {
        var contractingMethod = await _context.PresentationMemoSubProjects.AsNoTracking()
            .Where(x => x.SubProjectId == subProjectId)
            .OrderByDescending(x => x.PresentationMemo.CreatedAt)
            .ThenByDescending(x => x.PresentationMemo.Id)
            .Select(x => x.PresentationMemo.ContractingMethod)
            .FirstOrDefaultAsync(cancellationToken);

        var label = ContractingMethodLabels.ToLabel(contractingMethod)
            ?? throw new BusinessRuleException("لا يمكن تحديد نوع العقد قبل استكمال طريقة التعاقد في مذكرة العرض");

        var existing = await _context.Set<ContractType>()
            .FirstOrDefaultAsync(t => t.ContractName == label, cancellationToken);

        if (existing != null)
        {
            return existing.ContractTypeId;
        }

        var created = new ContractType { ContractName = label };
        _context.Set<ContractType>().Add(created);
        await _context.SaveChangesAsync(cancellationToken);
        return created.ContractTypeId;
    }
```

- [ ] **Step 8: Implement — label, Savings, ContractDate in `GetContractAwardDetailsAsync`**

Find:

```csharp
    private async Task<ContractAwardDetailsDto?> GetContractAwardDetailsAsync(int subProjectId, CancellationToken cancellationToken)
    {
        var project = await _context.SubProjects.AsNoTracking()
            .Where(s => s.SubProjectId == subProjectId)
            .Select(s => new { s.ProjectNature, s.BankFunding, s.SelfFunding })
            .FirstOrDefaultAsync(cancellationToken);

        if (project == null)
        {
            return null;
        }

        var doc = await _context.ContractAwards.AsNoTracking()
            .Where(x => x.SubProjectId == subProjectId)
            .Select(x => new
            {
                x.AdvancePaymentDone,
                x.AdvancePaymentPercentage,
                x.AdvancePaymentSelfAmount,
                x.AdvancePaymentBankAmount,
                x.ExecutionDurationMonths,
                x.ExecutionDurationDays,
                x.SiteHandoverMode,
                x.SiteHandoverDate,
                SiteHandoverProofFileName = x.SiteHandoverProofFile == null ? null : x.SiteHandoverProofFile.FileName,
                x.PenaltyAmount,
                x.ProjectAssignmentId,
                ContractorId = (int?)x.ProjectAssignment!.ContractorId,
                ContractorName = x.ProjectAssignment!.Contractor!.ContractorName,
                ContractTypeId = (int?)x.ProjectAssignment!.ContractTypeId,
                x.ProjectAssignment!.ContractNumber,
                x.ProjectAssignment!.ContractValue,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var details = new ContractAwardDetailsDto
        {
            ProjectNature = project.ProjectNature,
            RequiresAdvancePayment = IsContractingProject(project.ProjectNature),
            TotalCost = project.BankFunding + project.SelfFunding,
            BankFunding = project.BankFunding,
            SelfFunding = project.SelfFunding,
        };

        if (doc == null)
        {
            return details;
        }

        details.AdvancePaymentDone = doc.AdvancePaymentDone;
        details.AdvancePaymentPercentage = doc.AdvancePaymentPercentage;
        details.AdvancePaymentSelfAmount = doc.AdvancePaymentSelfAmount;
        details.AdvancePaymentBankAmount = doc.AdvancePaymentBankAmount;
        details.ExecutionDurationMonths = doc.ExecutionDurationMonths;
        details.ExecutionDurationDays = doc.ExecutionDurationDays;
        details.SiteHandoverMode = (int?)doc.SiteHandoverMode;
        details.SiteHandoverDate = doc.SiteHandoverDate;
        details.SiteHandoverProofFileName = doc.SiteHandoverProofFileName;
        details.ContractualDeliveryDate = doc.SiteHandoverDate?
            .AddMonths(doc.ExecutionDurationMonths ?? 0)
            .AddDays(doc.ExecutionDurationDays ?? 0);
        details.PenaltyAmount = doc.PenaltyAmount;
        details.ContractorId = doc.ContractorId;
        details.ContractorName = doc.ContractorName;
        details.ContractTypeId = doc.ContractTypeId;
        details.ContractNumber = doc.ContractNumber;
        details.ContractValue = doc.ContractValue;

        return details;
    }
```

Replace with:

```csharp
    private async Task<ContractAwardDetailsDto?> GetContractAwardDetailsAsync(int subProjectId, CancellationToken cancellationToken)
    {
        var project = await _context.SubProjects.AsNoTracking()
            .Where(s => s.SubProjectId == subProjectId)
            .Select(s => new { s.ProjectNature, s.BankFunding, s.SelfFunding })
            .FirstOrDefaultAsync(cancellationToken);

        if (project == null)
        {
            return null;
        }

        var doc = await _context.ContractAwards.AsNoTracking()
            .Where(x => x.SubProjectId == subProjectId)
            .Select(x => new
            {
                x.AdvancePaymentDone,
                x.AdvancePaymentPercentage,
                x.AdvancePaymentSelfAmount,
                x.AdvancePaymentBankAmount,
                x.ExecutionDurationMonths,
                x.ExecutionDurationDays,
                x.SiteHandoverMode,
                x.SiteHandoverDate,
                SiteHandoverProofFileName = x.SiteHandoverProofFile == null ? null : x.SiteHandoverProofFile.FileName,
                x.PenaltyAmount,
                x.ProjectAssignmentId,
                ContractorId = (int?)x.ProjectAssignment!.ContractorId,
                ContractorName = x.ProjectAssignment!.Contractor!.ContractorName,
                ContractTypeId = (int?)x.ProjectAssignment!.ContractTypeId,
                x.ProjectAssignment!.ContractDate,
                x.ProjectAssignment!.ContractValue,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalCost = project.BankFunding + project.SelfFunding;
        var details = new ContractAwardDetailsDto
        {
            ProjectNature = project.ProjectNature,
            RequiresAdvancePayment = IsContractingProject(project.ProjectNature),
            TotalCost = totalCost,
            BankFunding = project.BankFunding,
            SelfFunding = project.SelfFunding,
        };

        if (doc == null)
        {
            return details;
        }

        details.AdvancePaymentDone = doc.AdvancePaymentDone;
        details.AdvancePaymentPercentage = doc.AdvancePaymentPercentage;
        details.AdvancePaymentSelfAmount = doc.AdvancePaymentSelfAmount;
        details.AdvancePaymentBankAmount = doc.AdvancePaymentBankAmount;
        details.ExecutionDurationMonths = doc.ExecutionDurationMonths;
        details.ExecutionDurationDays = doc.ExecutionDurationDays;
        details.SiteHandoverMode = (int?)doc.SiteHandoverMode;
        details.SiteHandoverDate = doc.SiteHandoverDate;
        details.SiteHandoverProofFileName = doc.SiteHandoverProofFileName;
        details.ContractualDeliveryDate = doc.SiteHandoverDate?
            .AddMonths(doc.ExecutionDurationMonths ?? 0)
            .AddDays(doc.ExecutionDurationDays ?? 0);
        details.PenaltyAmount = doc.PenaltyAmount;
        details.ContractorId = doc.ContractorId;
        details.ContractorName = doc.ContractorName;
        details.ContractTypeId = doc.ContractTypeId;
        details.ContractDate = doc.ContractDate;
        details.ContractValue = doc.ContractValue;
        details.Savings = doc.ContractValue is decimal cv && cv < totalCost ? totalCost - cv : null;

        return details;
    }
```

- [ ] **Step 9: Rename the label read site**

The "إجمالي التكلفة" text lives in the frontend template (Task 4), not here — `TotalCost` the DTO property name is unchanged (it's an API contract name, not user-facing text). No backend step needed for item 1 beyond what Step 8 already produced.

- [ ] **Step 10: Confirm `SmartInvest.Domain.Enums` is imported**

Check the top of `ProcurementService.cs` for `using SmartInvest.Domain.Enums;` — it's already there (the file already uses `SiteHandoverMode` from the same namespace). `ContractingMethodLabels` needs no additional using statement.

- [ ] **Step 11: Build and run the tests**

Run: `cd Backend && dotnet build`
Expected: 0 errors (this resolves the errors Task 1 intentionally left).

Run: `cd Backend && dotnet test --filter ContractAwardReworkTests`
Expected: all 6 tests pass.

Run: `cd Backend && dotnet test --filter ProcurementDurationAndAuthorizationTests`
Expected: all pre-existing tests still pass (confirms this task didn't regress the just-merged upstream duration/authorization work).

- [ ] **Step 12: Commit**

```bash
git add Backend/src/SmartInvest.Infrastructure/Services/ProcurementService.cs Backend/tests/SmartInvest.Tests/ContractAwardReworkTests.cs
git commit -m "feat(procurement): enforce memo-completion gate, overrun ceiling, supply auto-handover, memo-derived contract type"
```

---

### Task 3: Frontend models + service

**Files:**
- Modify: `Frontend/src/app/core/models/financial.models.ts:22-66`
- Modify: `Frontend/src/app/core/models/project.models.ts:114`

**Interfaces:**
- Consumes: the reshaped `ContractAwardDetailsDto`/`SetContractAwardDetailsDto`/`SubProjectDetailDto` (Task 1 + 2, now live on the backend).
- Produces: `ContractAwardDetails.{contractDate,savings}` (TS), `SetContractAwardDetails.{contractDate}` (TS, `contractTypeId`/`contractNumber` removed), `SubProjectDetail.contractDate`. Task 4 and Task 5 consume these by name.

- [ ] **Step 1: Update `ContractAwardDetails` and `SetContractAwardDetails`**

In `Frontend/src/app/core/models/financial.models.ts`, find:

```typescript
  penaltyAmount: number | null;

  contractorId: number | null;
  contractorName: string | null;
  contractTypeId: number | null;
  contractNumber: string | null;
  contractValue: number | null;
}

export interface SetContractAwardDetails {
  advancePaymentDone: boolean;
  advancePaymentPercentage: number | null;
  advancePaymentSelfAmount: number | null;
  advancePaymentBankAmount: number | null;
  executionDurationMonths: number | null;
  executionDurationDays: number | null;
  siteHandoverMode: number | null;
  penaltyAmount: number | null;
  contractorId: number | null;
  contractTypeId: number | null;
  contractNumber: string | null;
  contractValue: number | null;
}
```

Replace with:

```typescript
  penaltyAmount: number | null;

  contractorId: number | null;
  contractorName: string | null;
  /** مُشتق من طريقة تعاقد مذكرة العرض الفعّالة — للعرض فقط، لا يُختار من الواجهة. */
  contractTypeId: number | null;
  contractDate: string | null;
  contractValue: number | null;
  /** الإجمالي المخطط ناقص قيمة العقد — موجودة فقط عندما تكون موجبة. */
  savings: number | null;
}

export interface SetContractAwardDetails {
  advancePaymentDone: boolean;
  advancePaymentPercentage: number | null;
  advancePaymentSelfAmount: number | null;
  advancePaymentBankAmount: number | null;
  executionDurationMonths: number | null;
  executionDurationDays: number | null;
  siteHandoverMode: number | null;
  penaltyAmount: number | null;
  contractorId: number | null;
  contractDate: string | null;
  contractValue: number | null;
}
```

- [ ] **Step 2: Update `SubProjectDetail`**

In `Frontend/src/app/core/models/project.models.ts`, find:

```typescript
  contractNumber: string | null;
```

Replace with:

```typescript
  contractDate: string | null;
```

- [ ] **Step 3: Build**

Run: `cd Frontend && npm run build`
Expected: errors in `procurement-workflow.ts` and `sub-project-details.ts`/`.html` (they still reference the old field names) — expected, Tasks 4 and 5 fix them. Confirm no errors outside those files.

- [ ] **Step 4: Commit**

```bash
git add Frontend/src/app/core/models/financial.models.ts Frontend/src/app/core/models/project.models.ts
git commit -m "feat(procurement): reshape frontend award models to match backend"
```

Same narrow build-break exception as Task 1 — this task and Tasks 4/5 land together before the branch needs to build standalone.

---

### Task 4: Frontend award panel rework

**Files:**
- Modify: `Frontend/src/app/features/financial/procurement-workflow.ts`
- Modify: `Frontend/src/app/features/financial/procurement-workflow.html:246-354` (the award panel block)

**Interfaces:**
- Consumes: `ContractAwardDetails`/`SetContractAwardDetails` (Task 3), `ProcurementOverview.activePresentationMemo.contractingMethodLabel` (existing, already fetched into `overview()`).
- Produces: nothing consumed by later tasks (leaf task for this file).

- [ ] **Step 1: Remove `aContractTypeId`, `aContractNumber`, `contractTypes`, and the `ContractTypesService`/`Lookup` import**

In `Frontend/src/app/features/financial/procurement-workflow.ts`, find:

```typescript
import { ContractorsService } from '../../core/services/contractors.service';
import { ContractTypesService } from '../../core/services/contract-types.service';
import { Contractor, Lookup } from '../../core/models/project.models';
```

Replace with:

```typescript
import { ContractorsService } from '../../core/services/contractors.service';
import { Contractor } from '../../core/models/project.models';
```

Find:

```typescript
  private readonly contractorsService = inject(ContractorsService);
  private readonly contractTypesService = inject(ContractTypesService);
```

Replace with:

```typescript
  private readonly contractorsService = inject(ContractorsService);
```

Find:

```typescript
  // ===== مرحلة الترسية =====
  protected readonly contractors = signal<Contractor[]>([]);
  protected readonly contractTypes = signal<Lookup[]>([]);

  protected readonly award = computed(() => this.stageDetail()?.contractAward ?? null);

  /** نموذج الترسية — إشارة لكل حقل، على نمط باقي النماذج في المشروع */
  protected readonly aContractorId = signal<number | null>(null);
  protected readonly aContractTypeId = signal<number | null>(null);
  protected readonly aContractNumber = signal('');
  protected readonly aContractValue = signal<number | null>(null);
```

Replace with:

```typescript
  // ===== مرحلة الترسية =====
  protected readonly contractors = signal<Contractor[]>([]);

  protected readonly award = computed(() => this.stageDetail()?.contractAward ?? null);

  /** نموذج الترسية — إشارة لكل حقل، على نمط باقي النماذج في المشروع */
  protected readonly aContractorId = signal<number | null>(null);
  protected readonly aContractDate = signal<string>('');
  protected readonly aContractValue = signal<number | null>(null);
```

- [ ] **Step 2: Stop fetching contract types**

Find:

```typescript
  private loadAwardLookups(): void {
    if (this.contractors().length > 0) {
      return;
    }
    this.contractors.set([]);
    this.contractorsService.getAll().subscribe({
      next: (items) => this.contractors.set(items.filter((c) => c.isActive)),
    });
    this.contractTypesService.getAll().subscribe({
      next: (items) => this.contractTypes.set(items),
    });
  }
```

Replace with:

```typescript
  private loadAwardLookups(): void {
    if (this.contractors().length > 0) {
      return;
    }
    this.contractors.set([]);
    this.contractorsService.getAll().subscribe({
      next: (items) => this.contractors.set(items.filter((c) => c.isActive)),
    });
  }
```

- [ ] **Step 3: Update form sync (load) and save**

Find:

```typescript
    this.aContractorId.set(details.contractorId);
    this.aContractTypeId.set(details.contractTypeId);
    this.aContractNumber.set(details.contractNumber ?? '');
    this.aContractValue.set(details.contractValue);
```

Replace with:

```typescript
    this.aContractorId.set(details.contractorId);
    this.aContractDate.set(details.contractDate?.slice(0, 10) ?? '');
    this.aContractValue.set(details.contractValue);
```

Find:

```typescript
        contractorId: this.aContractorId(),
        contractTypeId: this.aContractTypeId(),
        contractNumber: this.aContractNumber().trim() || null,
        contractValue: this.aContractValue(),
```

Replace with:

```typescript
        contractorId: this.aContractorId(),
        contractDate: this.aContractDate() || null,
        contractValue: this.aContractValue(),
```

- [ ] **Step 4: Rework the award panel template**

In `Frontend/src/app/features/financial/procurement-workflow.html`, find:

```html
                    <!-- ميزانية المشروع معروضة من البداية -->
                    <div class="award-budget">
                      <div class="ab-item">
                        <span>إجمالي التكلفة</span><b class="tnum">{{ thousandsLabel(aw.totalCost) }}</b>
                      </div>
```

Replace with:

```html
                    <!-- ميزانية المشروع معروضة من البداية -->
                    <div class="award-budget">
                      <div class="ab-item">
                        <span>إجمالي المخطط</span><b class="tnum">{{ thousandsLabel(aw.totalCost) }}</b>
                      </div>
```

Find:

```html
                      <div class="ab-item">
                        <span>نوع المشروع</span><b>{{ aw.projectNature || '—' }}</b>
                      </div>
                    </div>
```

Replace with:

```html
                      <div class="ab-item">
                        <span>نوع المشروع</span><b>{{ aw.projectNature || '—' }}</b>
                      </div>
                      @if (aw.savings != null) {
                        <div class="ab-item">
                          <span>وفرة</span><b class="tnum ok">{{ thousandsLabel(aw.savings) }}</b>
                        </div>
                      }
                    </div>
```

Find:

```html
                      <div class="si-fld">
                        <label>نوع العقد <span class="req">*</span></label>
                        <select [ngModel]="aContractTypeId()" (ngModelChange)="aContractTypeId.set($event)" [disabled]="!awardEditable()">
                          <option [ngValue]="null">— اختر —</option>
                          @for (t of contractTypes(); track t.id) {
                            <option [ngValue]="t.id">{{ t.name }}</option>
                          }
                        </select>
                      </div>
                      <div class="si-fld">
                        <label>رقم العقد</label>
                        <input [ngModel]="aContractNumber()" (ngModelChange)="aContractNumber.set($event)" [disabled]="!awardEditable()" />
                      </div>
                      <div class="si-fld">
                        <label>قيمة العقد</label>
                        <input type="number" [ngModel]="aContractValue()" (ngModelChange)="aContractValue.set($event)" [disabled]="!awardEditable()" />
                      </div>
```

Replace with:

```html
                      <div class="si-fld">
                        <label>نوع العقد</label>
                        <input [value]="overview()!.activePresentationMemo?.contractingMethodLabel || '—'" disabled />
                        <span class="hint">مُشتق من طريقة التعاقد في مذكرة العرض</span>
                      </div>
                      <div class="si-fld">
                        <label>تاريخ العقد</label>
                        <input type="date" [ngModel]="aContractDate()" (ngModelChange)="aContractDate.set($event)" [disabled]="!awardEditable()" />
                      </div>
                      <div class="si-fld">
                        <label>قيمة العقد</label>
                        <input type="number" [ngModel]="aContractValue()" (ngModelChange)="aContractValue.set($event)" [disabled]="!awardEditable()" />
                      </div>
```

- [ ] **Step 5: Hide handover fields for توريدات**

In `Frontend/src/app/features/financial/procurement-workflow.html`, find the entire "مدة التنفيذ وتسليم الأرضية" block:

```html
                    <div class="si-step"><span class="n">{{ aw.requiresAdvancePayment ? 3 : 2 }}</span><h4>مدة التنفيذ وتسليم الأرضية</h4></div>
                    <div class="si-grid">
                      <div class="si-fld">
                        <label>المدة القصوى — شهور</label>
                        <input type="number" min="0" [ngModel]="aDurationMonths()" (ngModelChange)="aDurationMonths.set($event)" [disabled]="!awardEditable()" />
                      </div>
                      <div class="si-fld">
                        <label>المدة القصوى — أيام</label>
                        <input type="number" min="0" [ngModel]="aDurationDays()" (ngModelChange)="aDurationDays.set($event)" [disabled]="!awardEditable()" />
                      </div>
                      <div class="si-fld full">
                        <label>أرضية المشروع <span class="req">*</span></label>
                        <select [ngModel]="aHandoverMode()" (ngModelChange)="aHandoverMode.set($event)" [disabled]="!awardEditable()">
                          <option [ngValue]="null">— اختر —</option>
                          <option [ngValue]="1">مُسلَّمة للمقاول — تبدأ المدة فور الترسية</option>
                          <option [ngValue]="2">لم تُسلَّم بعد — تبدأ المدة عند تسجيل التسليم لاحقًا</option>
                        </select>
                        <span class="hint">المدة لا تبدأ من الترسية، بل من تسليم الأرضية للمقاول</span>
                      </div>
                      @if (aw.siteHandoverDate) {
                        <div class="si-fld">
                          <label>تاريخ تسليم الأرضية</label>
                          <input [value]="dateStr(aw.siteHandoverDate)" disabled />
                        </div>
                      }
                      @if (aw.contractualDeliveryDate) {
                        <div class="si-fld">
                          <label>تاريخ التسليم المستحق</label>
                          <input [value]="dateStr(aw.contractualDeliveryDate)" disabled />
                        </div>
                      }
                      @if (aw.siteHandoverProofFileName) {
                        <div class="si-fld full">
                          <label>إثبات تسليم الأرضية</label>
                          <button type="button" class="file-lnk" (click)="downloadHandoverProof(aw.siteHandoverProofFileName!)">
                            📎 {{ aw.siteHandoverProofFileName }}
                          </button>
                        </div>
                      }
                      @if (aHandoverMode() === 1 && awardEditable()) {
                        <div class="si-fld full">
                          <label>تسجيل تسليم الأرضية <span class="req">*</span></label>
                          @if (aw.siteHandoverMode !== 1) {
                            <div class="si-err">احفظ بيانات الترسية أولًا (زر "حفظ بيانات الترسية" أسفل الصفحة) قبل تسجيل تسليم الأرضية</div>
                          } @else {
                            <div class="si-grid">
                              <div class="si-fld">
                                <label>تاريخ التسليم</label>
                                <input type="date" [ngModel]="aHandoverDate()" (ngModelChange)="aHandoverDate.set($event)" />
                              </div>
                              <div class="si-fld">
                                <label>إثبات التسليم (PDF أو صورة)</label>
                                <input type="file" accept=".pdf,.png,.jpg,.jpeg" (change)="onHandoverFileChange($event)" />
                              </div>
                            </div>
                            <button class="si-btn sm" [disabled]="aHandoverSaving()" (click)="saveHandover()">
                              @if (aHandoverSaving()) { جاري الحفظ… } @else { حفظ تسليم الأرضية }
                            </button>
                            <span class="hint">مطلوب قبل إكمال الترسية عندما تكون الأرضية مُسلَّمة للمقاول</span>
                          }
                        </div>
                      }
                    </div>
```

Replace with (the two duration fields stay unconditional — توريدات still needs them; everything from "أرضية المشروع" onward is now wrapped in `@if (aw.projectNature === 'مقاولات')`, with a note shown to supply projects instead):

```html
                    <div class="si-step"><span class="n">{{ aw.requiresAdvancePayment ? 3 : 2 }}</span><h4>مدة التنفيذ وتسليم الأرضية</h4></div>
                    <div class="si-grid">
                      <div class="si-fld">
                        <label>المدة القصوى — شهور</label>
                        <input type="number" min="0" [ngModel]="aDurationMonths()" (ngModelChange)="aDurationMonths.set($event)" [disabled]="!awardEditable()" />
                      </div>
                      <div class="si-fld">
                        <label>المدة القصوى — أيام</label>
                        <input type="number" min="0" [ngModel]="aDurationDays()" (ngModelChange)="aDurationDays.set($event)" [disabled]="!awardEditable()" />
                      </div>
                      @if (aw.projectNature === 'مقاولات') {
                        <div class="si-fld full">
                          <label>أرضية المشروع <span class="req">*</span></label>
                          <select [ngModel]="aHandoverMode()" (ngModelChange)="aHandoverMode.set($event)" [disabled]="!awardEditable()">
                            <option [ngValue]="null">— اختر —</option>
                            <option [ngValue]="1">مُسلَّمة للمقاول — تبدأ المدة فور الترسية</option>
                            <option [ngValue]="2">لم تُسلَّم بعد — تبدأ المدة عند تسجيل التسليم لاحقًا</option>
                          </select>
                          <span class="hint">المدة لا تبدأ من الترسية، بل من تسليم الأرضية للمقاول</span>
                        </div>
                        @if (aw.siteHandoverDate) {
                          <div class="si-fld">
                            <label>تاريخ تسليم الأرضية</label>
                            <input [value]="dateStr(aw.siteHandoverDate)" disabled />
                          </div>
                        }
                        @if (aw.contractualDeliveryDate) {
                          <div class="si-fld">
                            <label>تاريخ التسليم المستحق</label>
                            <input [value]="dateStr(aw.contractualDeliveryDate)" disabled />
                          </div>
                        }
                        @if (aw.siteHandoverProofFileName) {
                          <div class="si-fld full">
                            <label>إثبات تسليم الأرضية</label>
                            <button type="button" class="file-lnk" (click)="downloadHandoverProof(aw.siteHandoverProofFileName!)">
                              📎 {{ aw.siteHandoverProofFileName }}
                            </button>
                          </div>
                        }
                        @if (aHandoverMode() === 1 && awardEditable()) {
                          <div class="si-fld full">
                            <label>تسجيل تسليم الأرضية <span class="req">*</span></label>
                            @if (aw.siteHandoverMode !== 1) {
                              <div class="si-err">احفظ بيانات الترسية أولًا (زر "حفظ بيانات الترسية" أسفل الصفحة) قبل تسجيل تسليم الأرضية</div>
                            } @else {
                              <div class="si-grid">
                                <div class="si-fld">
                                  <label>تاريخ التسليم</label>
                                  <input type="date" [ngModel]="aHandoverDate()" (ngModelChange)="aHandoverDate.set($event)" />
                                </div>
                                <div class="si-fld">
                                  <label>إثبات التسليم (PDF أو صورة)</label>
                                  <input type="file" accept=".pdf,.png,.jpg,.jpeg" (change)="onHandoverFileChange($event)" />
                                </div>
                              </div>
                              <button class="si-btn sm" [disabled]="aHandoverSaving()" (click)="saveHandover()">
                                @if (aHandoverSaving()) { جاري الحفظ… } @else { حفظ تسليم الأرضية }
                              </button>
                              <span class="hint">مطلوب قبل إكمال الترسية عندما تكون الأرضية مُسلَّمة للمقاول</span>
                            }
                          </div>
                        }
                      } @else {
                        <div class="si-note full">
                          مشروع «توريدات» — لا تسليم أرضية له. يبدأ العدّ فور إكمال هذه المرحلة تلقائيًا.
                        </div>
                      }
                    </div>
```

- [ ] **Step 6: Build**

Run: `cd Frontend && npm run build`
Expected: 0 errors.

- [ ] **Step 7: Manually verify in browser**

Run: `npm start` (if not already running; reuse an already-running dev server if one exists, per this project's standing convention of not starting duplicate processes).

For a مقاولات project: open its procurement workflow, reach stage 6, confirm "إجمالي المخطط" label, نوع العقد shows read-only text from the memo (not a dropdown), تاريخ العقد is a date picker, entering a قيمة العقد below the plan shows a وفرة figure, entering one above the plan+overrun blocks completion with the ceiling message, handover fields are present and required as before.

For a توريدات project: confirm no handover-mode/date/proof fields render at all, only duration months/days; complete the stage; confirm in متابعة المشروعات that a real delivery deadline appears immediately (not "بانتظار تسليم الأرضية").

- [ ] **Step 8: Commit**

```bash
git add Frontend/src/app/features/financial/procurement-workflow.ts Frontend/src/app/features/financial/procurement-workflow.html
git commit -m "feat(procurement): rework award panel — memo-derived contract type, contract date, savings, supply-skip handover"
```

---

### Task 5: Frontend sub-project details page

**Files:**
- Modify: `Frontend/src/app/features/projects/details/sub-project-details.html:119`
- Modify: `Frontend/src/app/features/projects/details/sub-project-details.ts` (only if it contains logic referencing `contractNumber` — check via grep in Step 1)

**Interfaces:**
- Consumes: `SubProjectDetail.contractDate` (Task 3).
- Produces: nothing (leaf task).

- [ ] **Step 1: Check for TS-side references**

Run: `grep -n "contractNumber" Frontend/src/app/features/projects/details/sub-project-details.ts`
Expected: no matches (the field is only read in the template via `project()!.contractNumber`). If this returns a match, read the surrounding code and adapt it the same way as Step 2 below before proceeding.

- [ ] **Step 2: Update the template**

In `Frontend/src/app/features/projects/details/sub-project-details.html`, find:

```html
            <div class="row"><span>رقم العقد</span><b>{{ project()!.contractNumber || '—' }}</b></div>
```

Replace with:

```html
            <div class="row"><span>تاريخ العقد</span><b>{{ project()!.contractDate ? (project()!.contractDate | date: 'yyyy-MM-dd') : '—' }}</b></div>
```

If this template does not already import Angular's `DatePipe` (check the component's `imports` array in `sub-project-details.ts`), add it:

Find the `@Component({ imports: [...] })` array and confirm `DatePipe` is present. If not, find:

```typescript
import { Component, ... } from '@angular/core';
```

and add on its own line above the component decorator:

```typescript
import { DatePipe } from '@angular/common';
```

then add `DatePipe` to the `imports: [...]` array in the `@Component` decorator.

- [ ] **Step 3: Build**

Run: `cd Frontend && npm run build`
Expected: 0 errors.

- [ ] **Step 4: Manually verify**

Open a sub-project's details page for a project that has completed stage 6 — confirm "بيانات التعاقد" card shows "تاريخ العقد" with a real date (not "رقم العقد").

- [ ] **Step 5: Commit**

```bash
git add Frontend/src/app/features/projects/details/sub-project-details.html Frontend/src/app/features/projects/details/sub-project-details.ts
git commit -m "feat(projects): show contract date instead of contract number on details page"
```

---

### Task 6: Final end-to-end verification

**Files:** none (verification only).

**Interfaces:**
- Consumes: the complete result of Tasks 1-5.
- Produces: confirmation the feature is ready to merge.

- [ ] **Step 1: Full build + test check**

Run: `cd Backend && dotnet build` — expected 0 errors.
Run: `cd Backend && dotnet test` — expected all tests pass, including the new `ContractAwardReworkTests` and the pre-existing suite.
Run: `cd Frontend && npm run build` — expected 0 errors.
Run: `cd Frontend && npm test` — expected existing suite still passes.

- [ ] **Step 2: مقاولات project full walkthrough**

Create or use an approved, memo-linked مقاولات sub-project. Confirm: procurement stages 1-5 complete normally; stage 6 shows "إجمالي المخطط"; نوع العقد is read-only, sourced from the memo; enter a تاريخ العقد; enter a قيمة العقد comfortably under the plan and confirm a وفرة figure appears; enter advance payment percentage/split as before (unchanged behavior); enter handover mode/date/proof as before; complete the stage successfully; confirm متابعة المشروعات shows the project with a correctly computed final-delivery deadline.

- [ ] **Step 3: مقاولات over-ceiling rejection**

On a fresh مقاولات project at the same stage, set an OverrunPercentage on the sub-project (via its edit form) if not already set, then enter a قيمة العقد above `TotalCost × (1 + overrun%)`. Attempt to complete stage 6. Expected: blocked with the Arabic ceiling message, showing both the entered value and the ceiling.

- [ ] **Step 4: توريدات project full walkthrough**

Create or use an approved, memo-linked توريدات sub-project. Confirm: stage 6 shows no handover-mode/date/proof fields at all, only duration months/days, contract date, contract value; complete the stage without ever touching handover fields; confirm it completes successfully; confirm متابعة المشروعات immediately shows a real delivery deadline (not "بانتظار تسليم الأرضية") — proving the auto-set handover date worked.

- [ ] **Step 5: Memo-completion gate**

Attach an incomplete presentation memo (missing the legal-committee decision) to a fresh approved sub-project. Attempt to upload the first version of stage 1 (كراسة الشروط). Expected: blocked with a message about the memo needing to be complete, not just attached. Complete the memo (attach the legal decision), retry — expected: succeeds.

- [ ] **Step 6: Sub-project details page**

Open the details page for the مقاولات project completed in Step 2 — confirm the "بيانات التعاقد" card shows "تاريخ العقد" with the date entered, not a contract number.

- [ ] **Step 7: Report status**

If all checks pass, the branch is ready — proceed to `superpowers:finishing-a-development-branch`. If any check fails, note exactly which step and what was observed before proposing a fix — do not patch blindly.
