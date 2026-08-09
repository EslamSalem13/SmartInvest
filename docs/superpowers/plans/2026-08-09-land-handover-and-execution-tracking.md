# تسليم الأرضية وربط التعاقدات بمتابعة المشروعات — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Record land-handover date + proof on a contract award, gate متابعة المشروعات on a completed award, auto-maintain a locked final-delivery stage, and add the app-wide toast feedback whose absence caused the reported "step 6 just refreshes" bug.

**Architecture:** A handover is always the pair `{date, proof}` stored once as an owned `StoredFile` on `ContractAward`, written through one service path reachable from step 6 (`AtAward`) or متابعة المشروعات (`Pending`). A single idempotent method, `ExecutionStageService.SyncFinalDeliveryStageAsync`, owns the final-delivery stage and is called from award completion, handover recording, and a backfill migration. The stage's stored `Deadline` is a projection of the still-computed `ContractAward.ContractualDeliveryDate`, never a second source of truth.

**Tech Stack:** .NET 10 (Onion: Domain/Application/Infrastructure/API), EF Core + SQL Server, Angular 21 standalone components with Signals.

**Spec:** `docs/superpowers/specs/2026-08-09-land-handover-and-execution-tracking-design.md`

## Global Constraints

- **No automated test suite exists in this project.** TDD steps are replaced throughout by: implement → build clean → verify live against real data → commit. Never claim a task passes without showing the build output and the live check.
- Backend build: `cd Backend && dotnet build src/SmartInvest.API/SmartInvest.API.csproj`. Three pre-existing `CS8620` warnings in `SubProjectRepository.cs` are expected and are **not** regressions.
- Frontend build: `cd Frontend && npx ng build`. Pre-existing CSS-budget warnings for `home/projects/contractors/financial/users/follow-up-list` css and the `leaflet` non-ESM warning are expected.
- All user-facing error messages are Arabic, thrown as `BusinessRuleException` (from `SmartInvest.Application.Common.Exceptions`). FluentValidation is unwired codebase-wide — do **not** introduce it.
- Stored files use the existing owned-type pattern: `StoredFile` + `builder.OwnsStoredFile(x => x.Prop, "Prefix_")`.
- Money is always full EGP. Dates crossing the API are UTC.
- The final-delivery stage name is exactly `التسليم النهائي`.
- **git-bash gotcha:** `curl -d '{...}'` with inline Arabic silently mangles UTF-8 into `?`. Always write the JSON to a file first and use `curl --data-binary @file`.
- Do not start dev servers with `preview_start`. Run them manually and background them.
- Commit after each task. Do not push.
- **Every commit must build on its own.** Before committing, run `git status --porcelain` and confirm no modified file that the task touched is left unstaged — a task's `git add` paths are a reminder, not an exhaustive list. Task 1 originally shipped a commit that did not compile because a changed `SmartInvest.Application` file was never staged.

---

### Task 1: Schema — nullable deadline, final-delivery flag, handover proof, backfill

**Files:**
- Modify: `Backend/src/SmartInvest.Domain/Entities/ExecutionStage.cs:18`
- Modify: `Backend/src/SmartInvest.Domain/Entities/Procurement/ContractAward.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Data/Configurations/ExecutionStageConfiguration.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Data/Configurations/Procurement/ContractAwardConfiguration.cs`
- Create: `Backend/src/SmartInvest.Infrastructure/Migrations/<timestamp>_AddSiteHandoverProofAndFinalDeliveryStage.cs` (EF-generated, then hand-edited)

**Interfaces:**
- Consumes: nothing (first task)
- Produces: `ExecutionStage.Deadline` is now `DateTime?`; `ExecutionStage.IsFinalDelivery` is `bool`; `ContractAward.SiteHandoverProofFile` is `StoredFile?` with column prefix `SiteHandoverProof_`

- [ ] **Step 1: Make `Deadline` nullable and add the final-delivery flag**

In `ExecutionStage.cs`, replace line 18:

```csharp
    public DateTime? Deadline { get; set; }

    /// <summary>
    /// مرحلة التسليم النهائي المُدارة تلقائيًا — تُنشأ عند إكمال الترسية ويُعاد حساب موعدها
    /// من تاريخ تسليم الأرضية + مدة التنفيذ. لا يملك المستخدم إنشاءها أو تعديل موعدها.
    /// Deadline تكون null طالما لم تُسلَّم الأرضية بعد (لا يوجد تاريخ حقيقي يُحسب منه).
    /// </summary>
    public bool IsFinalDelivery { get; set; }
```

- [ ] **Step 2: Add the handover proof file to `ContractAward`**

In `ContractAward.cs`, immediately after the `SiteHandoverDate` property:

```csharp
        /// <summary>إثبات تسليم الأرضية للمقاول (PDF أو صورة) — إلزامي عند تسجيل التسليم.</summary>
        public StoredFile? SiteHandoverProofFile { get; set; }
```

Add `using SmartInvest.Domain.Common;` to the file's usings if not already present.

- [ ] **Step 3: Configure the owned file**

In `ContractAwardConfiguration.cs`, inside `Configure`, add:

```csharp
        builder.OwnsStoredFile(x => x.SiteHandoverProofFile, "SiteHandoverProof_");
```

- [ ] **Step 4: Make the minimum compile fixes for the nullable deadline**

Making the column nullable breaks two spots that assign `DateTime?` into `DateTime`. Fix exactly these — everything else about the DTO belongs to Task 2. Without this the project will not build, and `dotnet ef` in Step 6 builds before it runs.

In `Backend/src/SmartInvest.Application/DTOs/ExecutionStageDtos.cs:8`, inside `ExecutionStageDto`:

```csharp
    /// <summary>null فقط لمرحلة التسليم النهائي قبل تسليم الأرضية.</summary>
    public DateTime? Deadline { get; set; }
```

Leave `CreateExecutionStageDto.Deadline` as non-nullable `DateTime` — user-created stages still require a date.

In `Backend/src/SmartInvest.Infrastructure/Services/ExecutionStageService.cs`, in the private `FollowUpStageProjection` class:

```csharp
        public DateTime? Deadline { get; set; }
```

And in `GetFollowUpListAsync`, replace the `nextDeadline` line so a dateless stage can never be picked as "next":

```csharp
            var nextDeadline = stages
                .Where(x => !x.IsCompleted && x.Deadline != null)
                .OrderBy(x => x.Deadline)
                .FirstOrDefault()?.Deadline;
```

- [ ] **Step 5: Generate the migration**

```bash
cd Backend && dotnet ef migrations add AddSiteHandoverProofAndFinalDeliveryStage --project src/SmartInvest.Infrastructure --startup-project src/SmartInvest.API
```

Expected: `Done. To undo this action, use 'ef migrations remove'`. If the backend is running it will lock the DLLs — stop it first.

- [ ] **Step 6: Hand-add the backfill to the generated migration**

At the **end** of the generated `Up` method, append. Column names verified against the live schema:

```csharp
            // كل ترسية مكتملة موجودة بالفعل تحصل على مرحلة التسليم النهائي، حتى تتطابق
            // البيانات القديمة مع الجديدة من أول يوم. NOT EXISTS يجعله آمنًا لإعادة التشغيل.
            migrationBuilder.Sql(@"
INSERT INTO ExecutionStages
    (SubProjectId, Name, Deadline, SelfFundingSpent, BankFundingSpent,
     PhysicalProgressPercent, PenaltyPaid, IsCompleted, IsFinalDelivery, CreatedAt)
SELECT ca.SubProjectId,
       N'التسليم النهائي',
       CASE WHEN ca.SiteHandoverDate IS NULL THEN NULL
            ELSE DATEADD(DAY, ISNULL(ca.ExecutionDurationDays, 0),
                 DATEADD(MONTH, ISNULL(ca.ExecutionDurationMonths, 0), ca.SiteHandoverDate))
       END,
       0, 0, 0, 0, 0, 1, SYSUTCDATETIME()
FROM ContractAwards ca
WHERE ca.IsCompleted = 1
  AND NOT EXISTS (
      SELECT 1 FROM ExecutionStages e
      WHERE e.SubProjectId = ca.SubProjectId AND e.IsFinalDelivery = 1);
");
```

In the `Down` method, before the column drops:

```csharp
            migrationBuilder.Sql("DELETE FROM ExecutionStages WHERE IsFinalDelivery = 1;");
```

- [ ] **Step 7: Build**

Run: `cd Backend && dotnet build src/SmartInvest.API/SmartInvest.API.csproj`
Expected: `Build succeeded.` with `0 Error(s)`, only the 3 known `CS8620` warnings.

If anything still fails to compile on `Deadline`, fix it the same minimal way (accept `DateTime?`) — do not add the Task 2 fields to work around it.

- [ ] **Step 8: Apply and verify the backfill**

```bash
cd Backend && dotnet ef database update --project src/SmartInvest.Infrastructure --startup-project src/SmartInvest.API
```

Then verify — sub-project 1 has a completed award with a handover date, 1770 does not:

```bash
sqlcmd -S . -E -C -d SmartInvestDB -W -Q "SET NOCOUNT ON; SELECT SubProjectId, Name, Deadline, IsFinalDelivery FROM ExecutionStages WHERE IsFinalDelivery = 1;"
```

Expected: exactly **one** row, `SubProjectId=1`, `Name=التسليم النهائي`, `Deadline=2027-02-08` (sub-project 1's handover 2026-08-08 + 6 months). No row for 1770.

- [ ] **Step 9: Commit**

```bash
git add Backend/src
git commit -m "feat: schema for handover proof + auto-managed final delivery stage"
```

`Backend/src` (not just Domain + Infrastructure) — Step 4 edits `SmartInvest.Application`, and omitting it produces a commit that does not compile.

---

### Task 2: `SyncFinalDeliveryStageAsync` + nullable-deadline fallout

**Files:**
- Modify: `Backend/src/SmartInvest.Application/Interfaces/IExecutionStageService.cs`
- Modify: `Backend/src/SmartInvest.Application/DTOs/ExecutionStageDtos.cs:8`
- Modify: `Backend/src/SmartInvest.Infrastructure/Services/ExecutionStageService.cs`

**Interfaces:**
- Consumes (all from Task 1): `ExecutionStage.IsFinalDelivery`; `ExecutionStage.Deadline` and `ExecutionStageDto.Deadline` are already `DateTime?`; `FollowUpStageProjection.Deadline` is already nullable with its `nextDeadline` null guard in place
- Produces:
  - `Task SyncFinalDeliveryStageAsync(int subProjectId, CancellationToken cancellationToken = default)` on `IExecutionStageService`
  - New `ExecutionStageDto.IsFinalDelivery` (bool) and `ExecutionStageDto.ExceedsContractualDeadline` (bool)

- [ ] **Step 1: Add the method to the interface**

In `IExecutionStageService.cs`, after `GetFollowUpListAsync`:

```csharp
    /// <summary>
    /// ينشئ/يحدّث مرحلة التسليم النهائي المُدارة تلقائيًا لهذا المشروع. آمن للاستدعاء المتكرر —
    /// لا يُنشئ صفًا مكررًا ولا يمس ما سُجِّل على الصف من صرف أو نسبة تنفيذ أو غرامة.
    /// لا يفعل شيئًا إذا لم تكن الترسية مكتملة.
    /// </summary>
    Task SyncFinalDeliveryStageAsync(int subProjectId, CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Add the two new DTO flags**

Task 1 already made `ExecutionStageDto.Deadline` nullable. Add these two properties directly beneath it in `ExecutionStageDtos.cs`:

```csharp
    /// <summary>مرحلة التسليم النهائي المُدارة تلقائيًا — مقفولة في الواجهة.</summary>
    public bool IsFinalDelivery { get; set; }

    /// <summary>موعد هذه المرحلة يتجاوز تاريخ التسليم التعاقدي — تحذير فقط، لا يمنع الحفظ.</summary>
    public bool ExceedsContractualDeadline { get; set; }
```

**Do not add `IsFinalDelivery` to `CreateExecutionStageDto`.** Its absence is what makes "users cannot create a final-delivery stage" true by construction, with no runtime guard to forget. `SyncFinalDeliveryStageAsync` is the only writer of that flag.

- [ ] **Step 3: Implement the sync method**

In `ExecutionStageService.cs`, add this constant just below the field declarations (above the constructor):

```csharp
    public const string FinalDeliveryStageName = "التسليم النهائي";
```

Then add these two methods after `GetFollowUpListAsync`:

```csharp
    public async Task SyncFinalDeliveryStageAsync(int subProjectId, CancellationToken cancellationToken = default)
    {
        var award = await _context.ContractAwards.AsNoTracking()
            .FirstOrDefaultAsync(a => a.SubProjectId == subProjectId, cancellationToken);

        if (award is not { IsCompleted: true })
        {
            return;
        }

        var deadline = ComputeContractualDeliveryDate(
            award.SiteHandoverDate, award.ExecutionDurationMonths, award.ExecutionDurationDays);

        var stage = await _context.ExecutionStages
            .FirstOrDefaultAsync(s => s.SubProjectId == subProjectId && s.IsFinalDelivery, cancellationToken);

        if (stage == null)
        {
            stage = new ExecutionStage
            {
                SubProjectId = subProjectId,
                Name = FinalDeliveryStageName,
                IsFinalDelivery = true,
                Deadline = deadline,
            };
            await _stageRepository.AddAsync(stage, cancellationToken);
        }
        else
        {
            // الاسم والموعد فقط يُداران تلقائيًا — الصرف والنسبة والغرامة تبقى كما سجّلها الموظف
            stage.Name = FinalDeliveryStageName;
            stage.Deadline = deadline;
            _stageRepository.Update(stage);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static DateTime? ComputeContractualDeliveryDate(DateTime? handoverDate, int? months, int? days) =>
        handoverDate?.AddMonths(months ?? 0).AddDays(days ?? 0);
```

- [ ] **Step 4: Compute the contractual date for DTO mapping**

Add this helper next to `GetOwnedStageAsync`:

```csharp
    /// <summary>تاريخ التسليم التعاقدي المحسوب — null لو الترسية غير مكتملة أو الأرضية لم تُسلَّم.</summary>
    private async Task<DateTime?> GetContractualDeliveryDateAsync(int subProjectId, CancellationToken cancellationToken)
    {
        var award = await _context.ContractAwards.AsNoTracking()
            .Where(a => a.SubProjectId == subProjectId && a.IsCompleted)
            .Select(a => new { a.SiteHandoverDate, a.ExecutionDurationMonths, a.ExecutionDurationDays })
            .FirstOrDefaultAsync(cancellationToken);

        return award == null
            ? null
            : ComputeContractualDeliveryDate(award.SiteHandoverDate, award.ExecutionDurationMonths, award.ExecutionDurationDays);
    }
```

- [ ] **Step 5: Update `ToDto` and its four call sites**

Replace the `ToDto` method (currently at the end of the file) with:

```csharp
    private static ExecutionStageDto ToDto(ExecutionStage s, DateTime? contractualDeliveryDate) => new()
    {
        Id = s.ExecutionStageId,
        SubProjectId = s.SubProjectId,
        Name = s.Name,
        Deadline = s.Deadline,
        IsFinalDelivery = s.IsFinalDelivery,
        // مرحلة التسليم النهائي هي المرجع نفسه، فلا تُقارن بذاتها
        ExceedsContractualDeadline = !s.IsFinalDelivery
            && s.Deadline != null
            && contractualDeliveryDate != null
            && s.Deadline > contractualDeliveryDate,
        SelfFundingSpent = s.SelfFundingSpent,
        BankFundingSpent = s.BankFundingSpent,
        HasSelfFundingProof = s.SelfFundingProofFile != null,
        HasBankFundingProof = s.BankFundingProofFile != null,
        SelfFundingProofFileName = s.SelfFundingProofFile?.FileName,
        BankFundingProofFileName = s.BankFundingProofFile?.FileName,
        PhysicalProgressPercent = s.PhysicalProgressPercent,
        HasPhysicalProgressProof = s.PhysicalProgressProofFile != null,
        PhysicalProgressProofFileName = s.PhysicalProgressProofFile?.FileName,
        Notes = s.Notes,
        PenaltyAmount = s.PenaltyAmount,
        PenaltyPaid = s.PenaltyPaid,
        IsCompleted = s.IsCompleted,
        CreatedAt = s.CreatedAt,
        CompletedAt = s.CompletedAt,
    };
```

Update the four callers. In `GetBySubProjectAsync`, replace the body's final line:

```csharp
        var contractualDeliveryDate = await GetContractualDeliveryDateAsync(subProjectId, cancellationToken);
        return stages.Select(s => ToDto(s, contractualDeliveryDate)).ToList();
```

In `CreateAsync`, `MarkCompleteAsync`, and `SetPenaltyAsync`, replace each `return ToDto(stage);` with:

```csharp
        return ToDto(stage, await GetContractualDeliveryDateAsync(subProjectId, cancellationToken));
```

- [ ] **Step 6: Build**

Run: `cd Backend && dotnet build src/SmartInvest.API/SmartInvest.API.csproj`
Expected: `Build succeeded.` `0 Error(s)`, only the 3 known `CS8620` warnings.

- [ ] **Step 7: Commit**

```bash
git add Backend/src
git commit -m "feat: SyncFinalDeliveryStageAsync owns the auto final stage"
```

---

### Task 3: Handover with proof + award completion rule

**Files:**
- Modify: `Backend/src/SmartInvest.Application/Interfaces/IProcurementService.cs`
- Modify: `Backend/src/SmartInvest.Application/DTOs/ProcurementDtos.cs:70-127`
- Modify: `Backend/src/SmartInvest.Infrastructure/Services/ProcurementService.cs`
- Modify: `Backend/src/SmartInvest.API/Controllers/ProcurementController.cs:122-128`

**Interfaces:**
- Consumes: `IExecutionStageService.SyncFinalDeliveryStageAsync` (Task 2), `ContractAward.SiteHandoverProofFile` (Task 1)
- Produces:
  - `Task SetSiteHandoverAsync(int subProjectId, DateTime handoverDate, FileUploadDto proofFile, CancellationToken ct = default)`
  - `Task<FileDownloadDto> DownloadSiteHandoverProofAsync(int subProjectId, CancellationToken ct = default)`
  - `PUT api/subprojects/{id}/procurement/contract-award/site-handover` is now `multipart/form-data`: field `handoverDate`, file field `proof`
  - `GET api/subprojects/{id}/procurement/contract-award/site-handover/proof`
  - `ContractAwardDetailsDto.SiteHandoverProofFileName` (`string?`)

- [ ] **Step 1: Update the service interface**

In `IProcurementService.cs`, replace the existing `SetSiteHandoverAsync` declaration with:

```csharp
    Task SetSiteHandoverAsync(int subProjectId, DateTime handoverDate, FileUploadDto proofFile, CancellationToken cancellationToken = default);

    Task<FileDownloadDto> DownloadSiteHandoverProofAsync(int subProjectId, CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Update the DTOs**

In `ProcurementDtos.cs`, inside `ContractAwardDetailsDto` after `SiteHandoverDate`:

```csharp
    /// <summary>اسم ملف إثبات تسليم الأرضية — null يعني لم يُرفع بعد.</summary>
    public string? SiteHandoverProofFileName { get; set; }
```

Delete the now-unused `SetSiteHandoverDto` class (lines 124-127) — the endpoint becomes multipart.

- [ ] **Step 3: Inject `IExecutionStageService` into `ProcurementService`**

Replace the field block and constructor (lines 20-27):

```csharp
    private readonly AppDbContext _context;
    private readonly IExecutionStageService _executionStageService;
    private readonly Dictionary<ProcurementStage, IStageOps> _stages;

    public ProcurementService(AppDbContext context, IExecutionStageService executionStageService)
    {
        _context = context;
        _executionStageService = executionStageService;
        _stages = BuildStages(context);
    }
```

No DI registration change is needed — both are already registered in `DependencyInjection.cs`, and `ExecutionStageService` does not depend on `IProcurementService`, so there is no cycle.

- [ ] **Step 4: Replace the site-handover write path**

Replace the whole `SetSiteHandoverAsync` method (lines 235-250) with:

```csharp
    public async Task SetSiteHandoverAsync(int subProjectId, DateTime handoverDate, FileUploadDto proofFile, CancellationToken cancellationToken = default)
    {
        await EnsureSubProjectExistsAsync(subProjectId, cancellationToken);

        var doc = await _context.ContractAwards
            .FirstOrDefaultAsync(x => x.SubProjectId == subProjectId, cancellationToken)
            ?? throw new NotFoundException("لم تبدأ مرحلة الترسية بعد");

        if (doc.SiteHandoverMode == null)
        {
            throw new BusinessRuleException("يجب تحديد حالة أرضية المشروع في بيانات الترسية أولاً");
        }

        // «لم تُسلَّم بعد» تعني أن التسليم يحدث بعد الترسية — تسجيله قبل اكتمالها بلا معنى.
        // أما «مُسلَّمة وقت الترسية» فتُسجَّل أثناء المرحلة السادسة نفسها قبل إكمالها.
        if (doc.SiteHandoverMode == SiteHandoverMode.Pending && !doc.IsCompleted)
        {
            throw new BusinessRuleException("لا يمكن تسجيل تسليم الأرضية قبل إكمال الترسية");
        }

        if (proofFile == null || proofFile.Content.Length == 0)
        {
            throw new BusinessRuleException("إثبات تسليم الأرضية مطلوب");
        }

        doc.SiteHandoverDate = handoverDate;
        doc.SiteHandoverProofFile = new StoredFile
        {
            FileName = proofFile.FileName,
            FileExtension = proofFile.FileExtension,
            FileSize = proofFile.FileSize,
            Content = proofFile.Content,
        };

        await _context.SaveChangesAsync(cancellationToken);

        // الموعد النهائي يُحسب من تاريخ التسليم، فأي تغيير هنا يجب أن ينعكس على مرحلة التسليم النهائي
        await _executionStageService.SyncFinalDeliveryStageAsync(subProjectId, cancellationToken);
    }

    public async Task<FileDownloadDto> DownloadSiteHandoverProofAsync(int subProjectId, CancellationToken cancellationToken = default)
    {
        var doc = await _context.ContractAwards.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubProjectId == subProjectId, cancellationToken)
            ?? throw new NotFoundException("لم تبدأ مرحلة الترسية بعد");

        var file = doc.SiteHandoverProofFile
            ?? throw new NotFoundException("لم يُرفع إثبات تسليم الأرضية بعد");

        return new FileDownloadDto
        {
            FileName = file.FileName,
            FileExtension = file.FileExtension,
            Content = file.Content,
        };
    }
```

- [ ] **Step 5: Replace the completion hook and delete the auto-stamp**

The old `StampSiteHandoverOnAwardAsync` silently stamped `SiteHandoverDate = UtcNow` for `AtAward` projects. The date is now entered explicitly with its proof, so that auto-stamp would overwrite real data with a guess. Delete the method entirely (lines ~360-371) and replace its call in `SetCompletionAsync` (line 189):

```csharp
            if (stage == ProcurementStage.ContractAward)
            {
                await _executionStageService.SyncFinalDeliveryStageAsync(subProjectId, cancellationToken);
            }
```

- [ ] **Step 6: Require date + proof before completing an `AtAward` award**

In `ValidateContractAwardForCompletionAsync`, immediately after the existing `doc.SiteHandoverMode == null` check:

```csharp
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
```

- [ ] **Step 7: Populate the proof filename in the details DTO**

In `GetContractAwardDetailsAsync`, add `x.SiteHandoverProofFile` to the anonymous projection's select list, then next to the existing `details.SiteHandoverDate = doc.SiteHandoverDate;` assignment add:

```csharp
        details.SiteHandoverProofFileName = doc.SiteHandoverProofFile?.FileName;
```

- [ ] **Step 8: Convert the endpoint to multipart and add the download**

In `ProcurementController.cs`, replace the `SetSiteHandover` action (lines 121-128):

```csharp
    /// <summary>تسجيل تسليم أرضية المشروع للمقاول — multipart/form-data: handoverDate + ملف proof.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/contract-award/site-handover")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> SetSiteHandover(
        int subProjectId,
        [FromForm] DateTime handoverDate,
        CancellationToken cancellationToken)
    {
        var file = Request.Form.Files.FirstOrDefault(f => f.Name == "proof" && f.Length > 0)
            ?? throw new BusinessRuleException("إثبات تسليم الأرضية مطلوب");

        var proof = await FileRequestHelpers.ToUploadDtoAsync(file, cancellationToken);
        await _procurementService.SetSiteHandoverAsync(subProjectId, handoverDate, proof, cancellationToken);
        return NoContent();
    }

    /// <summary>تنزيل إثبات تسليم الأرضية.</summary>
    [HttpGet("api/subprojects/{subProjectId:int}/procurement/contract-award/site-handover/proof")]
    public async Task<IActionResult> DownloadSiteHandoverProof(int subProjectId, CancellationToken cancellationToken)
    {
        var file = await _procurementService.DownloadSiteHandoverProofAsync(subProjectId, cancellationToken);
        return File(file.Content, FileRequestHelpers.GetContentType(file.FileExtension), file.FileName);
    }
```

- [ ] **Step 9: Build**

Run: `cd Backend && dotnet build src/SmartInvest.API/SmartInvest.API.csproj`
Expected: `Build succeeded.` `0 Error(s)`.

- [ ] **Step 10: Live-verify the handover round-trip**

Start the backend manually and background it:

```bash
cd Backend/src/SmartInvest.API && (dotnet run --launch-profile https > /tmp/be.log 2>&1 &)
```

Wait for `Now listening on: https://localhost:7250` in `/tmp/be.log`, then:

```bash
cd /tmp && printf '{"usernameOrEmail":"admin@gmail.com","password":"Admin@123"}' > login.json
TOKEN=$(curl -sk -X POST https://localhost:7250/api/auth/login -H "Content-Type: application/json" --data-binary @login.json | node -e "let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>console.log(JSON.parse(d).token))")
printf 'handover proof test' > proof.txt
curl -sk -X PUT "https://localhost:7250/api/subprojects/1/procurement/contract-award/site-handover" -H "Authorization: Bearer $TOKEN" -F "handoverDate=2026-08-01T00:00:00Z" -F "proof=@proof.txt" -w "\nstatus=%{http_code}\n"
```

Expected: `status=204`. Then confirm the final stage's deadline moved to `2027-02-01` — sub-project 1's award is **6 months + 0 days**, so 2026-08-01 + 6 months:

```bash
sqlcmd -S . -E -C -d SmartInvestDB -W -Q "SET NOCOUNT ON; SELECT SubProjectId, Deadline FROM ExecutionStages WHERE IsFinalDelivery = 1;"
```

- [ ] **Step 11: Verify idempotency**

Run the same `curl` again. Expected `status=204`, and:

```bash
sqlcmd -S . -E -C -d SmartInvestDB -W -Q "SET NOCOUNT ON; SELECT COUNT(*) AS FinalStages FROM ExecutionStages WHERE IsFinalDelivery = 1;"
```

Expected: still exactly `1`. Stop the backend when done.

- [ ] **Step 12: Commit**

```bash
git add Backend/src
git commit -m "feat: handover records date + proof, gates award completion, syncs final stage"
```

---

### Task 4: Gate متابعة المشروعات on a completed award

**Files:**
- Modify: `Backend/src/SmartInvest.Infrastructure/Services/ExecutionStageService.cs:186-196`

**Interfaces:**
- Consumes: nothing new
- Produces: `GET api/follow-up` now returns only sub-projects that are approved **and** have `ContractAward.IsCompleted == true`

- [ ] **Step 1: Replace the approved-only filter**

In `GetFollowUpListAsync`, replace the block that currently reads `var approved = subProjects.Where(s => s.IsApproved).ToList();` … through the `subProjectIds` assignment with:

```csharp
        // متابعة المشروعات للمشروعات المُسندة فعلًا لمقاول — أي التي اكتملت ترسيتها.
        // إكمال الترسية يستلزم بالفعل إكمال المراحل الخمس السابقة، فهذا يكافئ 6/6.
        var approvedIds = subProjects.Where(s => s.IsApproved).Select(s => s.SubProjectId).ToList();
        if (approvedIds.Count == 0)
        {
            return [];
        }

        var awardedIds = (await _context.ContractAwards.AsNoTracking()
                .Where(a => a.IsCompleted && approvedIds.Contains(a.SubProjectId))
                .Select(a => a.SubProjectId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var approved = subProjects.Where(s => s.IsApproved && awardedIds.Contains(s.SubProjectId)).ToList();
        if (approved.Count == 0)
        {
            return [];
        }

        var subProjectIds = approved.Select(s => s.SubProjectId).ToList();
```

- [ ] **Step 2: Build**

Run: `cd Backend && dotnet build src/SmartInvest.API/SmartInvest.API.csproj`
Expected: `Build succeeded.` `0 Error(s)`.

- [ ] **Step 3: Live-verify the gating**

Start the backend, get a token as in Task 3 Step 10, then:

```bash
curl -sk "https://localhost:7250/api/follow-up?financialYearId=1" -H "Authorization: Bearer $TOKEN" -o /tmp/fu.json
node -e "const r=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8')); console.log('count', r.length); console.log(r.map(x=>x.subProjectId+':'+x.stageCount).join(', '));" "$(cygpath -w /tmp/fu.json)"
```

Expected: `count 1` — only sub-project 1 (award complete). Sub-projects 2110/1770 and other approved-but-unawarded projects are gone. `stageCount` for 1 is `6` (5 user stages + the backfilled final one).

- [ ] **Step 4: Commit**

```bash
git add Backend/src
git commit -m "feat: متابعة المشروعات lists only projects with a completed award"
```

---

### Task 5: App-wide toast service

**Files:**
- Create: `Frontend/src/app/core/services/toast.service.ts`
- Create: `Frontend/src/app/shared/toast-host.ts`
- Modify: `Frontend/src/app/layout/main-layout/main-layout.html:81-83`
- Modify: `Frontend/src/app/layout/main-layout/main-layout.ts`

**Interfaces:**
- Consumes: nothing
- Produces: `ToastService` with `success(text: string): void`, `error(text: string): void`, `dismiss(id: number): void`, and a `toasts` signal of `Toast[]`

- [ ] **Step 1: Create the service**

`Frontend/src/app/core/services/toast.service.ts`:

```typescript
import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  text: string;
  kind: 'success' | 'error';
}

/**
 * رسائل تأكيد/خطأ عابرة على مستوى التطبيق. سبب وجودها: كل عمليات الحفظ الناجحة كانت
 * صامتة تمامًا، فبدت للمستخدم وكأن الصفحة "تُحدَّث فقط" دون أن يحدث شيء.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;

  readonly toasts = signal<Toast[]>([]);

  success(text: string): void {
    this.push(text, 'success');
  }

  error(text: string): void {
    this.push(text, 'error');
  }

  dismiss(id: number): void {
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private push(text: string, kind: Toast['kind']): void {
    const id = this.nextId++;
    this.toasts.update((list) => [...list, { id, text, kind }]);
    // الأخطاء تبقى أطول — المستخدم يحتاج وقتًا لقراءتها
    setTimeout(() => this.dismiss(id), kind === 'error' ? 6000 : 3500);
  }
}
```

- [ ] **Step 2: Create the host component**

`Frontend/src/app/shared/toast-host.ts`:

```typescript
import { Component, inject } from '@angular/core';
import { ToastService } from '../core/services/toast.service';

@Component({
  selector: 'app-toast-host',
  template: `
    <div class="toast-stack">
      @for (t of toast.toasts(); track t.id) {
        <div class="toast" [class.err]="t.kind === 'error'" (click)="toast.dismiss(t.id)">
          <span class="ico">{{ t.kind === 'error' ? '⚠' : '✓' }}</span>
          <span class="txt">{{ t.text }}</span>
        </div>
      }
    </div>
  `,
  styles: [`
    .toast-stack {
      position: fixed;
      bottom: 24px;
      inset-inline-start: 24px;
      z-index: 9999;
      display: flex;
      flex-direction: column;
      gap: 10px;
      pointer-events: none;
    }
    .toast {
      pointer-events: auto;
      display: flex;
      align-items: center;
      gap: 10px;
      min-width: 260px;
      max-width: 420px;
      padding: 13px 16px;
      border-radius: 10px;
      font-size: 13.5px;
      font-weight: 700;
      color: #fff;
      background: linear-gradient(155deg, #2f7d4f, #1f5c39);
      box-shadow: 0 8px 24px rgba(0, 0, 0, .22);
      cursor: pointer;
      animation: toast-in .22s ease-out;
    }
    .toast.err { background: linear-gradient(155deg, #c0392b, #96271c); }
    .toast .ico { font-size: 15px; flex-shrink: 0; }
    .toast .txt { line-height: 1.5; }
    @keyframes toast-in {
      from { opacity: 0; transform: translateY(10px); }
      to { opacity: 1; transform: translateY(0); }
    }
  `],
})
export class ToastHost {
  protected readonly toast = inject(ToastService);
}
```

- [ ] **Step 3: Mount it in the layout**

In `main-layout.ts`, add `ToastHost` to the component's `imports` array and add the import statement:

```typescript
import { ToastHost } from '../../shared/toast-host';
```

In `main-layout.html`, replace lines 81-83:

```html
  <!-- المحتوى -->
  <div class="content">
    <router-outlet />
  </div>
  <app-toast-host />
```

- [ ] **Step 4: Build**

Run: `cd Frontend && npx ng build`
Expected: `Application bundle generation complete.` with no errors.

- [ ] **Step 5: Commit**

```bash
git add Frontend/src
git commit -m "feat: app-wide toast service + host"
```

---

### Task 6: Step 6 — handover entry, proof upload, and real feedback

**Files:**
- Modify: `Frontend/src/app/core/models/financial.models.ts:22-62`
- Modify: `Frontend/src/app/core/services/financial.service.ts`
- Modify: `Frontend/src/app/features/financial/procurement-workflow.ts`
- Modify: `Frontend/src/app/features/financial/procurement-workflow.html:209-260`

**Interfaces:**
- Consumes: `ToastService` (Task 5); the multipart site-handover endpoint and `siteHandoverProofFileName` (Task 3)
- Produces: nothing consumed by later tasks

- [ ] **Step 1: Add the model field**

In `financial.models.ts`, inside `ContractAwardDetails` after `siteHandoverDate`:

```typescript
  siteHandoverProofFileName: string | null;
```

- [ ] **Step 2: Add the service calls**

In `financial.service.ts`, after `setContractAwardDetails`:

```typescript
  /** تسجيل تسليم الأرضية — multipart: التاريخ + ملف الإثبات */
  setSiteHandover(subProjectId: number, handoverDate: string, proof: File): Observable<void> {
    const form = new FormData();
    form.append('handoverDate', handoverDate);
    form.append('proof', proof, proof.name);
    return this.http.put<void>(
      `${this.base}/subprojects/${subProjectId}/procurement/contract-award/site-handover`,
      form,
    );
  }

  /** تنزيل إثبات تسليم الأرضية كـ Blob (رابط مباشر يفشل بـ 401 لأنه لا يمر على auth.interceptor) */
  downloadSiteHandoverProof(subProjectId: number): Observable<Blob> {
    return this.http.get(
      `${this.base}/subprojects/${subProjectId}/procurement/contract-award/site-handover/proof`,
      { responseType: 'blob' },
    );
  }
```

- [ ] **Step 3: Wire the toast service and handover state into the component**

In `procurement-workflow.ts`, add the import and injection:

```typescript
import { ToastService } from '../../core/services/toast.service';
```

```typescript
  private readonly toast = inject(ToastService);
```

Add these signals next to the other `a*` award signals:

```typescript
  protected readonly aHandoverDate = signal<string>('');
  protected readonly aHandoverSaving = signal(false);
  protected aHandoverFile: File | null = null;
```

- [ ] **Step 4: Add the handover save handler**

Add after `saveAward`:

```typescript
  protected onHandoverFileChange(event: Event): void {
    this.aHandoverFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected saveHandover(): void {
    if (this.aHandoverSaving()) {
      return;
    }
    if (!this.aHandoverDate()) {
      this.toast.error('برجاء تحديد تاريخ تسليم الأرضية');
      return;
    }
    if (!this.aHandoverFile) {
      this.toast.error('برجاء رفع إثبات تسليم الأرضية');
      return;
    }

    this.aHandoverSaving.set(true);
    this.financial.setSiteHandover(this.subProjectId, this.aHandoverDate(), this.aHandoverFile).subscribe({
      next: () => {
        this.aHandoverSaving.set(false);
        this.aHandoverFile = null;
        this.toast.success('تم تسجيل تسليم الأرضية');
        this.reload();
      },
      error: (err) => {
        this.aHandoverSaving.set(false);
        this.toast.error(err?.error?.message ?? 'تعذر تسجيل تسليم الأرضية');
      },
    });
  }

  protected downloadHandoverProof(name: string): void {
    this.financial
      .downloadSiteHandoverProof(this.subProjectId)
      .subscribe((blob) => this.financial.saveBlob(blob, name));
  }
```

- [ ] **Step 5: Replace silent success and `alert()` with toasts**

In `saveAward`, replace the `next` handler:

```typescript
        next: () => {
          this.awardSaving.set(false);
          this.toast.success('تم حفظ بيانات الترسية');
          this.reload();
        },
```

In `complete`, replace both handlers:

```typescript
      next: () => {
        this.busy.set(false);
        this.toast.success('تم إكمال المرحلة');
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        this.toast.error(err?.error?.message ?? 'تعذر إكمال المرحلة');
      },
```

In `reopen`, likewise:

```typescript
      next: () => {
        this.busy.set(false);
        this.toast.success('تم إعادة فتح المرحلة');
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        this.toast.error(err?.error?.message ?? 'تعذر إعادة فتح المرحلة');
      },
```

Then find every remaining `alert(` in this file and convert each to `this.toast.error(...)` with the same message. Verify none remain:

```bash
grep -n "alert(" Frontend/src/app/features/financial/procurement-workflow.ts
```

Expected: no output.

- [ ] **Step 6: Add the handover UI to the award form**

In `procurement-workflow.html`, replace the two `@if` blocks at lines 228-239 with:

```html
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
                        </div>
                      }
```

- [ ] **Step 7: Build**

Run: `cd Frontend && npx ng build`
Expected: `Application bundle generation complete.` with no errors.

- [ ] **Step 8: Live-verify against the browser**

Start both servers manually (frontend **must** be port 4200 — CORS in `appsettings.json` allows only `http://localhost:4200`):

```bash
cd Backend/src/SmartInvest.API && (dotnet run --launch-profile https > /tmp/be.log 2>&1 &)
cd Frontend && (npx ng serve --port 4200 > /tmp/fe.log 2>&1 &)
```

Log in as `admin@gmail.com` / `Admin@123`, open `/app/financial/1770` (FY 2053/2054), open step 6, press **حفظ بيانات الترسية**.

Expected: a green toast reading `تم حفظ بيانات الترسية` appears bottom-start. This is the originally reported bug — confirm it visually with a screenshot.

Then set أرضية المشروع to `مُسلَّمة للمقاول`, save, and confirm the handover date + file fields appear.

- [ ] **Step 9: Commit**

```bash
git add Frontend/src
git commit -m "feat: step 6 records land handover with proof, and confirms every save"
```

---

### Task 7: متابعة المشروعات — locked final row, handover action, deadline warning

**Files:**
- Modify: `Frontend/src/app/core/models/follow-up.models.ts:14-34`
- Modify: `Frontend/src/app/features/follow-up/follow-up-list.ts`
- Modify: `Frontend/src/app/features/follow-up/follow-up-list.html:114-148`
- Modify: `Frontend/src/app/features/follow-up/follow-up-list.css`

**Interfaces:**
- Consumes: `ExecutionStageDto.IsFinalDelivery` / nullable `Deadline` / `ExceedsContractualDeadline` (Task 2); `FinancialService.setSiteHandover` (Task 6); `ToastService` (Task 5)
- Produces: nothing consumed by later tasks

- [ ] **Step 1: Update the model**

In `follow-up.models.ts`, inside `ExecutionStage` replace `deadline: string;` with:

```typescript
  /** null فقط لمرحلة التسليم النهائي قبل تسليم الأرضية */
  deadline: string | null;
  isFinalDelivery: boolean;
  exceedsContractualDeadline: boolean;
```

- [ ] **Step 2: Add handover state and the save handler**

In `follow-up-list.ts`, add the imports and injections:

```typescript
import { FinancialService } from '../../core/services/financial.service';
import { ToastService } from '../../core/services/toast.service';
```

```typescript
  private readonly financial = inject(FinancialService);
  private readonly toast = inject(ToastService);
```

Add these members next to the other stage signals:

```typescript
  protected readonly showHandover = signal(false);
  protected readonly handoverDate = signal('');
  protected readonly handoverSaving = signal(false);
  protected handoverFile: File | null = null;

  /** مرحلة التسليم النهائي بلا موعد = الأرضية لم تُسلَّم بعد */
  protected readonly awaitingHandover = computed(() =>
    this.stages().some((s) => s.isFinalDelivery && s.deadline == null),
  );

  protected openHandover(): void {
    this.handoverDate.set('');
    this.handoverFile = null;
    this.showHandover.set(true);
  }

  protected closeHandover(): void {
    this.showHandover.set(false);
  }

  protected onHandoverFileChange(event: Event): void {
    this.handoverFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected saveHandover(): void {
    const item = this.selectedItem();
    if (!item || this.handoverSaving()) {
      return;
    }
    if (!this.handoverDate()) {
      this.toast.error('برجاء تحديد تاريخ تسليم الأرضية');
      return;
    }
    if (!this.handoverFile) {
      this.toast.error('برجاء رفع إثبات تسليم الأرضية');
      return;
    }

    this.handoverSaving.set(true);
    this.financial.setSiteHandover(item.subProjectId, this.handoverDate(), this.handoverFile).subscribe({
      next: () => {
        this.handoverSaving.set(false);
        this.showHandover.set(false);
        this.toast.success('تم تسجيل تسليم الأرضية — تم احتساب الموعد النهائي');
        this.loadStages(item.subProjectId);
        this.load();
      },
      error: (err) => {
        this.handoverSaving.set(false);
        this.toast.error(err?.error?.message ?? 'تعذر تسجيل تسليم الأرضية');
      },
    });
  }
```

`loadStages` is currently `private` — change it to `protected` so the handler above compiles unchanged.

- [ ] **Step 3: Render the final row distinctly**

In `follow-up-list.html`, replace the opening of the stage row (line 115) and its name/deadline cells:

```html
                    <tr [class.final-row]="s.isFinalDelivery">
                      <td>
                        {{ s.name }}
                        @if (s.isFinalDelivery) { <span class="chip lock-chip">تلقائية</span> }
                      </td>
                      <td class="tnum" [class.warn]="s.exceedsContractualDeadline">
                        @if (s.deadline) {
                          {{ s.deadline | date: 'yyyy/MM/dd' }}
                          @if (s.exceedsContractualDeadline) {
                            <span class="chip warn-chip" title="يتجاوز تاريخ التسليم التعاقدي">تجاوز</span>
                          }
                        } @else {
                          <span class="wait">بانتظار تسليم الأرضية</span>
                        }
                      </td>
```

- [ ] **Step 4: Add the handover button and modal**

In the stage-modal header, after the `+ مرحلة جديدة` button:

```html
          @if (awaitingHandover()) {
            <button class="si-btn sm" (click)="openHandover()">تسجيل تسليم الأرضية</button>
          }
```

Then add this modal after the add-stage modal's closing `}`:

```html
  @if (showHandover()) {
    <div class="si-overlay" (click)="closeHandover()">
      <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(480px,100%)">
        <div class="si-modal-head">
          <div class="grow"><h3>تسجيل تسليم الأرضية</h3><p>تبدأ مدة التنفيذ من هذا التاريخ</p></div>
          <button class="si-x" (click)="closeHandover()">×</button>
        </div>
        <div class="si-modal-body">
          <div class="si-grid">
            <div class="si-fld">
              <label>تاريخ التسليم <span class="req">*</span></label>
              <input type="date" [ngModel]="handoverDate()" (ngModelChange)="handoverDate.set($event)" />
            </div>
            <div class="si-fld">
              <label>إثبات التسليم <span class="req">*</span></label>
              <input type="file" accept=".pdf,.png,.jpg,.jpeg" (change)="onHandoverFileChange($event)" />
            </div>
          </div>
        </div>
        <div class="si-modal-foot">
          <button class="si-btn primary" [disabled]="handoverSaving()" (click)="saveHandover()">
            @if (handoverSaving()) { جاري الحفظ… } @else { حفظ }
          </button>
          <button class="si-btn" (click)="closeHandover()">إلغاء</button>
        </div>
      </div>
    </div>
  }
```

- [ ] **Step 5: Style the final row**

Append to `follow-up-list.css`:

```css
/* مرحلة التسليم النهائي المُدارة تلقائيًا — مميزة ومقفولة */
.final-row { background: var(--surface-2); }
.final-row td { font-weight: 700; }
```

`.chip.lock-chip`, `.warn-chip`, `.wait`, and `.tnum.warn` already exist in this stylesheet — do not redefine them. Remember that Angular per-component styles do **not** cross component boundaries, so never rely on a class defined only in `financial.css`.

- [ ] **Step 6: Confirm the final row keeps its two allowed actions**

Only the name and deadline cells were touched. The الحالة and غرامة cells must be left exactly as they are, so on the final row:

- **إنهاء** still works — marking the final stage complete is how a project is recorded as delivered.
- The **✏️ تعديل الغرامة** control still shows for managers — a late-delivery fine belongs on precisely this row.

Verify in the rendered table that both appear on the `التسليم النهائي` row. If either is missing, the row-level markup was over-edited; restore those two cells.

- [ ] **Step 7: Build**

Run: `cd Frontend && npx ng build`
Expected: `Application bundle generation complete.` with no errors.

- [ ] **Step 8: Commit**

```bash
git add Frontend/src
git commit -m "feat: follow-up shows locked final delivery stage + handover recording"
```

---

### Task 8: Final end-to-end verification

**Files:** none modified — verification only.

**Interfaces:**
- Consumes: everything from Tasks 1-7
- Produces: nothing

- [ ] **Step 1: Clean builds from scratch**

```bash
cd Backend && dotnet build src/SmartInvest.API/SmartInvest.API.csproj 2>&1 | tail -5
cd Frontend && npx ng build 2>&1 | tail -5
```

Expected: `0 Error(s)` and `Application bundle generation complete.`

- [ ] **Step 2: Start both servers**

```bash
cd Backend/src/SmartInvest.API && (dotnet run --launch-profile https > /tmp/be.log 2>&1 &)
cd Frontend && (npx ng serve --port 4200 > /tmp/fe.log 2>&1 &)
```

Confirm `Now listening on: https://localhost:7250` and `Local: http://localhost:4200`.

- [ ] **Step 3: Verify the `Pending` path end-to-end**

In the browser as `admin@gmail.com` / `Admin@123`: open الإدارة المالية for a sub-project whose أرضية المشروع is `لم تُسلَّم بعد`, upload أمر الإسناد + العقد, and complete step 6.

Expected: green toast `تم إكمال المرحلة`. Then open متابعة المشروعات — the project now appears, with a `التسليم النهائي` row reading **بانتظار تسليم الأرضية**.

Press **تسجيل تسليم الأرضية**, supply a date + file, save. Expected: toast, and the final row now shows a real date.

- [ ] **Step 4: Verify the `AtAward` completion gate**

On a different project set أرضية المشروع to `مُسلَّمة للمقاول` and try to complete step 6 **without** recording the handover.

Expected: red toast `يجب تسجيل تاريخ تسليم الأرضية قبل إكمال الترسية`. Record date + proof, complete again — succeeds, and the final stage carries a computed date immediately.

- [ ] **Step 5: Verify no duplicate final stages**

```bash
sqlcmd -S . -E -C -d SmartInvestDB -W -Q "SET NOCOUNT ON; SELECT SubProjectId, COUNT(*) AS n FROM ExecutionStages WHERE IsFinalDelivery = 1 GROUP BY SubProjectId HAVING COUNT(*) > 1;"
```

Expected: no rows.

- [ ] **Step 6: Verify reopen preserves the row**

Reopen step 6 as manager, then complete it again. Re-run the query from Step 5 (still no duplicates) and confirm in متابعة المشروعات that the final row and any spend recorded on it survived.

- [ ] **Step 7: Stop the servers and commit any fixes**

```bash
for pid in $(netstat -ano | grep -E ':(4200|7250)' | grep LISTENING | awk '{print $5}' | sort -u); do taskkill //F //PID $pid; done
```

If Steps 3-6 required fixes, commit them:

```bash
git add -A && git commit -m "fix: issues found in end-to-end verification"
```

---

## Out of scope

The Excel-import wizard's per-row "show and edit all project data" button is **not** part of this plan. It needs its own spec/plan cycle: the preview currently returns only aggregate counts and unresolved-lookup lists, so it requires new per-row preview DTOs plus commit-time row overrides applied to the cached `ParsedImportFile`.

The other 19 `alert()` calls across 9 files (`agencies`, `contractors`, `presentation-memos`, `measurements`, `plan-list`, `sub-project-details`, `projects`, `settings-lookup-page`, `users`) keep their current behavior. `ToastService` is app-wide, so they can migrate as those pages are next worked on.
