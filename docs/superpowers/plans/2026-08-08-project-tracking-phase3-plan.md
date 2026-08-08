# Phase 3: متابعة المشروعات (Project Execution Tracking) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the core متابعة المشروعات module — a new sidebar page listing every approved sub-project's financial/physical execution progress (financial-year filtered), a per-project stage timeline (create/complete stages, record ذاتي/بنكي spend + proof files, physical progress % + proof, notes, penalties, overrun validation), and contractor-profile fields (notes, fines rollup, "work again?" flag) surfaced during contractor assignment.

**Architecture:** New `ExecutionStage` entity (1:many from `SubProject`), separate from the existing 6-stage pre-award procurement model. A single `ExecutionStageService` owns both the per-project stage CRUD/business-rules and the aggregated متابعة المشروعات list query (mirrors how `IProcurementService` already owns both its list and per-stage endpoints). Frontend gets one new list page (mirrors `financial-list.ts`) with an in-page modal for the stage timeline (mirrors the `si-modal` pattern already used throughout `sub-project-details.ts`).

**Tech Stack:** ASP.NET Core 10 / EF Core (SQL Server) backend, Angular 21 standalone components + Signals frontend. No automated test suite — verify via `dotnet build` + `ng build` + live walkthrough, per established project convention.

## Global Constraints

- No automated test framework in this repo — every task's verification step is `dotnet build` / `ng build` + a live check via curl or the browser, not a unit test file.
- New backend code follows the exact patterns already in the codebase: `IGenericRepository<T>` + `IUnitOfWork` for simple CRUD, `AppDbContext` direct queries only where a service already does this for a cross-cutting read (matches `ProcurementService`).
- File uploads reuse the existing `StoredFile` owned-type + `StoredFileConfigurationExtensions.OwnsStoredFile` + `FileUploadDto`/`FileDownloadDto` (`Backend/src/SmartInvest.Application/DTOs/ProcurementDtos.cs`) + `FileRequestHelpers` (`Backend/src/SmartInvest.API/Common/FileRequestHelpers.cs`) — do not invent a new file-storage mechanism.
- Money in the DB is always full EGP (`decimal(18,2)`), never thousands — matches the fix already applied to Excel import.
- Arabic strings, RTL layout, `.si-modal`/`.si-btn`/`.si-fld`/`.si-grid`/`.tbl-wrap`/`.kpis`/`.tnum` CSS classes already defined globally — reuse them, do not invent new component-scoped equivalents.
- `Roles.PlanningStaff` = `"PlanningEmployee,PlanningManager"` (read/write day-to-day), `Roles.PlanningManager` alone gates approval-type actions — matches every other controller in this codebase.
- **Explicitly out of scope for this plan** (per the spec's own two-part split): the AI RAG contractor report, the AI RAG stalled-project precedent warning, and the agentic portfolio-reports page. Those get their own follow-up plan once this core tracking module is live and has real data to report on.
- **Scoped down from the spec's "same advanced filters as Projects page":** Task 4's `GET api/follow-up` already accepts `mainProgramId`/`subProgramId`/`markazId`/`priorityId` server-side, but Task 7's list page only wires up the financial-year selector + free-text search in the UI (matches `financial-list.ts`'s current scope exactly). Adding the full advanced-filter panel UI (level/agency/funding-range) is a fast-follow once this core page is live — flag this explicitly to the user rather than silently shipping a narrower filter set than the spec describes.

---

## File Structure

**Backend — new files:**
- `Backend/src/SmartInvest.Domain/Entities/ExecutionStage.cs`
- `Backend/src/SmartInvest.Domain/Entities/ContractorNote.cs`
- `Backend/src/SmartInvest.Infrastructure/Data/Configurations/ExecutionStageConfiguration.cs`
- `Backend/src/SmartInvest.Infrastructure/Data/Configurations/ContractorNoteConfiguration.cs`
- `Backend/src/SmartInvest.Application/DTOs/ExecutionStageDtos.cs`
- `Backend/src/SmartInvest.Application/DTOs/ContractorNoteDtos.cs`
- `Backend/src/SmartInvest.Application/Validators/CreateExecutionStageDtoValidator.cs`
- `Backend/src/SmartInvest.Application/Interfaces/IExecutionStageService.cs`
- `Backend/src/SmartInvest.Infrastructure/Services/ExecutionStageService.cs`
- `Backend/src/SmartInvest.API/Controllers/ExecutionStagesController.cs`

**Backend — modified files:**
- `Backend/src/SmartInvest.Domain/Entities/SubProject.cs` (add `OverrunPercentage`)
- `Backend/src/SmartInvest.Domain/Entities/Contractor.cs` (add `WillWorkAgain`, `Notes` collection)
- `Backend/src/SmartInvest.Infrastructure/Data/AppDbContext.cs` (register 2 new `DbSet`s)
- `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs` (register `IExecutionStageService`)
- `Backend/src/SmartInvest.Application/DTOs/ContractorDtos.cs` (add `WillWorkAgain`, `TotalFines`, `UnpaidFines`, `Notes`)
- `Backend/src/SmartInvest.Application/Interfaces/IContractorService.cs` (add `UpdateWillWorkAgainAsync`, `AddNoteAsync`, `GetNotesAsync`)
- `Backend/src/SmartInvest.Application/Services/ContractorService.cs` (implement the above)
- `Backend/src/SmartInvest.API/Controllers/ContractorsController.cs` (expose the new endpoints)

**Frontend — new files:**
- `Frontend/src/app/core/models/follow-up.models.ts`
- `Frontend/src/app/core/services/follow-up.service.ts`
- `Frontend/src/app/features/follow-up/follow-up-list.ts`
- `Frontend/src/app/features/follow-up/follow-up-list.html`
- `Frontend/src/app/features/follow-up/follow-up-list.css`

**Frontend — modified files:**
- `Frontend/src/app/app.routes.ts` (add `follow-up` route)
- `Frontend/src/app/layout/main-layout/main-layout.ts` (add sidebar nav item)
- `Frontend/src/app/core/models/contractor.models.ts` (add `willWorkAgain`, fines, notes fields)
- `Frontend/src/app/core/services/contractors.service.ts` (add note/flag API calls)
- `Frontend/src/app/features/contractors/contractors.ts` / `.html` (show flag + fines + notes, add-note action)
- `Frontend/src/app/features/financial/procurement-workflow.ts` / `.html` (contractor profile summary panel on assignment)

---

### Task 1: Data model — `ExecutionStage`, `ContractorNote`, `SubProject.OverrunPercentage`, `Contractor.WillWorkAgain`, migration

**Files:**
- Create: `Backend/src/SmartInvest.Domain/Entities/ExecutionStage.cs`
- Create: `Backend/src/SmartInvest.Domain/Entities/ContractorNote.cs`
- Create: `Backend/src/SmartInvest.Infrastructure/Data/Configurations/ExecutionStageConfiguration.cs`
- Create: `Backend/src/SmartInvest.Infrastructure/Data/Configurations/ContractorNoteConfiguration.cs`
- Modify: `Backend/src/SmartInvest.Domain/Entities/SubProject.cs`
- Modify: `Backend/src/SmartInvest.Domain/Entities/Contractor.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Data/AppDbContext.cs`

**Interfaces:**
- Produces: `ExecutionStage` entity with fields `ExecutionStageId, SubProjectId, Name, Deadline, SelfFundingSpent, BankFundingSpent, SelfFundingProofFile, BankFundingProofFile, PhysicalProgressPercent, PhysicalProgressProofFile, Notes, PenaltyAmount, PenaltyPaid, IsCompleted, CreatedAt, CompletedAt` — every later task's service/DTO/UI code uses these exact names.
- Produces: `ContractorNote` entity with fields `ContractorNoteId, ContractorId, SubProjectId (nullable), Text, IsAiGenerated, CreatedAt`.
- Produces: `SubProject.OverrunPercentage` (`decimal?`), `Contractor.WillWorkAgain` (`bool?`), `Contractor.Notes` (`ICollection<ContractorNote>`).

- [ ] **Step 1: Create the `ExecutionStage` entity**

```csharp
// Backend/src/SmartInvest.Domain/Entities/ExecutionStage.cs
using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities;

/// <summary>
/// مرحلة تنفيذ حرة يضيفها مدير التخطيط بعد اكتمال العقد والترسية — منفصلة عمدًا عن
/// Entities/Procurement (تلك مراحل الطرح الثابتة الستة قبل الترسية؛ هذه قائمة مفتوحة
/// بعد الترسية، اسم كل مرحلة يكتبه الموظف بنفسه).
/// </summary>
public class ExecutionStage
{
    public int ExecutionStageId { get; set; }

    public int SubProjectId { get; set; }
    public virtual SubProject SubProject { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }

    public decimal SelfFundingSpent { get; set; }
    public decimal BankFundingSpent { get; set; }
    public StoredFile? SelfFundingProofFile { get; set; }
    public StoredFile? BankFundingProofFile { get; set; }

    public decimal PhysicalProgressPercent { get; set; }
    public StoredFile? PhysicalProgressProofFile { get; set; }

    public string? Notes { get; set; }

    /// <summary>يُملأ يدويًا عند تجاوز الموعد النهائي — لا يُحسب تلقائيًا.</summary>
    public decimal? PenaltyAmount { get; set; }
    public bool PenaltyPaid { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
```

- [ ] **Step 2: Create the `ContractorNote` entity**

```csharp
// Backend/src/SmartInvest.Domain/Entities/ContractorNote.cs
namespace SmartInvest.Domain.Entities;

/// <summary>ملاحظة عامة عن المقاول (SubProjectId فارغ) أو مرتبطة بمشروع بعينه.</summary>
public class ContractorNote
{
    public int ContractorNoteId { get; set; }

    public int ContractorId { get; set; }
    public virtual Contractor Contractor { get; set; } = null!;

    public int? SubProjectId { get; set; }
    public virtual SubProject? SubProject { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>true لو كتبها الذكاء الاصطناعي (تقرير مستقبلي) بدل موظف.</summary>
    public bool IsAiGenerated { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 3: Add `OverrunPercentage` to `SubProject`**

In `Backend/src/SmartInvest.Domain/Entities/SubProject.cs`, add right after the existing `ProjectNature` property:

```csharp
        public string ProjectNature { get; set; } = string.Empty;

        /// <summary>نسبة السماح بتجاوز الميزانية أثناء التنفيذ (مثال: 10 تعني 10%) — على مستوى المشروع الفرعي كله، وليس لكل مرحلة.</summary>
        public decimal? OverrunPercentage { get; set; }
```

- [ ] **Step 4: Add `WillWorkAgain` + `Notes` to `Contractor`**

In `Backend/src/SmartInvest.Domain/Entities/Contractor.cs`:

```csharp
namespace SmartInvest.Domain.Entities
{
    public class Contractor
    {
        [Key]
        public int ContractorId { get; set; }
        public string ContractorName { get; set; } = string.Empty;
        public string CompanyType { get; set; } = string.Empty;
        public string NationalIdOrCommercialRegister { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        /// <summary>هل نتعامل معه تاني؟ null = لم يُقيَّم بعد.</summary>
        public bool? WillWorkAgain { get; set; }

        public virtual ICollection<ProjectAssignment> ProjectAssignments { get; set; }
        public virtual ICollection<ContractorNote> Notes { get; set; } = new List<ContractorNote>();
    }
}
```

- [ ] **Step 5: EF configuration for `ExecutionStage`**

```csharp
// Backend/src/SmartInvest.Infrastructure/Data/Configurations/ExecutionStageConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class ExecutionStageConfiguration : IEntityTypeConfiguration<ExecutionStage>
{
    public void Configure(EntityTypeBuilder<ExecutionStage> builder)
    {
        builder.HasKey(x => x.ExecutionStageId);

        builder.Property(x => x.Name).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.SelfFundingSpent).HasColumnType("decimal(18,2)");
        builder.Property(x => x.BankFundingSpent).HasColumnType("decimal(18,2)");
        builder.Property(x => x.PhysicalProgressPercent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.PenaltyAmount).HasColumnType("decimal(18,2)");

        builder.HasOne(x => x.SubProject)
               .WithMany()
               .HasForeignKey(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsStoredFile(x => x.SelfFundingProofFile, "SelfFundingProof_");
        builder.OwnsStoredFile(x => x.BankFundingProofFile, "BankFundingProof_");
        builder.OwnsStoredFile(x => x.PhysicalProgressProofFile, "PhysicalProgressProof_");
    }
}
```

- [ ] **Step 6: EF configuration for `ContractorNote`**

```csharp
// Backend/src/SmartInvest.Infrastructure/Data/Configurations/ContractorNoteConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class ContractorNoteConfiguration : IEntityTypeConfiguration<ContractorNote>
{
    public void Configure(EntityTypeBuilder<ContractorNote> builder)
    {
        builder.HasKey(x => x.ContractorNoteId);
        builder.Property(x => x.Text).HasMaxLength(2000).IsRequired();

        builder.HasOne(x => x.Contractor)
               .WithMany(c => c.Notes)
               .HasForeignKey(x => x.ContractorId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SubProject)
               .WithMany()
               .HasForeignKey(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 7: Register the two new `DbSet`s**

In `Backend/src/SmartInvest.Infrastructure/Data/AppDbContext.cs`, add after the `ContractAwardVersions` line:

```csharp
    public DbSet<ContractAwardVersion> ContractAwardVersions => Set<ContractAwardVersion>();
    // ===== end Financial Management =====

    // ===== Project Execution Tracking (متابعة المشروعات) =====
    public DbSet<ExecutionStage> ExecutionStages => Set<ExecutionStage>();
    public DbSet<ContractorNote> ContractorNotes => Set<ContractorNote>();
    // ===== end Project Execution Tracking =====
```

- [ ] **Step 8: Generate and inspect the migration**

Run from `Backend/`:

```bash
dotnet ef migrations add AddExecutionTracking --project src/SmartInvest.Infrastructure --startup-project src/SmartInvest.API
```

Expected: a new file under `Backend/src/SmartInvest.Infrastructure/Migrations/` creating tables `ExecutionStages` and `ContractorNotes`, plus an `AddColumn` for `SubProjects.OverrunPercentage` and `Contractors.WillWorkAgain`. Open the generated `.cs` migration file and confirm those four changes are present — no unrelated table drops/renames.

- [ ] **Step 9: Build and apply the migration**

```bash
dotnet build src/SmartInvest.API/SmartInvest.API.csproj
dotnet ef database update --project src/SmartInvest.Infrastructure --startup-project src/SmartInvest.API
```

Expected: `Build succeeded`, then `Applying migration '..._AddExecutionTracking'` followed by `Done.`

- [ ] **Step 10: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Entities/ExecutionStage.cs Backend/src/SmartInvest.Domain/Entities/ContractorNote.cs Backend/src/SmartInvest.Domain/Entities/SubProject.cs Backend/src/SmartInvest.Domain/Entities/Contractor.cs Backend/src/SmartInvest.Infrastructure/Data/Configurations/ExecutionStageConfiguration.cs Backend/src/SmartInvest.Infrastructure/Data/Configurations/ContractorNoteConfiguration.cs Backend/src/SmartInvest.Infrastructure/Data/AppDbContext.cs Backend/src/SmartInvest.Infrastructure/Migrations/
git commit -m "feat: Phase 3 data model — ExecutionStage, ContractorNote, overrun %, will-work-again flag"
```

---

### Task 2: DTOs + validator

**Files:**
- Create: `Backend/src/SmartInvest.Application/DTOs/ExecutionStageDtos.cs`
- Create: `Backend/src/SmartInvest.Application/Validators/CreateExecutionStageDtoValidator.cs`

**Interfaces:**
- Consumes: nothing new (uses `FileUploadDto` from `ProcurementDtos.cs`, already in the codebase).
- Produces: `ExecutionStageDto`, `CreateExecutionStageDto`, `SetExecutionStagePenaltyDto`, `FollowUpListItemDto`, `FollowUpFilterDto` — Task 3 (service) and Task 4 (controller) consume these exact names/shapes.

- [ ] **Step 1: Write the DTOs**

```csharp
// Backend/src/SmartInvest.Application/DTOs/ExecutionStageDtos.cs
namespace SmartInvest.Application.DTOs;

public class ExecutionStageDto
{
    public int Id { get; set; }
    public int SubProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }

    public decimal SelfFundingSpent { get; set; }
    public decimal BankFundingSpent { get; set; }
    public bool HasSelfFundingProof { get; set; }
    public bool HasBankFundingProof { get; set; }

    public decimal PhysicalProgressPercent { get; set; }
    public bool HasPhysicalProgressProof { get; set; }

    public string? Notes { get; set; }
    public decimal? PenaltyAmount { get; set; }
    public bool PenaltyPaid { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>يُبنى في الـ Controller من multipart/form-data (حقول + حتى 3 ملفات) — نفس نمط UploadProcurementVersionDto.</summary>
public class CreateExecutionStageDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
    public decimal SelfFundingSpent { get; set; }
    public decimal BankFundingSpent { get; set; }
    public FileUploadDto? SelfFundingProofFile { get; set; }
    public FileUploadDto? BankFundingProofFile { get; set; }
    public decimal PhysicalProgressPercent { get; set; }
    public FileUploadDto? PhysicalProgressProofFile { get; set; }
    public string? Notes { get; set; }
}

public class SetExecutionStagePenaltyDto
{
    public decimal? PenaltyAmount { get; set; }
    public bool PenaltyPaid { get; set; }
}

/// <summary>صف جدول متابعة المشروعات.</summary>
public class FollowUpListItemDto
{
    public int SubProjectId { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
    public string? SubProjectCode { get; set; }
    public string MainProjectName { get; set; } = string.Empty;
    public string? ContractorName { get; set; }
    public bool IsStalled { get; set; }
    public decimal FinancialProgressPercent { get; set; }
    public decimal PhysicalProgressPercent { get; set; }
    public DateTime? NextDeadline { get; set; }
    public int StageCount { get; set; }
}
```

- [ ] **Step 2: Write the validator**

```csharp
// Backend/src/SmartInvest.Application/Validators/CreateExecutionStageDtoValidator.cs
using FluentValidation;
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Validators;

public class CreateExecutionStageDtoValidator : AbstractValidator<CreateExecutionStageDto>
{
    public CreateExecutionStageDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المرحلة مطلوب")
            .MaximumLength(250);

        RuleFor(x => x.Deadline)
            .NotEmpty().WithMessage("الموعد النهائي للمرحلة مطلوب");

        RuleFor(x => x.SelfFundingSpent)
            .GreaterThanOrEqualTo(0).WithMessage("المصروف الذاتي لا يمكن أن يكون سالبًا");

        RuleFor(x => x.BankFundingSpent)
            .GreaterThanOrEqualTo(0).WithMessage("المصروف البنكي لا يمكن أن يكون سالبًا");

        RuleFor(x => x.PhysicalProgressPercent)
            .InclusiveBetween(0, 100).WithMessage("نسبة التنفيذ العيني يجب أن تكون بين 0 و100");

        RuleFor(x => x.SelfFundingProofFile)
            .NotNull().WithMessage("إثبات الصرف الذاتي مطلوب عند تسجيل مبلغ ذاتي")
            .When(x => x.SelfFundingSpent > 0);

        RuleFor(x => x.BankFundingProofFile)
            .NotNull().WithMessage("إثبات الصرف البنكي مطلوب عند تسجيل مبلغ بنكي")
            .When(x => x.BankFundingSpent > 0);

        RuleFor(x => x.PhysicalProgressProofFile)
            .NotNull().WithMessage("إثبات التنفيذ العيني مطلوب عند تسجيل نسبة تنفيذ")
            .When(x => x.PhysicalProgressPercent > 0);
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build src/SmartInvest.API/SmartInvest.API.csproj
```

Expected: `Build succeeded` (FluentValidation auto-discovers validators via `AddValidatorsFromAssembly` — confirm that call already exists by checking `Backend/src/SmartInvest.API/Program.cs` or `Backend/src/SmartInvest.Application/DependencyInjection.cs` for `AddValidatorsFromAssemblyContaining`; every other `Create*DtoValidator` in this codebase is picked up the same way, no manual registration needed).

- [ ] **Step 4: Commit**

```bash
git add Backend/src/SmartInvest.Application/DTOs/ExecutionStageDtos.cs Backend/src/SmartInvest.Application/Validators/CreateExecutionStageDtoValidator.cs
git commit -m "feat: Phase 3 execution-stage DTOs + validator"
```

---

### Task 3: `IExecutionStageService` / `ExecutionStageService` — CRUD + business rules + follow-up list

**Files:**
- Create: `Backend/src/SmartInvest.Application/Interfaces/IExecutionStageService.cs`
- Create: `Backend/src/SmartInvest.Infrastructure/Services/ExecutionStageService.cs`

**Interfaces:**
- Consumes: `ExecutionStage`, `ContractAward` (via `AppDbContext.ContractAwards`), `SubProject` entities; `ExecutionStageDto`, `CreateExecutionStageDto`, `SetExecutionStagePenaltyDto`, `FollowUpListItemDto` from Task 2; `FileDownloadDto` from `ProcurementDtos.cs`.
- Produces: `IExecutionStageService` with methods `GetBySubProjectAsync`, `CreateAsync`, `MarkCompleteAsync`, `SetPenaltyAsync`, `DownloadFileAsync`, `GetFollowUpListAsync` — Task 4 (controller) calls these exact signatures.

- [ ] **Step 1: Write the interface**

```csharp
// Backend/src/SmartInvest.Application/Interfaces/IExecutionStageService.cs
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

/// <summary>
/// مراحل التنفيذ بعد الترسية (متابعة المشروعات) — منفصلة عن IProcurementService (مراحل الطرح قبل الترسية).
/// </summary>
public interface IExecutionStageService
{
    Task<IReadOnlyList<ExecutionStageDto>> GetBySubProjectAsync(int subProjectId, CancellationToken cancellationToken = default);

    Task<ExecutionStageDto> CreateAsync(int subProjectId, CreateExecutionStageDto dto, CancellationToken cancellationToken = default);

    Task<ExecutionStageDto> MarkCompleteAsync(int subProjectId, int stageId, CancellationToken cancellationToken = default);

    Task<ExecutionStageDto> SetPenaltyAsync(int subProjectId, int stageId, SetExecutionStagePenaltyDto dto, CancellationToken cancellationToken = default);

    Task<FileDownloadDto> DownloadFileAsync(int subProjectId, int stageId, string fileKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FollowUpListItemDto>> GetFollowUpListAsync(
        int? financialYearId, int? mainProgramId, int? subProgramId, int? markazId, int? priorityId,
        string? searchTerm, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Write the service**

```csharp
// Backend/src/SmartInvest.Infrastructure/Services/ExecutionStageService.cs
using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Services;

public class ExecutionStageService : IExecutionStageService
{
    private readonly AppDbContext _context;
    private readonly IGenericRepository<ExecutionStage> _stageRepository;
    private readonly ISubProjectRepository _subProjectRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ExecutionStageService(
        AppDbContext context,
        IGenericRepository<ExecutionStage> stageRepository,
        ISubProjectRepository subProjectRepository,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _stageRepository = stageRepository;
        _subProjectRepository = subProjectRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ExecutionStageDto>> GetBySubProjectAsync(int subProjectId, CancellationToken cancellationToken = default)
    {
        var stages = await _context.ExecutionStages.AsNoTracking()
            .Where(s => s.SubProjectId == subProjectId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return stages.Select(ToDto).ToList();
    }

    public async Task<ExecutionStageDto> CreateAsync(int subProjectId, CreateExecutionStageDto dto, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetByIdAsync(subProjectId, cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");

        var award = await _context.ContractAwards.AsNoTracking()
            .FirstOrDefaultAsync(a => a.SubProjectId == subProjectId, cancellationToken);
        if (award is not { IsCompleted: true })
        {
            throw new BusinessRuleException("لا يمكن إضافة مرحلة تنفيذ قبل اكتمال مرحلة العقد والترسية");
        }

        // ترتيب التنفيذ: مقاولات يسمح بصرف الدفعة فور بدء المرحلة، توريدات يشترط تسجيل تنفيذ عيني
        // في نفس المرحلة قبل أي صرف - المقاول يورّد أولًا ثم يُصرف له.
        var hasSpend = dto.SelfFundingSpent > 0 || dto.BankFundingSpent > 0;
        if (subProject.ProjectNature == "توريدات" && hasSpend && dto.PhysicalProgressPercent <= 0)
        {
            throw new BusinessRuleException("في مشروعات التوريدات، يجب تسجيل نسبة تنفيذ عيني قبل تسجيل أي صرف على نفس المرحلة");
        }

        var existingStages = await _context.ExecutionStages.AsNoTracking()
            .Where(s => s.SubProjectId == subProjectId)
            .ToListAsync(cancellationToken);

        var spentSoFar = existingStages.Sum(s => s.SelfFundingSpent + s.BankFundingSpent);
        var newTotalSpent = spentSoFar + dto.SelfFundingSpent + dto.BankFundingSpent;
        var overrunMultiplier = 1 + (subProject.OverrunPercentage ?? 0) / 100m;
        var allowedCeiling = subProject.TotalCost * overrunMultiplier;
        if (newTotalSpent > allowedCeiling)
        {
            throw new BusinessRuleException(
                $"إجمالي المصروف ({newTotalSpent:N2} ج.م) يتجاوز الحد المسموح ({allowedCeiling:N2} ج.م = التكلفة الإجمالية + نسبة التجاوز)");
        }

        var stage = new ExecutionStage
        {
            SubProjectId = subProjectId,
            Name = dto.Name.Trim(),
            Deadline = dto.Deadline,
            SelfFundingSpent = dto.SelfFundingSpent,
            BankFundingSpent = dto.BankFundingSpent,
            PhysicalProgressPercent = dto.PhysicalProgressPercent,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
        };

        if (dto.SelfFundingProofFile != null)
        {
            stage.SelfFundingProofFile = ToStoredFile(dto.SelfFundingProofFile);
        }
        if (dto.BankFundingProofFile != null)
        {
            stage.BankFundingProofFile = ToStoredFile(dto.BankFundingProofFile);
        }
        if (dto.PhysicalProgressProofFile != null)
        {
            stage.PhysicalProgressProofFile = ToStoredFile(dto.PhysicalProgressProofFile);
        }

        await _stageRepository.AddAsync(stage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(stage);
    }

    public async Task<ExecutionStageDto> MarkCompleteAsync(int subProjectId, int stageId, CancellationToken cancellationToken = default)
    {
        var stage = await GetOwnedStageAsync(subProjectId, stageId, cancellationToken);
        stage.IsCompleted = true;
        stage.CompletedAt = DateTime.UtcNow;

        _stageRepository.Update(stage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(stage);
    }

    public async Task<ExecutionStageDto> SetPenaltyAsync(int subProjectId, int stageId, SetExecutionStagePenaltyDto dto, CancellationToken cancellationToken = default)
    {
        var stage = await GetOwnedStageAsync(subProjectId, stageId, cancellationToken);
        stage.PenaltyAmount = dto.PenaltyAmount;
        stage.PenaltyPaid = dto.PenaltyPaid;

        _stageRepository.Update(stage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(stage);
    }

    public async Task<FileDownloadDto> DownloadFileAsync(int subProjectId, int stageId, string fileKey, CancellationToken cancellationToken = default)
    {
        var stage = await GetOwnedStageAsync(subProjectId, stageId, cancellationToken);

        var file = fileKey switch
        {
            "self" => stage.SelfFundingProofFile,
            "bank" => stage.BankFundingProofFile,
            "progress" => stage.PhysicalProgressProofFile,
            _ => throw new BusinessRuleException($"نوع الملف '{fileKey}' غير معروف"),
        };

        if (file == null)
        {
            throw new NotFoundException("الملف المطلوب غير موجود");
        }

        return new FileDownloadDto { FileName = file.FileName, FileExtension = file.FileExtension, Content = file.Content };
    }

    public async Task<IReadOnlyList<FollowUpListItemDto>> GetFollowUpListAsync(
        int? financialYearId, int? mainProgramId, int? subProgramId, int? markazId, int? priorityId,
        string? searchTerm, CancellationToken cancellationToken = default)
    {
        var (subProjects, _) = await _subProjectRepository.SearchAsync(
            mainProjectId: null, mainProgramId, subProgramId, markazId, priorityId,
            statusId: null, financialYearId, searchTerm, page: 1, pageSize: 2000, cancellationToken);

        var approved = subProjects.Where(s => s.IsApproved).ToList();
        if (approved.Count == 0)
        {
            return [];
        }

        var subProjectIds = approved.Select(s => s.SubProjectId).ToList();

        var stagesByProject = (await _context.ExecutionStages.AsNoTracking()
                .Where(s => subProjectIds.Contains(s.SubProjectId))
                .ToListAsync(cancellationToken))
            .GroupBy(s => s.SubProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var contractorNameByProject = (await _context.Set<ProjectAssignment>().AsNoTracking()
                .Where(a => subProjectIds.Contains(a.SubProjectId) && a.ContractorId != null)
                .OrderByDescending(a => a.AssignmentDate)
                .Select(a => new { a.SubProjectId, ContractorName = a.Contractor!.ContractorName })
                .ToListAsync(cancellationToken))
            .GroupBy(a => a.SubProjectId)
            .ToDictionary(g => g.Key, g => g.First().ContractorName);

        return approved.Select(s =>
        {
            stagesByProject.TryGetValue(s.SubProjectId, out var stages);
            stages ??= [];

            var financialPercent = s.TotalCost <= 0
                ? 0
                : Math.Round(stages.Sum(x => x.SelfFundingSpent + x.BankFundingSpent) / s.TotalCost * 100, 2);

            var latestPhysical = stages.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.PhysicalProgressPercent ?? 0;

            var nextDeadline = stages.Where(x => !x.IsCompleted).OrderBy(x => x.Deadline).FirstOrDefault()?.Deadline;

            contractorNameByProject.TryGetValue(s.SubProjectId, out var contractorName);

            return new FollowUpListItemDto
            {
                SubProjectId = s.SubProjectId,
                SubProjectName = s.SubProjectName,
                SubProjectCode = s.SubProjectCode,
                MainProjectName = s.MainProject.MainProjectName,
                ContractorName = contractorName,
                IsStalled = s.Status.StatusName == "متعثر",
                FinancialProgressPercent = financialPercent,
                PhysicalProgressPercent = latestPhysical,
                NextDeadline = nextDeadline,
                StageCount = stages.Count,
            };
        }).ToList();
    }

    private async Task<ExecutionStage> GetOwnedStageAsync(int subProjectId, int stageId, CancellationToken cancellationToken)
    {
        var stage = await _stageRepository.GetByIdAsync(stageId, cancellationToken);
        if (stage == null || stage.SubProjectId != subProjectId)
        {
            throw new NotFoundException($"مرحلة التنفيذ رقم {stageId} غير موجودة لهذا المشروع");
        }

        return stage;
    }

    private static StoredFile ToStoredFile(FileUploadDto dto) => new()
    {
        FileName = dto.FileName,
        FileExtension = dto.FileExtension,
        FileSize = dto.FileSize,
        Content = dto.Content,
    };

    private static ExecutionStageDto ToDto(ExecutionStage s) => new()
    {
        Id = s.ExecutionStageId,
        SubProjectId = s.SubProjectId,
        Name = s.Name,
        Deadline = s.Deadline,
        SelfFundingSpent = s.SelfFundingSpent,
        BankFundingSpent = s.BankFundingSpent,
        HasSelfFundingProof = s.SelfFundingProofFile != null,
        HasBankFundingProof = s.BankFundingProofFile != null,
        PhysicalProgressPercent = s.PhysicalProgressPercent,
        HasPhysicalProgressProof = s.PhysicalProgressProofFile != null,
        Notes = s.Notes,
        PenaltyAmount = s.PenaltyAmount,
        PenaltyPaid = s.PenaltyPaid,
        IsCompleted = s.IsCompleted,
        CreatedAt = s.CreatedAt,
        CompletedAt = s.CompletedAt,
    };
}
```

- [ ] **Step 3: Register in DI**

In `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`, add next to `services.AddScoped<IContractorService, ContractorService>();`:

```csharp
        services.AddScoped<IExecutionStageService, ExecutionStageService>();
```

- [ ] **Step 4: Build**

```bash
dotnet build src/SmartInvest.API/SmartInvest.API.csproj
```

Expected: `Build succeeded`. If `ISubProjectRepository.SearchAsync`'s parameter order differs from what's written above, fix the call site to match `Backend/src/SmartInvest.Domain/Interfaces/ISubProjectRepository.cs`'s actual signature (`mainProjectId, mainProgramId, subProgramId, markazId, priorityId, statusId, financialYearId, searchTerm, page, pageSize`) rather than changing the interface.

- [ ] **Step 5: Commit**

```bash
git add Backend/src/SmartInvest.Application/Interfaces/IExecutionStageService.cs Backend/src/SmartInvest.Infrastructure/Services/ExecutionStageService.cs Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs
git commit -m "feat: Phase 3 ExecutionStageService — CRUD, execution-order + overrun validation, follow-up list"
```

---

### Task 4: `ExecutionStagesController`

**Files:**
- Create: `Backend/src/SmartInvest.API/Controllers/ExecutionStagesController.cs`

**Interfaces:**
- Consumes: `IExecutionStageService` (Task 3), `FileRequestHelpers` (`Backend/src/SmartInvest.API/Common/FileRequestHelpers.cs`, already exists).
- Produces: `GET api/follow-up`, `GET/POST api/subprojects/{id}/execution-stages`, `PUT .../complete`, `PUT .../penalty`, `GET .../files/{fileKey}` — Task 7's frontend service calls these exact routes.

- [ ] **Step 1: Write the controller**

```csharp
// Backend/src/SmartInvest.API/Controllers/ExecutionStagesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.API.Common;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

/// <summary>مراحل التنفيذ بعد الترسية (متابعة المشروعات).</summary>
[ApiController]
[Authorize]
public class ExecutionStagesController : ControllerBase
{
    private readonly IExecutionStageService _executionStageService;

    public ExecutionStagesController(IExecutionStageService executionStageService)
    {
        _executionStageService = executionStageService;
    }

    /// <summary>جدول متابعة المشروعات — مشروعات معتمدة فقط، بنفس فلاتر صفحة المشروعات.</summary>
    [HttpGet("api/follow-up")]
    public async Task<ActionResult<IReadOnlyList<FollowUpListItemDto>>> GetFollowUpList(
        [FromQuery] int? financialYearId,
        [FromQuery] int? mainProgramId,
        [FromQuery] int? subProgramId,
        [FromQuery] int? markazId,
        [FromQuery] int? priorityId,
        [FromQuery] string? searchTerm,
        CancellationToken cancellationToken)
    {
        var result = await _executionStageService.GetFollowUpListAsync(
            financialYearId, mainProgramId, subProgramId, markazId, priorityId, searchTerm, cancellationToken);
        return Ok(result);
    }

    [HttpGet("api/subprojects/{subProjectId:int}/execution-stages")]
    public async Task<ActionResult<IReadOnlyList<ExecutionStageDto>>> GetBySubProject(int subProjectId, CancellationToken cancellationToken)
    {
        var result = await _executionStageService.GetBySubProjectAsync(subProjectId, cancellationToken);
        return Ok(result);
    }

    /// <summary>multipart/form-data: name, deadline, selfFundingSpent, bankFundingSpent, physicalProgressPercent, notes + حتى 3 ملفات (selfFundingProof / bankFundingProof / physicalProgressProof).</summary>
    [HttpPost("api/subprojects/{subProjectId:int}/execution-stages")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<ExecutionStageDto>> Create(
        int subProjectId,
        [FromForm] string name,
        [FromForm] DateTime deadline,
        [FromForm] decimal selfFundingSpent,
        [FromForm] decimal bankFundingSpent,
        [FromForm] decimal physicalProgressPercent,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        var dto = new CreateExecutionStageDto
        {
            Name = name,
            Deadline = deadline,
            SelfFundingSpent = selfFundingSpent,
            BankFundingSpent = bankFundingSpent,
            PhysicalProgressPercent = physicalProgressPercent,
            Notes = notes,
        };

        var selfFile = Request.Form.Files["selfFundingProof"];
        if (selfFile is { Length: > 0 })
        {
            dto.SelfFundingProofFile = await FileRequestHelpers.ToUploadDtoAsync(selfFile, cancellationToken);
        }

        var bankFile = Request.Form.Files["bankFundingProof"];
        if (bankFile is { Length: > 0 })
        {
            dto.BankFundingProofFile = await FileRequestHelpers.ToUploadDtoAsync(bankFile, cancellationToken);
        }

        var progressFile = Request.Form.Files["physicalProgressProof"];
        if (progressFile is { Length: > 0 })
        {
            dto.PhysicalProgressProofFile = await FileRequestHelpers.ToUploadDtoAsync(progressFile, cancellationToken);
        }

        var result = await _executionStageService.CreateAsync(subProjectId, dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("api/subprojects/{subProjectId:int}/execution-stages/{stageId:int}/complete")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<ExecutionStageDto>> MarkComplete(int subProjectId, int stageId, CancellationToken cancellationToken)
    {
        var result = await _executionStageService.MarkCompleteAsync(subProjectId, stageId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("api/subprojects/{subProjectId:int}/execution-stages/{stageId:int}/penalty")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<ExecutionStageDto>> SetPenalty(int subProjectId, int stageId, SetExecutionStagePenaltyDto dto, CancellationToken cancellationToken)
    {
        var result = await _executionStageService.SetPenaltyAsync(subProjectId, stageId, dto, cancellationToken);
        return Ok(result);
    }

    [HttpGet("api/subprojects/{subProjectId:int}/execution-stages/{stageId:int}/files/{fileKey}")]
    public async Task<IActionResult> DownloadFile(int subProjectId, int stageId, string fileKey, CancellationToken cancellationToken)
    {
        var file = await _executionStageService.DownloadFileAsync(subProjectId, stageId, fileKey, cancellationToken);
        return File(file.Content, FileRequestHelpers.GetContentType(file.FileExtension), file.FileName);
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/SmartInvest.API/SmartInvest.API.csproj
```

Expected: `Build succeeded`.

- [ ] **Step 3: Live-check with curl**

Restart the backend (kill any running `SmartInvest.API` process first), log in, then:

```bash
curl -sk https://localhost:7250/api/follow-up -H "Authorization: Bearer $TOKEN"
```

Expected: `200 OK` with a JSON array (empty is fine — no stages exist yet).

- [ ] **Step 4: Commit**

```bash
git add Backend/src/SmartInvest.API/Controllers/ExecutionStagesController.cs
git commit -m "feat: Phase 3 ExecutionStagesController — follow-up list + per-project stage CRUD"
```

---

### Task 5: Contractor profile — notes, fines rollup, will-work-again flag

**Files:**
- Create: `Backend/src/SmartInvest.Application/DTOs/ContractorNoteDtos.cs`
- Modify: `Backend/src/SmartInvest.Application/DTOs/ContractorDtos.cs`
- Modify: `Backend/src/SmartInvest.Application/Interfaces/IContractorService.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/ContractorService.cs`
- Modify: `Backend/src/SmartInvest.API/Controllers/ContractorsController.cs`

**Interfaces:**
- Produces: `ContractorDto.WillWorkAgain`, `.TotalFines`, `.UnpaidFines`, `.Notes`; `IContractorService.UpdateWillWorkAgainAsync`, `.AddNoteAsync`; `GET/PUT api/contractors/{id}/will-work-again`, `POST api/contractors/{id}/notes` — Task 9's frontend consumes these.

- [ ] **Step 1: Note DTOs**

```csharp
// Backend/src/SmartInvest.Application/DTOs/ContractorNoteDtos.cs
namespace SmartInvest.Application.DTOs;

public class ContractorNoteDto
{
    public int Id { get; set; }
    public int? SubProjectId { get; set; }
    public string? SubProjectName { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsAiGenerated { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateContractorNoteDto
{
    public int? SubProjectId { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class SetWillWorkAgainDto
{
    public bool? WillWorkAgain { get; set; }
}
```

- [ ] **Step 2: Extend `ContractorDto`**

In `Backend/src/SmartInvest.Application/DTOs/ContractorDtos.cs`, add to `ContractorDto`:

```csharp
    public bool? WillWorkAgain { get; set; }
    public decimal TotalFines { get; set; }
    public decimal UnpaidFines { get; set; }
    public List<ContractorNoteDto> Notes { get; set; } = new();
```

- [ ] **Step 3: Extend `IContractorService`**

In `Backend/src/SmartInvest.Application/Interfaces/IContractorService.cs`, add:

```csharp
    Task<ContractorDto> SetWillWorkAgainAsync(int id, SetWillWorkAgainDto dto, CancellationToken cancellationToken = default);

    Task<ContractorNoteDto> AddNoteAsync(int id, CreateContractorNoteDto dto, CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Implement in `ContractorService`**

In `Backend/src/SmartInvest.Application/Services/ContractorService.cs`, first widen the constructor to also take `IGenericRepository<ExecutionStage>` and `IGenericRepository<ContractorNote>`:

```csharp
using SmartInvest.Domain.Entities;
// ... existing usings unchanged

public class ContractorService : IContractorService
{
    private readonly IGenericRepository<Contractor> _contractorRepository;
    private readonly IGenericRepository<ProjectAssignment> _assignmentRepository;
    private readonly IProjectAssignmentRepository _projectAssignmentRepository;
    private readonly IGenericRepository<ExecutionStage> _executionStageRepository;
    private readonly IGenericRepository<ContractorNote> _noteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ContractorService(
        IGenericRepository<Contractor> contractorRepository,
        IGenericRepository<ProjectAssignment> assignmentRepository,
        IProjectAssignmentRepository projectAssignmentRepository,
        IGenericRepository<ExecutionStage> executionStageRepository,
        IGenericRepository<ContractorNote> noteRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _contractorRepository = contractorRepository;
        _assignmentRepository = assignmentRepository;
        _projectAssignmentRepository = projectAssignmentRepository;
        _executionStageRepository = executionStageRepository;
        _noteRepository = noteRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }
```

Then extend `GetByIdAsync` to also fill fines + notes (replace the existing method body):

```csharp
    public async Task<ContractorDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var contractor = await GetOrThrowAsync(id, cancellationToken);
        var dto = _mapper.Map<ContractorDto>(contractor);

        var assignments = await _projectAssignmentRepository.GetByContractorAsync(id, cancellationToken);
        dto.AssignedSubProjects = assignments
            .Select(a => new AssignedSubProjectDto
            {
                Id = a.SubProject.SubProjectId,
                Name = a.SubProject.SubProjectName,
                MainProjectName = a.SubProject.MainProject.MainProjectName,
            })
            .ToList();

        var subProjectIds = assignments.Select(a => a.SubProjectId).ToHashSet();
        var stagesWithPenalty = subProjectIds.Count == 0
            ? []
            : await _executionStageRepository.FindAsync(
                s => subProjectIds.Contains(s.SubProjectId) && s.PenaltyAmount != null, cancellationToken);

        dto.TotalFines = stagesWithPenalty.Sum(s => s.PenaltyAmount ?? 0);
        dto.UnpaidFines = stagesWithPenalty.Where(s => !s.PenaltyPaid).Sum(s => s.PenaltyAmount ?? 0);

        var notes = await _noteRepository.FindAsync(n => n.ContractorId == id, cancellationToken);
        dto.Notes = notes
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new ContractorNoteDto
            {
                Id = n.ContractorNoteId,
                SubProjectId = n.SubProjectId,
                SubProjectName = n.SubProject?.SubProjectName,
                Text = n.Text,
                IsAiGenerated = n.IsAiGenerated,
                CreatedAt = n.CreatedAt,
            })
            .ToList();

        return dto;
    }

    public async Task<ContractorDto> SetWillWorkAgainAsync(int id, SetWillWorkAgainDto dto, CancellationToken cancellationToken = default)
    {
        var contractor = await GetOrThrowAsync(id, cancellationToken);
        contractor.WillWorkAgain = dto.WillWorkAgain;

        _contractorRepository.Update(contractor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ContractorNoteDto> AddNoteAsync(int id, CreateContractorNoteDto dto, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(dto.Text))
        {
            throw new BusinessRuleException("نص الملاحظة مطلوب");
        }

        var note = new ContractorNote
        {
            ContractorId = id,
            SubProjectId = dto.SubProjectId,
            Text = dto.Text.Trim(),
            IsAiGenerated = false,
        };

        await _noteRepository.AddAsync(note, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ContractorNoteDto
        {
            Id = note.ContractorNoteId,
            SubProjectId = note.SubProjectId,
            Text = note.Text,
            IsAiGenerated = false,
            CreatedAt = note.CreatedAt,
        };
    }
```

- [ ] **Step 5: Controller endpoints**

In `Backend/src/SmartInvest.API/Controllers/ContractorsController.cs`, add:

```csharp
    [HttpPut("{id:int}/will-work-again")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<ContractorDto>> SetWillWorkAgain(int id, SetWillWorkAgainDto dto, CancellationToken cancellationToken)
    {
        var result = await _contractorService.SetWillWorkAgainAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:int}/notes")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<ContractorNoteDto>> AddNote(int id, CreateContractorNoteDto dto, CancellationToken cancellationToken)
    {
        var result = await _contractorService.AddNoteAsync(id, dto, cancellationToken);
        return Ok(result);
    }
```

- [ ] **Step 6: Build**

```bash
dotnet build src/SmartInvest.API/SmartInvest.API.csproj
```

Expected: `Build succeeded`.

- [ ] **Step 7: Commit**

```bash
git add Backend/src/SmartInvest.Application/DTOs/ContractorNoteDtos.cs Backend/src/SmartInvest.Application/DTOs/ContractorDtos.cs Backend/src/SmartInvest.Application/Interfaces/IContractorService.cs Backend/src/SmartInvest.Application/Services/ContractorService.cs Backend/src/SmartInvest.API/Controllers/ContractorsController.cs
git commit -m "feat: Phase 3 contractor profile — fines rollup, will-work-again flag, notes"
```

---

### Task 6: Frontend models + service

**Files:**
- Create: `Frontend/src/app/core/models/follow-up.models.ts`
- Create: `Frontend/src/app/core/services/follow-up.service.ts`
- Modify: `Frontend/src/app/core/models/contractor.models.ts`
- Modify: `Frontend/src/app/core/services/contractors.service.ts`

**Interfaces:**
- Consumes: routes from Task 4 (`api/follow-up`, `api/subprojects/{id}/execution-stages`) and Task 5 (`api/contractors/{id}/will-work-again`, `api/contractors/{id}/notes`).
- Produces: `FollowUpListItem`, `ExecutionStage`, `CreateExecutionStagePayload` types + `FollowUpService` methods — Task 7/8/9 consume these exact names.

- [ ] **Step 1: Follow-up models**

```typescript
// Frontend/src/app/core/models/follow-up.models.ts
export interface FollowUpListItem {
  subProjectId: number;
  subProjectName: string;
  subProjectCode: string | null;
  mainProjectName: string;
  contractorName: string | null;
  isStalled: boolean;
  financialProgressPercent: number;
  physicalProgressPercent: number;
  nextDeadline: string | null;
  stageCount: number;
}

export interface ExecutionStage {
  id: number;
  subProjectId: number;
  name: string;
  deadline: string;
  selfFundingSpent: number;
  bankFundingSpent: number;
  hasSelfFundingProof: boolean;
  hasBankFundingProof: boolean;
  physicalProgressPercent: number;
  hasPhysicalProgressProof: boolean;
  notes: string | null;
  penaltyAmount: number | null;
  penaltyPaid: boolean;
  isCompleted: boolean;
  createdAt: string;
  completedAt: string | null;
}

export interface CreateExecutionStagePayload {
  name: string;
  deadline: string;
  selfFundingSpent: number;
  bankFundingSpent: number;
  physicalProgressPercent: number;
  notes: string;
  selfFundingProof: File | null;
  bankFundingProof: File | null;
  physicalProgressProof: File | null;
}

export interface FollowUpFilters {
  financialYearId?: number | null;
  mainProgramId?: number | null;
  subProgramId?: number | null;
  markazId?: number | null;
  priorityId?: number | null;
  searchTerm?: string | null;
}
```

- [ ] **Step 2: Follow-up service**

```typescript
// Frontend/src/app/core/services/follow-up.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateExecutionStagePayload,
  ExecutionStage,
  FollowUpFilters,
  FollowUpListItem,
} from '../models/follow-up.models';

@Injectable({ providedIn: 'root' })
export class FollowUpService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  getList(filters: FollowUpFilters): Observable<FollowUpListItem[]> {
    const params: Record<string, string | number> = {};
    if (filters.financialYearId != null) params['financialYearId'] = filters.financialYearId;
    if (filters.mainProgramId != null) params['mainProgramId'] = filters.mainProgramId;
    if (filters.subProgramId != null) params['subProgramId'] = filters.subProgramId;
    if (filters.markazId != null) params['markazId'] = filters.markazId;
    if (filters.priorityId != null) params['priorityId'] = filters.priorityId;
    if (filters.searchTerm) params['searchTerm'] = filters.searchTerm;
    return this.http.get<FollowUpListItem[]>(`${this.base}/follow-up`, { params });
  }

  getStages(subProjectId: number): Observable<ExecutionStage[]> {
    return this.http.get<ExecutionStage[]>(`${this.base}/subprojects/${subProjectId}/execution-stages`);
  }

  createStage(subProjectId: number, payload: CreateExecutionStagePayload): Observable<ExecutionStage> {
    const form = new FormData();
    form.append('name', payload.name);
    form.append('deadline', payload.deadline);
    form.append('selfFundingSpent', String(payload.selfFundingSpent));
    form.append('bankFundingSpent', String(payload.bankFundingSpent));
    form.append('physicalProgressPercent', String(payload.physicalProgressPercent));
    if (payload.notes.trim()) form.append('notes', payload.notes.trim());
    if (payload.selfFundingProof) form.append('selfFundingProof', payload.selfFundingProof, payload.selfFundingProof.name);
    if (payload.bankFundingProof) form.append('bankFundingProof', payload.bankFundingProof, payload.bankFundingProof.name);
    if (payload.physicalProgressProof) form.append('physicalProgressProof', payload.physicalProgressProof, payload.physicalProgressProof.name);

    return this.http.post<ExecutionStage>(`${this.base}/subprojects/${subProjectId}/execution-stages`, form);
  }

  markComplete(subProjectId: number, stageId: number): Observable<ExecutionStage> {
    return this.http.put<ExecutionStage>(
      `${this.base}/subprojects/${subProjectId}/execution-stages/${stageId}/complete`,
      {},
    );
  }

  setPenalty(
    subProjectId: number,
    stageId: number,
    penaltyAmount: number | null,
    penaltyPaid: boolean,
  ): Observable<ExecutionStage> {
    return this.http.put<ExecutionStage>(
      `${this.base}/subprojects/${subProjectId}/execution-stages/${stageId}/penalty`,
      { penaltyAmount, penaltyPaid },
    );
  }

  downloadFileUrl(subProjectId: number, stageId: number, fileKey: 'self' | 'bank' | 'progress'): string {
    return `${this.base}/subprojects/${subProjectId}/execution-stages/${stageId}/files/${fileKey}`;
  }
}
```

- [ ] **Step 3: Extend contractor model + service**

In `Frontend/src/app/core/models/contractor.models.ts`, add to the `Contractor` interface (name may be `ContractorDto` — match whatever the existing interface is called):

```typescript
export interface ContractorNote {
  id: number;
  subProjectId: number | null;
  subProjectName: string | null;
  text: string;
  isAiGenerated: boolean;
  createdAt: string;
}
```

And add these fields to the existing contractor detail interface: `willWorkAgain: boolean | null; totalFines: number; unpaidFines: number; notes: ContractorNote[];`

In `Frontend/src/app/core/services/contractors.service.ts`, add:

```typescript
  setWillWorkAgain(id: number, willWorkAgain: boolean | null): Observable<Contractor> {
    return this.http.put<Contractor>(`${this.base}/contractors/${id}/will-work-again`, { willWorkAgain });
  }

  addNote(id: number, text: string, subProjectId: number | null): Observable<ContractorNote> {
    return this.http.post<ContractorNote>(`${this.base}/contractors/${id}/notes`, { text, subProjectId });
  }
```

(Match the exact existing `Contractor` interface name, `base` property name, and constructor injection style already in that file — read it first before editing.)

- [ ] **Step 4: Build**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.json
```

Expected: no new type errors (pre-existing unrelated errors, if any, are not this task's concern).

- [ ] **Step 5: Commit**

```bash
git add Frontend/src/app/core/models/follow-up.models.ts Frontend/src/app/core/services/follow-up.service.ts Frontend/src/app/core/models/contractor.models.ts Frontend/src/app/core/services/contractors.service.ts
git commit -m "feat: Phase 3 frontend models + services"
```

---

### Task 7: متابعة المشروعات list page + route + nav item

**Files:**
- Create: `Frontend/src/app/features/follow-up/follow-up-list.ts`
- Create: `Frontend/src/app/features/follow-up/follow-up-list.html`
- Create: `Frontend/src/app/features/follow-up/follow-up-list.css`
- Modify: `Frontend/src/app/app.routes.ts`
- Modify: `Frontend/src/app/layout/main-layout/main-layout.ts`

**Interfaces:**
- Consumes: `FollowUpService.getList` (Task 6).
- Produces: route `/app/follow-up`, component `FollowUpList` with a public `openStages(item)` hook — Task 8 wires the stage-detail modal into this same component.

- [ ] **Step 1: Component**

```typescript
// Frontend/src/app/features/follow-up/follow-up-list.ts
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FollowUpService } from '../../core/services/follow-up.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { FollowUpListItem } from '../../core/models/follow-up.models';
import { FinancialYear } from '../../core/models/project.models';

@Component({
  selector: 'app-follow-up-list',
  imports: [FormsModule],
  templateUrl: './follow-up-list.html',
  styleUrl: './follow-up-list.css',
})
export class FollowUpList implements OnInit {
  private readonly followUp = inject(FollowUpService);
  private readonly financialYearsService = inject(FinancialYearsService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly items = signal<FollowUpListItem[]>([]);
  protected readonly search = signal('');

  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly selectedYearId = signal<number | null>(null);
  protected readonly sortedYears = computed(() =>
    [...this.financialYears()].sort((a, b) => b.startDate.localeCompare(a.startDate)),
  );

  protected readonly filtered = computed(() => {
    const term = this.search().trim();
    if (!term) return this.items();
    return this.items().filter(
      (x) =>
        x.subProjectName.includes(term) ||
        (x.subProjectCode ?? '').includes(term) ||
        x.mainProjectName.includes(term),
    );
  });

  protected readonly kpiTotal = computed(() => this.items().length);
  protected readonly kpiStalled = computed(() => this.items().filter((x) => x.isStalled).length);
  protected readonly kpiOverdue = computed(
    () => this.items().filter((x) => x.nextDeadline && new Date(x.nextDeadline) < new Date()).length,
  );

  protected readonly selectedItem = signal<FollowUpListItem | null>(null);

  ngOnInit(): void {
    this.financialYearsService.getAll().subscribe({
      next: (years) => {
        this.financialYears.set(years);
        const sorted = [...years].sort((a, b) => b.startDate.localeCompare(a.startDate));
        if (sorted.length > 0) {
          this.selectedYearId.set(sorted[0].id);
        }
        this.load();
      },
      error: () => this.load(),
    });
  }

  protected onYearChange(id: number | null): void {
    this.selectedYearId.set(id);
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.followUp.getList({ financialYearId: this.selectedYearId() }).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذر تحميل بيانات متابعة المشروعات');
        this.loading.set(false);
      },
    });
  }

  protected openStages(item: FollowUpListItem): void {
    this.selectedItem.set(item);
  }

  protected closeStages(): void {
    this.selectedItem.set(null);
  }

  protected overdue(item: FollowUpListItem): boolean {
    return !!item.nextDeadline && new Date(item.nextDeadline) < new Date();
  }
}
```

- [ ] **Step 2: Template** (list only — the stage modal itself is Task 8; leave a placeholder `@if (selectedItem())` block Task 8 fills in)

```html
<!-- Frontend/src/app/features/follow-up/follow-up-list.html -->
<div class="page">
  <header class="page-head">
    <div>
      <h1>متابعة المشروعات</h1>
      <p>نسبة التنفيذ المالي والعيني لكل مشروع فرعي معتمد، ومراحل تنفيذه بعد الترسية</p>
    </div>
  </header>

  <div class="toolbar">
    <select class="mini" [ngModel]="selectedYearId()" (ngModelChange)="onYearChange($event)">
      @for (y of sortedYears(); track y.id) { <option [ngValue]="y.id">{{ y.name }}</option> }
    </select>
  </div>

  @if (loading()) {
    <div class="state"><span class="spinner"></span> جاري التحميل…</div>
  } @else if (error()) {
    <div class="state error">{{ error() }}</div>
  } @else {
    <div class="kpis">
      <div class="kpi">
        <span>إجمالي المشروعات</span>
        <b class="tnum">{{ kpiTotal() }}</b>
      </div>
      <div class="kpi">
        <span>متعثرة</span>
        <b class="tnum warn">{{ kpiStalled() }}</b>
      </div>
      <div class="kpi">
        <span>متأخرة عن الموعد</span>
        <b class="tnum warn">{{ kpiOverdue() }}</b>
      </div>
    </div>

    <div class="card">
      <div class="card-head">
        <h3>المشروعات الفرعية</h3>
        <div class="grow"></div>
        <input
          class="search"
          placeholder="بحث بالاسم أو الكود…"
          [ngModel]="search()"
          (ngModelChange)="search.set($event)"
        />
      </div>
      <div class="tbl-wrap">
        <table>
          <thead>
            <tr>
              <th>اسم المشروع الفرعي</th>
              <th>المقاول</th>
              <th>% التنفيذ المالي</th>
              <th>% التنفيذ العيني</th>
              <th>أقرب موعد مرحلة</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (item of filtered(); track item.subProjectId) {
              <tr>
                <td>
                  {{ item.subProjectName }}
                  @if (item.isStalled) { <span class="chip warn-chip">متعثر</span> }
                </td>
                <td>{{ item.contractorName ?? 'غير مسند' }}</td>
                <td class="tnum">{{ item.financialProgressPercent }}%</td>
                <td class="tnum">{{ item.physicalProgressPercent }}%</td>
                <td class="tnum" [class.warn]="overdue(item)">
                  {{ item.nextDeadline ? (item.nextDeadline | date: 'yyyy/MM/dd') : '—' }}
                </td>
                <td>
                  <button class="si-btn primary sm" (click)="openStages(item)">عرض المراحل</button>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="empty">لا توجد مشروعات فرعية معتمدة لهذه السنة.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  }
</div>
```

Add `DatePipe` to the component's `imports` array (`import { DatePipe } from '@angular/common';` + `imports: [FormsModule, DatePipe]`) since the template uses `| date`.

- [ ] **Step 3: CSS** — copy `Frontend/src/app/features/financial/financial.css` verbatim to `follow-up-list.css` (identical `.page`/`.kpis`/`.card`/`.tbl-wrap`/`.toolbar` layout is already defined there; add one rule for the stalled chip and warn text used above):

```bash
cp Frontend/src/app/features/financial/financial.css Frontend/src/app/features/follow-up/follow-up-list.css
```

Then append to the copied file:

```css
.warn-chip { background: var(--bad-bg); color: var(--bad); margin-inline-start: 8px; }
.tnum.warn { color: var(--bad); font-weight: 700; }
```

(If `--bad`/`--bad-bg` aren't defined as CSS variables in `styles.css`, use the same literal colors `financial.css` already uses for its own warning states instead — check that file for the exact token names before assuming.)

- [ ] **Step 4: Route**

In `Frontend/src/app/app.routes.ts`, add after the `financial/:id` route:

```typescript
      {
        path: 'follow-up',
        loadComponent: () =>
          import('./features/follow-up/follow-up-list').then((m) => m.FollowUpList),
      },
```

- [ ] **Step 5: Sidebar nav item**

In `Frontend/src/app/layout/main-layout/main-layout.ts`, add after the الإدارة المالية entry:

```typescript
    { label: 'متابعة المشروعات', route: '/app/follow-up', icon: 'M9 11l3 3L22 4M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11', managerOnly: false },
```

- [ ] **Step 6: Live-check**

```bash
cd Frontend && npx ng build
```

Expected: build succeeds. Then in the browser: navigate to `/app/follow-up`, confirm the sidebar shows "متابعة المشروعات", the year selector populates, and the table renders (empty is fine — no `ExecutionStage` rows exist yet).

- [ ] **Step 7: Commit**

```bash
git add Frontend/src/app/features/follow-up/ Frontend/src/app/app.routes.ts Frontend/src/app/layout/main-layout/main-layout.ts
git commit -m "feat: Phase 3 متابعة المشروعات list page"
```

---

### Task 8: Stage-detail modal — view/add stages, proof uploads, complete, penalty

**Files:**
- Modify: `Frontend/src/app/features/follow-up/follow-up-list.ts`
- Modify: `Frontend/src/app/features/follow-up/follow-up-list.html`

**Interfaces:**
- Consumes: `FollowUpService.getStages/createStage/markComplete/setPenalty/downloadFileUrl` (Task 6).

- [ ] **Step 1: Extend the component**

Add to `follow-up-list.ts` (inside the `FollowUpList` class, after `closeStages`):

```typescript
  protected readonly stages = signal<ExecutionStage[]>([]);
  protected readonly stagesLoading = signal(false);
  protected readonly showAddStage = signal(false);
  protected readonly savingStage = signal(false);
  protected readonly stageError = signal<string | null>(null);

  protected readonly newStageName = signal('');
  protected readonly newStageDeadline = signal('');
  protected readonly newStageSelfSpent = signal(0);
  protected readonly newStageBankSpent = signal(0);
  protected readonly newStageProgress = signal(0);
  protected readonly newStageNotes = signal('');
  protected newStageSelfFile: File | null = null;
  protected newStageBankFile: File | null = null;
  protected newStageProgressFile: File | null = null;

  protected onSelectStages(item: FollowUpListItem): void {
    this.openStages(item);
    this.loadStages(item.subProjectId);
  }

  private loadStages(subProjectId: number): void {
    this.stagesLoading.set(true);
    this.followUp.getStages(subProjectId).subscribe({
      next: (stages) => {
        this.stages.set(stages);
        this.stagesLoading.set(false);
      },
      error: () => this.stagesLoading.set(false),
    });
  }

  protected openAddStage(): void {
    this.newStageName.set('');
    this.newStageDeadline.set('');
    this.newStageSelfSpent.set(0);
    this.newStageBankSpent.set(0);
    this.newStageProgress.set(0);
    this.newStageNotes.set('');
    this.newStageSelfFile = null;
    this.newStageBankFile = null;
    this.newStageProgressFile = null;
    this.stageError.set(null);
    this.showAddStage.set(true);
  }

  protected closeAddStage(): void {
    this.showAddStage.set(false);
  }

  protected onSelfFileChange(event: Event): void {
    this.newStageSelfFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected onBankFileChange(event: Event): void {
    this.newStageBankFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected onProgressFileChange(event: Event): void {
    this.newStageProgressFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected saveNewStage(): void {
    const item = this.selectedItem();
    if (!item || this.savingStage()) return;

    if (!this.newStageName().trim() || !this.newStageDeadline()) {
      this.stageError.set('اسم المرحلة والموعد النهائي مطلوبان');
      return;
    }

    this.savingStage.set(true);
    this.stageError.set(null);

    this.followUp
      .createStage(item.subProjectId, {
        name: this.newStageName().trim(),
        deadline: this.newStageDeadline(),
        selfFundingSpent: this.newStageSelfSpent(),
        bankFundingSpent: this.newStageBankSpent(),
        physicalProgressPercent: this.newStageProgress(),
        notes: this.newStageNotes(),
        selfFundingProof: this.newStageSelfFile,
        bankFundingProof: this.newStageBankFile,
        physicalProgressProof: this.newStageProgressFile,
      })
      .subscribe({
        next: () => {
          this.savingStage.set(false);
          this.showAddStage.set(false);
          this.loadStages(item.subProjectId);
          this.load();
        },
        error: (err) => {
          this.savingStage.set(false);
          this.stageError.set(err?.error?.message ?? 'تعذّر حفظ المرحلة');
        },
      });
  }

  protected completeStage(stage: ExecutionStage): void {
    const item = this.selectedItem();
    if (!item) return;
    this.followUp.markComplete(item.subProjectId, stage.id).subscribe({
      next: () => this.loadStages(item.subProjectId),
    });
  }

  protected fileUrl(stage: ExecutionStage, key: 'self' | 'bank' | 'progress'): string {
    return this.followUp.downloadFileUrl(stage.subProjectId, stage.id, key);
  }

  protected money(value: number | null | undefined): string {
    return (value ?? 0).toLocaleString('en-US');
  }
```

Add `import { ExecutionStage } from '../../core/models/follow-up.models';` (merge with the existing `FollowUpListItem` import) and change every `openStages(item)` template call target: the button in Task 7's template should call `onSelectStages(item)` instead of `openStages(item)` — go back and fix that one line in the `<button (click)="openStages(item)">` from Task 7.

- [ ] **Step 2: Add the modal markup**

Append to the end of `follow-up-list.html`, right before the closing `</div>` of `.page`:

```html
  @if (selectedItem(); as item) {
    <div class="si-overlay" (click)="closeStages()">
      <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(760px,100%)">
        <div class="si-modal-head">
          <div class="grow">
            <h3>مراحل تنفيذ: {{ item.subProjectName }}</h3>
            <p>{{ item.mainProjectName }}</p>
          </div>
          <button class="si-btn primary sm" (click)="openAddStage()">+ مرحلة جديدة</button>
          <button class="si-x" (click)="closeStages()">×</button>
        </div>
        <div class="si-modal-body">
          @if (stagesLoading()) {
            <div class="state"><span class="spinner"></span> جاري التحميل…</div>
          } @else {
            <div class="tbl-wrap">
              <table>
                <thead>
                  <tr>
                    <th>المرحلة</th>
                    <th>الموعد</th>
                    <th>ذاتي</th>
                    <th>بنكي</th>
                    <th>تنفيذ عيني</th>
                    <th>غرامة</th>
                    <th>الحالة</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  @for (s of stages(); track s.id) {
                    <tr>
                      <td>{{ s.name }}</td>
                      <td class="tnum">{{ s.deadline | date: 'yyyy/MM/dd' }}</td>
                      <td class="tnum">
                        {{ money(s.selfFundingSpent) }}
                        @if (s.hasSelfFundingProof) {
                          <a [href]="fileUrl(s, 'self')" target="_blank" class="file-link">📎</a>
                        }
                      </td>
                      <td class="tnum">
                        {{ money(s.bankFundingSpent) }}
                        @if (s.hasBankFundingProof) {
                          <a [href]="fileUrl(s, 'bank')" target="_blank" class="file-link">📎</a>
                        }
                      </td>
                      <td class="tnum">
                        {{ s.physicalProgressPercent }}%
                        @if (s.hasPhysicalProgressProof) {
                          <a [href]="fileUrl(s, 'progress')" target="_blank" class="file-link">📎</a>
                        }
                      </td>
                      <td class="tnum">{{ s.penaltyAmount != null ? money(s.penaltyAmount) : '—' }}</td>
                      <td>
                        @if (s.isCompleted) {
                          <span class="st-pill ok">مكتملة</span>
                        } @else {
                          <button class="si-btn sm" (click)="completeStage(s)">إنهاء</button>
                        }
                      </td>
                      <td>@if (s.notes) { <span class="note-hint" [title]="s.notes">📝</span> }</td>
                    </tr>
                  } @empty {
                    <tr><td colspan="8" class="empty">لا توجد مراحل تنفيذ بعد.</td></tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
        <div class="si-modal-foot">
          <button class="si-btn" (click)="closeStages()">إغلاق</button>
        </div>
      </div>
    </div>
  }

  @if (showAddStage()) {
    <div class="si-overlay" (click)="closeAddStage()">
      <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(560px,100%)">
        <div class="si-modal-head">
          <div class="grow"><h3>مرحلة تنفيذ جديدة</h3></div>
          <button class="si-x" (click)="closeAddStage()">×</button>
        </div>
        <div class="si-modal-body">
          @if (stageError()) { <div class="si-err">{{ stageError() }}</div> }
          <div class="si-grid">
            <div class="si-fld full">
              <label>اسم المرحلة <span class="req">*</span></label>
              <input [ngModel]="newStageName()" (ngModelChange)="newStageName.set($event)" placeholder="مثال: رصف الطبقة الأولى" />
            </div>
            <div class="si-fld">
              <label>الموعد النهائي <span class="req">*</span></label>
              <input type="date" [ngModel]="newStageDeadline()" (ngModelChange)="newStageDeadline.set($event)" />
            </div>
            <div class="si-fld">
              <label>نسبة التنفيذ العيني %</label>
              <input type="number" min="0" max="100" [ngModel]="newStageProgress()" (ngModelChange)="newStageProgress.set($event)" />
            </div>
            <div class="si-fld">
              <label>مصروف ذاتي</label>
              <input type="number" min="0" [ngModel]="newStageSelfSpent()" (ngModelChange)="newStageSelfSpent.set($event)" />
            </div>
            <div class="si-fld">
              <label>مصروف بنكي</label>
              <input type="number" min="0" [ngModel]="newStageBankSpent()" (ngModelChange)="newStageBankSpent.set($event)" />
            </div>
            <div class="si-fld">
              <label>إثبات الصرف الذاتي</label>
              <input type="file" (change)="onSelfFileChange($event)" />
            </div>
            <div class="si-fld">
              <label>إثبات الصرف البنكي</label>
              <input type="file" (change)="onBankFileChange($event)" />
            </div>
            <div class="si-fld">
              <label>إثبات التنفيذ العيني</label>
              <input type="file" (change)="onProgressFileChange($event)" />
            </div>
            <div class="si-fld full">
              <label>ملاحظات</label>
              <textarea rows="3" [ngModel]="newStageNotes()" (ngModelChange)="newStageNotes.set($event)"></textarea>
            </div>
          </div>
        </div>
        <div class="si-modal-foot">
          <button class="si-btn primary" [disabled]="savingStage()" (click)="saveNewStage()">
            @if (savingStage()) { جاري الحفظ… } @else { حفظ المرحلة }
          </button>
          <button class="si-btn" (click)="closeAddStage()">إلغاء</button>
        </div>
      </div>
    </div>
  }
```

Also fix the Task 7 button to call the loader too: in the table row, change `(click)="openStages(item)"` to `(click)="onSelectStages(item)"`.

Add `DatePipe` usage is already imported from Task 7; no further import changes needed beyond the `ExecutionStage` type import noted in Step 1.

- [ ] **Step 2: Build**

```bash
cd Frontend && npx ng build
```

Expected: build succeeds.

- [ ] **Step 3: Live walkthrough**

In the browser: pick an approved sub-project whose العقد والترسية stage is completed (use one already set up from earlier procurement testing, or complete one via `/app/financial/:id`). Open متابعة المشروعات, click "عرض المراحل", click "+ مرحلة جديدة", fill in a مقاولات-type project's stage with a self-funding spend and its proof file, save. Confirm:
- The stage appears in the table with the 📎 proof link working (opens/downloads the file).
- The list page's % التنفيذ المالي updated after closing the modal.
- Try creating a stage on a توريدات project with spend > 0 but progress = 0 — expect the `BusinessRuleException` message to surface as `stageError()`.
- Try a spend that pushes total past `TotalCost × (1 + overrun%)` — expect the same kind of rejection.

- [ ] **Step 4: Commit**

```bash
git add Frontend/src/app/features/follow-up/
git commit -m "feat: Phase 3 stage-detail modal — add/view stages, proof uploads, complete, execution-order + overrun errors surfaced"
```

---

### Task 9: Contractor profile UI — assignment-flow summary + Contractors page

**Files:**
- Modify: `Frontend/src/app/features/financial/procurement-workflow.ts`
- Modify: `Frontend/src/app/features/financial/procurement-workflow.html`
- Modify: `Frontend/src/app/features/contractors/contractors.ts`
- Modify: `Frontend/src/app/features/contractors/contractors.html`

**Interfaces:**
- Consumes: `ContractorsService.setWillWorkAgain/addNote` (Task 6), the widened `Contractor`/`ContractorDto` fields (Task 6).

- [ ] **Step 1: Surface the profile summary during assignment**

In `procurement-workflow.ts`, add a computed that finds the full contractor record for the currently-selected `aContractorId()` (the `contractors()` signal already holds the full list per the earlier exploration — confirm its element type includes the new `willWorkAgain`/`totalFines`/`unpaidFines`/`notes` fields from Task 6's `Contractor` model; if `contractors()` is currently typed to a narrower list-item shape, switch its source call to the one returning full `ContractorDto`s, or add a dedicated fetch):

```typescript
  protected readonly selectedContractorProfile = computed(() => {
    const id = this.aContractorId();
    if (id == null) return null;
    return this.contractors().find((c) => c.id === id) ?? null;
  });
```

- [ ] **Step 2: Render it**

In `procurement-workflow.html`, right after the المقاول `<select>`'s closing `</div>` (inside the same `.si-grid` from the earlier "إسناد المشروع لمقاول" step), add a new full-width block:

```html
                      @if (selectedContractorProfile(); as profile) {
                        <div class="si-fld full contractor-summary">
                          <div class="cs-row">
                            <span>هل نتعامل معه تاني؟</span>
                            @if (profile.willWorkAgain === true) {
                              <span class="chip ok-chip">نعم</span>
                            } @else if (profile.willWorkAgain === false) {
                              <span class="chip warn-chip">لا</span>
                            } @else {
                              <span class="chip">لم يُقيَّم بعد</span>
                            }
                          </div>
                          @if (profile.totalFines > 0) {
                            <div class="cs-row">
                              <span>إجمالي الغرامات</span>
                              <b class="tnum warn">{{ profile.totalFines | number }} ج.م ({{ profile.unpaidFines | number }} غير مسددة)</b>
                            </div>
                          }
                          @if (profile.notes.length > 0) {
                            <div class="cs-row">
                              <span>آخر ملاحظة</span>
                              <span>{{ profile.notes[0].text }}</span>
                            </div>
                          }
                        </div>
                      }
```

Add `DecimalPipe` (`number` pipe) to the component's imports if not already present.

- [ ] **Step 3: Contractors page — flag + notes**

In `contractors.ts`, read the file first to find the existing edit-modal signals/methods, then add (following the exact same signal-per-field pattern already used there):

```typescript
  protected readonly noteText = signal('');
  protected readonly savingNote = signal(false);

  protected setWillWorkAgain(contractor: Contractor, value: boolean | null): void {
    this.contractorsService.setWillWorkAgain(contractor.id, value).subscribe({
      next: (updated) => {
        this.items.update((list) => list.map((c) => (c.id === updated.id ? updated : c)));
      },
    });
  }

  protected addNote(contractor: Contractor): void {
    const text = this.noteText().trim();
    if (!text || this.savingNote()) return;
    this.savingNote.set(true);
    this.contractorsService.addNote(contractor.id, text, null).subscribe({
      next: (note) => {
        this.savingNote.set(false);
        this.noteText.set('');
        this.items.update((list) =>
          list.map((c) => (c.id === contractor.id ? { ...c, notes: [note, ...c.notes] } : c)),
        );
      },
      error: () => this.savingNote.set(false),
    });
  }
```

(Exact field/method names in `items`/list signal must match what's already in `contractors.ts` — read the file first and adapt variable names to match, per the "In existing codebases, follow established patterns" constraint.)

In `contractors.html`, inside the existing detail/edit view for a contractor, add:

```html
<div class="wwa-row">
  <span>نتعامل معه تاني؟</span>
  <button class="si-btn sm" [class.primary]="contractor.willWorkAgain === true" (click)="setWillWorkAgain(contractor, true)">نعم</button>
  <button class="si-btn sm" [class.danger]="contractor.willWorkAgain === false" (click)="setWillWorkAgain(contractor, false)">لا</button>
</div>
<div class="notes-list">
  @for (n of contractor.notes; track n.id) {
    <div class="note-item">
      <span>{{ n.text }}</span>
      <small>{{ n.createdAt | date: 'yyyy/MM/dd' }} @if (n.isAiGenerated) { · AI }</small>
    </div>
  }
</div>
<div class="note-add">
  <input [ngModel]="noteText()" (ngModelChange)="noteText.set($event)" placeholder="أضف ملاحظة…" />
  <button class="si-btn sm primary" [disabled]="savingNote()" (click)="addNote(contractor)">إضافة</button>
</div>
```

(Adapt to however `contractors.html` currently structures its per-contractor detail block — read it first; the markup above is the content to insert, not necessarily the exact wrapper.)

- [ ] **Step 4: Build**

```bash
cd Frontend && npx ng build
```

Expected: build succeeds.

- [ ] **Step 5: Live walkthrough**

In the browser: open a sub-project's العقد والترسية stage, select a contractor that has at least one unpaid penalty recorded (create one via Task 8's stage-penalty flow first if none exist), confirm the fines summary appears next to the contractor select. Then go to `/app/settings/contractors` (or wherever the Contractors page lives), toggle "نتعامل معه تاني؟", add a note, confirm both persist after a page reload.

- [ ] **Step 6: Commit**

```bash
git add Frontend/src/app/features/financial/procurement-workflow.ts Frontend/src/app/features/financial/procurement-workflow.html Frontend/src/app/features/contractors/contractors.ts Frontend/src/app/features/contractors/contractors.html
git commit -m "feat: Phase 3 contractor profile UI — surfaced at assignment + notes/flag on Contractors page"
```

---

### Task 10: Final end-to-end verification

**Files:** none (verification only).

- [ ] **Step 1: Full backend build**

```bash
cd Backend && dotnet build src/SmartInvest.API/SmartInvest.API.csproj
```

Expected: `Build succeeded`, `0 Error(s)`.

- [ ] **Step 2: Full frontend build**

```bash
cd Frontend && npx ng build
```

Expected: build succeeds with no new errors.

- [ ] **Step 3: Confirm the migration applied cleanly on a real DB**

```bash
cd Backend && dotnet ef database update --project src/SmartInvest.Infrastructure --startup-project src/SmartInvest.API
```

Expected: `Done.` (or "No migrations were applied" if Task 1 already applied it — either is fine, just confirms no pending migration was missed).

- [ ] **Step 4: Live walkthrough — every business rule**

Restart the backend, open the app in the browser, and verify in order:
1. متابعة المشروعات appears in the sidebar for both PlanningEmployee and PlanningManager roles.
2. A sub-project whose العقد والترسية is NOT yet completed: attempting to add a stage returns "لا يمكن إضافة مرحلة تنفيذ قبل اكتمال مرحلة العقد والترسية" — confirm via the API directly (`curl -X POST .../execution-stages ...`) since the UI only exposes stage-adding for projects that already show up correctly.
3. A مقاولات project: add a stage with only `bankFundingSpent > 0` and no `physicalProgressPercent` — succeeds (advance-payment-first is allowed).
4. A توريدات project: add a stage with `bankFundingSpent > 0` and `physicalProgressPercent = 0` — rejected with the Arabic message from `CreateAsync`. Retry with `physicalProgressPercent > 0` and the matching proof file — succeeds.
5. Set `SubProject.OverrunPercentage` (via a direct API call, since Task 8's form doesn't expose it — note this as a known gap for a follow-up task if the user wants it editable from the UI) then add a stage whose spend pushes the running total past `TotalCost × (1+overrun%)` — rejected with the overrun message.
6. متابعة المشروعات table shows correct % financial (sum of spend / TotalCost) and % physical (latest stage's percent) for the project just tested, and shows a متعثر badge for any stalled project in the same financial year.
7. Contractor profile: assign the tested contractor to a new project, confirm the fines/will-work-again summary renders next to the select using real data from step 5's penalty (set a penalty via `PUT .../execution-stages/{id}/penalty` first if none exists yet).

- [ ] **Step 5: Report to user**

Summarize what was verified (per the bullet list above) and flag the one known UI gap from step 5 (`OverrunPercentage` has no dedicated edit UI yet — mention it, don't silently build one beyond what the spec asked for).

No commit for this task — it's verification only, nothing new to stage.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-08-08-project-tracking-phase3-plan.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
