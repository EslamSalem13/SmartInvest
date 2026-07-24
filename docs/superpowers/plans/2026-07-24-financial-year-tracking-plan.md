# Financial-Year Tracking + Plan Archive Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let sub-projects be tracked across one or more financial years directly, shrink `Plan` down to a printable archive snapshot, and remove the dead `InvestmentProject` stack + orphaned `ProjectStatus` enum found during the design review.

**Architecture:** .NET 10 Onion architecture (Domain → Application → Infrastructure → API), matching the existing SmartInvest codebase exactly — same conventions as every prior feature in this repo (thin controllers, `[Authorize(Roles=...)]` role gates, Arabic user-facing messages, generic repository for simple CRUD).

**Tech Stack:** ASP.NET Core 10 Web API, EF Core 10 (SQL Server), AutoMapper, FluentValidation. No test project exists in this repo — verification per task is `dotnet build` + manual HTTP calls via Swagger UI or curl, matching the codebase's existing convention.

## Global Constraints

- Backend only. Do not touch anything under `Frontend/`.
- All user-facing strings (validation messages, exceptions) must be Arabic, matching every existing message in the codebase.
- Follow the existing file/namespace conventions exactly: `SmartInvest.Domain.Entities`, `SmartInvest.Domain.Common`, `SmartInvest.Application.DTOs`, `SmartInvest.Application.Interfaces`, `SmartInvest.Application.Services`, `SmartInvest.Application.Validators`, `SmartInvest.Application.Common.Mappings`, `SmartInvest.Infrastructure.Data.Configurations`, `SmartInvest.API.Controllers`.
- Currency/financial fields use `[Column(TypeName = "decimal(18,2)")]` on the entity (matches `SubProject.BankFunding`/`SelfFunding`), not Fluent `HasColumnType`.
- `[Authorize]` attributes: class-level and method-level attributes AND-compose in ASP.NET Core — a method-level attribute can only **narrow** access (a role subset of the class-level gate), never **widen** it. If a class-level gate is `[Authorize(Roles = Roles.PlanningStaff)]`, no method on that controller can ever be reached by a role outside `PlanningStaff` no matter what its own `[Authorize]` says. Where an action needs to be MORE open than the rest of the controller, the class-level attribute must be bare `[Authorize]` and every action must carry its own explicit role list. This was a real, twice-confirmed bug in a previous phase of this codebase — do not repeat it.
- Cascade-delete avoidance: every new FK relationship uses `.OnDelete(DeleteBehavior.Restrict)` in Fluent configuration, matching the existing codebase-wide safety convention (see `SubProjectConfiguration.cs`'s comment: "منع الـ cascade delete اللي ممكن يمسح بيانات التخطيط بالغلط").
- Run `dotnet build Backend` after every task and confirm `0 Error(s)` before committing.
- EF migrations: `dotnet-ef` on this machine needs explicit flags to resolve the migrations-assembly mismatch — always run migration commands from `Backend/src/SmartInvest.API` as: `dotnet ef migrations add <Name> --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .` (and the same two flags for `database update`/`migrations remove`). The plain form without flags fails with `Your target project 'SmartInvest.API' doesn't match your migrations assembly...`.

---

## File Structure Overview

```
Backend/src/SmartInvest.Domain/
  Entities/InvestmentProject.cs                        [DELETE]
  Enums/ProjectStatus.cs                                [DELETE]
  Entities/ProjectFollowUp.cs                            [MODIFY]
  Entities/PlanProject.cs                                [MODIFY]
  Entities/Plan.cs                                       [MODIFY]
  Entities/SubProject.cs                                 [MODIFY]
  Entities/FinancialYear.cs                              [MODIFY]
  Entities/SubProjectFinancialYear.cs                    [CREATE]

Backend/src/SmartInvest.Infrastructure/
  Data/Configurations/PlanProjectConfiguration.cs        [MODIFY]
  Data/Configurations/ProjectFollowUpConfiguration.cs    [MODIFY]
  Data/Configurations/SubProjectFinancialYearConfiguration.cs [CREATE]
  Data/AppDbContext.cs                                   [MODIFY]
  DependencyInjection.cs                                 [MODIFY]
  Migrations/..._RemoveInvestmentProject                 [CREATE via dotnet ef]
  Migrations/..._AddSubProjectFinancialYearTracking       [CREATE via dotnet ef]

Backend/src/SmartInvest.Application/
  Interfaces/IInvestmentProjectService.cs                [DELETE]
  Services/InvestmentProjectService.cs                   [DELETE]
  DTOs/InvestmentProjectDtos.cs                           [DELETE]
  Common/Mappings/MappingProfile.cs                       [DELETE]
  DependencyInjection.cs                                  [MODIFY]
  DTOs/FinancialYearDtos.cs                                [CREATE]
  Validators/CreateFinancialYearDtoValidator.cs            [CREATE]
  Common/Mappings/FinancialYearMappingProfile.cs           [CREATE]
  Interfaces/IFinancialYearService.cs                      [CREATE]
  Services/FinancialYearService.cs                         [CREATE]
  DTOs/SubProjectFinancialYearDtos.cs                      [CREATE]
  Interfaces/ISubProjectFinancialYearService.cs            [CREATE]
  Services/SubProjectFinancialYearService.cs               [CREATE]
  DTOs/PlanDtos.cs                                         [CREATE]
  Validators/CreatePlanDtoValidator.cs                     [CREATE]
  Interfaces/IPlanService.cs                               [CREATE]
  Services/PlanService.cs                                  [CREATE]

Backend/src/SmartInvest.API/
  Controllers/InvestmentProjectsController.cs             [DELETE]
  Controllers/FinancialYearsController.cs                  [CREATE]
  Controllers/SubProjectFinancialYearsController.cs         [CREATE]
  Controllers/PlansController.cs                            [CREATE]
```

---

### Task 1: Remove dead code (InvestmentProject stack, orphaned ProjectStatus enum)

**Files:**
- Delete: `Backend/src/SmartInvest.Domain/Entities/InvestmentProject.cs`
- Delete: `Backend/src/SmartInvest.Domain/Enums/ProjectStatus.cs`
- Delete: `Backend/src/SmartInvest.Application/Interfaces/IInvestmentProjectService.cs`
- Delete: `Backend/src/SmartInvest.Application/Services/InvestmentProjectService.cs`
- Delete: `Backend/src/SmartInvest.Application/DTOs/InvestmentProjectDtos.cs`
- Delete: `Backend/src/SmartInvest.Application/Common/Mappings/MappingProfile.cs`
- Delete: `Backend/src/SmartInvest.API/Controllers/InvestmentProjectsController.cs`
- Modify: `Backend/src/SmartInvest.Application/DependencyInjection.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Data/AppDbContext.cs`
- Modify: `Backend/src/SmartInvest.Domain/Entities/ProjectFollowUp.cs`

**Interfaces:**
- Consumes: nothing from later tasks.
- Produces: a clean tree with no `InvestmentProject`/`Domain.Enums.ProjectStatus` references anywhere — verified by Step 8's grep. Later tasks build on the post-cleanup state.

**Why `ProjectFollowUp.cs` is in scope:** it has `using SmartInvest.Domain.Enums;` at the top, but never actually uses any type from that namespace (`StatusId`/`Status` reference `SmartInvest.Domain.Entities.ProjectStatus`, a different type in a different namespace, not the enum being deleted). Once `Domain/Enums/ProjectStatus.cs` — the only file in that folder — is deleted, the `SmartInvest.Domain.Enums` namespace no longer exists anywhere in the assembly, and this dangling `using` becomes a compile error (`CS0246`). This must be fixed in this same task, not left for later, since Task 1 has to build cleanly on its own.

- [ ] **Step 1: Delete the dead files**

Run:

```bash
rm Backend/src/SmartInvest.Domain/Entities/InvestmentProject.cs
rm Backend/src/SmartInvest.Domain/Enums/ProjectStatus.cs
rm Backend/src/SmartInvest.Application/Interfaces/IInvestmentProjectService.cs
rm Backend/src/SmartInvest.Application/Services/InvestmentProjectService.cs
rm Backend/src/SmartInvest.Application/DTOs/InvestmentProjectDtos.cs
rm Backend/src/SmartInvest.Application/Common/Mappings/MappingProfile.cs
rm Backend/src/SmartInvest.API/Controllers/InvestmentProjectsController.cs
```

- [ ] **Step 2: Remove the dangling `using` in `ProjectFollowUp.cs`**

In `Backend/src/SmartInvest.Domain/Entities/ProjectFollowUp.cs`, remove the first line (`using SmartInvest.Domain.Enums;`) and the blank line after it. The file should start directly with:

```csharp
namespace SmartInvest.Domain.Entities
{
    public class ProjectFollowUp
```

(Leave everything else in the file untouched for this task — Task 2 will modify its FK/nav separately.)

- [ ] **Step 3: Remove the DI registration for the deleted service**

In `Backend/src/SmartInvest.Application/DependencyInjection.cs`, remove this line from inside `AddApplication`:

```csharp
        services.AddScoped<IInvestmentProjectService, InvestmentProjectService>();
```

Leave the rest of the file (including the now-temporarily-unused `// Application services` comment and the `using SmartInvest.Application.Services;` directive) exactly as-is — later tasks in this plan add new service registrations in the same spot.

- [ ] **Step 4: Remove the `DbSet<InvestmentProject>` property**

In `Backend/src/SmartInvest.Infrastructure/Data/AppDbContext.cs`, remove this line:

```csharp
    public DbSet<InvestmentProject> InvestmentProjects => Set<InvestmentProject>();
```

- [ ] **Step 5: Build and verify**

Run: `dotnet build Backend`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Generate the migration**

Run from `Backend/src/SmartInvest.API`:

```bash
cd Backend/src/SmartInvest.API
dotnet ef migrations add RemoveInvestmentProject --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

Expected: `Done.` A tool-version warning (`9.0.4` vs `10.0.10`) is expected and harmless. Open the generated migration's `Up()` method and confirm it contains only `migrationBuilder.DropTable(name: "InvestmentProjects");` — no other table/column changes. If it contains anything else, something in Steps 1-4 was missed or done to the wrong file; stop and investigate before continuing.

- [ ] **Step 7: Apply the migration (skip if no local SQL Server is reachable)**

Run from `Backend/src/SmartInvest.API`:

```bash
dotnet ef database update --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

Expected: `Done.` If this fails with a connection error, skip — not required to continue the plan.

- [ ] **Step 8: Verify no dangling references remain**

Run:

```bash
grep -ril "InvestmentProject" Backend/src --include=*.cs | grep -v /Migrations/
grep -ril "Domain.Enums" Backend/src --include=*.cs
```

Expected: the first command returns nothing (all non-migration references gone — migration files are expected to still mention `InvestmentProjects` since they record history, that's correct and fine). The second command returns nothing at all (no file references the now-empty `Domain.Enums` namespace).

- [ ] **Step 9: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Entities/ProjectFollowUp.cs \
        Backend/src/SmartInvest.Application/DependencyInjection.cs \
        Backend/src/SmartInvest.Infrastructure/Data/AppDbContext.cs \
        Backend/src/SmartInvest.Infrastructure/Migrations/
git add -u Backend/src/SmartInvest.Domain/Entities/InvestmentProject.cs \
        Backend/src/SmartInvest.Domain/Enums/ProjectStatus.cs \
        Backend/src/SmartInvest.Application/Interfaces/IInvestmentProjectService.cs \
        Backend/src/SmartInvest.Application/Services/InvestmentProjectService.cs \
        Backend/src/SmartInvest.Application/DTOs/InvestmentProjectDtos.cs \
        Backend/src/SmartInvest.Application/Common/Mappings/MappingProfile.cs \
        Backend/src/SmartInvest.API/Controllers/InvestmentProjectsController.cs
git commit -m "chore: remove unused InvestmentProject stack and orphaned ProjectStatus enum"
```

---

### Task 2: Domain model — SubProjectFinancialYear, Plan archive fields, PlanProject snapshot

**Files:**
- Modify: `Backend/src/SmartInvest.Domain/Entities/PlanProject.cs`
- Modify: `Backend/src/SmartInvest.Domain/Entities/Plan.cs`
- Create: `Backend/src/SmartInvest.Domain/Entities/SubProjectFinancialYear.cs`
- Modify: `Backend/src/SmartInvest.Domain/Entities/SubProject.cs`
- Modify: `Backend/src/SmartInvest.Domain/Entities/FinancialYear.cs`
- Modify: `Backend/src/SmartInvest.Domain/Entities/ProjectFollowUp.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Data/Configurations/PlanProjectConfiguration.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Data/Configurations/ProjectFollowUpConfiguration.cs`
- Create: `Backend/src/SmartInvest.Infrastructure/Data/Configurations/SubProjectFinancialYearConfiguration.cs`

**Interfaces:**
- Consumes: the cleaned-up tree from Task 1 (this task's full-file replacement of `ProjectFollowUp.cs` must NOT reintroduce `using SmartInvest.Domain.Enums;`).
- Produces: `SubProjectFinancialYear` entity with `SubProjectFinancialYearId, SubProjectId, FinancialYearId, ProjectFollowUps` (collection) — no `ApprovalStatus` field (deliberately omitted, see design spec). `Plan.SuggestionDate` (`DateTime`), `Plan.ApprovalDate` (`DateTime?`). `PlanProject` reduced to `PlanProjectId, PlanId, SubProjectId` only. `ProjectFollowUp.SubProjectFinancialYearId`/`SubProjectFinancialYear` (renamed from `PlanProjectId`/`PlanProject`). These exact names are used by Tasks 3-5.

- [ ] **Step 1: Strip `PlanProject` down to a pure snapshot join**

Replace the full contents of `Backend/src/SmartInvest.Domain/Entities/PlanProject.cs`:

```csharp
namespace SmartInvest.Domain.Entities
{
    public class PlanProject
    {
        [Key]
        public int PlanProjectId { get; set; }

        [ForeignKey("Plan")]
        public int PlanId { get; set; }
        public virtual Plan Plan { get; set; }

        [ForeignKey("SubProject")]
        public int SubProjectId { get; set; }
        public virtual SubProject SubProject { get; set; }
    }
}
```

- [ ] **Step 2: Add archive dates to `Plan`**

In `Backend/src/SmartInvest.Domain/Entities/Plan.cs`, add this block right after `public string PlanStatus { get; set; } = string.Empty;`:

```csharp

        public DateTime SuggestionDate { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovalDate { get; set; }
```

The full file should read:

```csharp
namespace SmartInvest.Domain.Entities
{
    public class Plan
    {
        [Key]
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string PlanStatus { get; set; } = string.Empty;

        public DateTime SuggestionDate { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovalDate { get; set; }

        [ForeignKey("FinancialYear")]
        public int FinancialYearId { get; set; }
        public virtual FinancialYear FinancialYear { get; set; }

        public virtual ICollection<PlanProject> PlanProjects { get; set; }
    }
}
```

- [ ] **Step 3: Add the `SubProjectFinancialYear` entity**

Create `Backend/src/SmartInvest.Domain/Entities/SubProjectFinancialYear.cs`:

```csharp
namespace SmartInvest.Domain.Entities
{
    public class SubProjectFinancialYear
    {
        [Key]
        public int SubProjectFinancialYearId { get; set; }

        [ForeignKey("SubProject")]
        public int SubProjectId { get; set; }
        public virtual SubProject SubProject { get; set; }

        [ForeignKey("FinancialYear")]
        public int FinancialYearId { get; set; }
        public virtual FinancialYear FinancialYear { get; set; }

        public virtual ICollection<ProjectFollowUp> ProjectFollowUps { get; set; }
    }
}
```

- [ ] **Step 4: Add the inverse navigation on `SubProject`**

In `Backend/src/SmartInvest.Domain/Entities/SubProject.cs`, add this line right after `public virtual ICollection<PlanProject> PlanProjects { get; set; }`:

```csharp
        public virtual ICollection<SubProjectFinancialYear> FinancialYears { get; set; }
```

- [ ] **Step 5: Add the inverse navigation on `FinancialYear`**

In `Backend/src/SmartInvest.Domain/Entities/FinancialYear.cs`, add this line right after `public virtual ICollection<Plan> Plans { get; set; }`:

```csharp
        public virtual ICollection<SubProjectFinancialYear> SubProjectFinancialYears { get; set; }
```

- [ ] **Step 6: Retarget `ProjectFollowUp`'s FK from `PlanProject` to `SubProjectFinancialYear`**

Replace the full contents of `Backend/src/SmartInvest.Domain/Entities/ProjectFollowUp.cs`:

```csharp
namespace SmartInvest.Domain.Entities
{
    public class ProjectFollowUp
    {
        [Key]
        public int FollowUpId { get; set; }

        [ForeignKey("SubProjectFinancialYear")]
        public int SubProjectFinancialYearId { get; set; }
        public virtual SubProjectFinancialYear SubProjectFinancialYear { get; set; }
        [ForeignKey("Status")]
        public int StatusId { get; set; }
        public virtual ProjectStatus Status { get; set; }
        [ForeignKey("DelayReason")]
        public int? DelayReasonId { get; set; } // Nullable based on ERD
        public virtual DelayReason DelayReason { get; set; }

        public decimal ProgressPercentage { get; set; }
        public DateTime FollowUpDate { get; set; }
        public string? Notes { get; set; }

        public virtual ICollection<ProjectAttachment> Attachments { get; set; }
    }
}
```

- [ ] **Step 7: Configure `PlanProject` relations (add `Plan` side + uniqueness)**

Replace the full contents of `Backend/src/SmartInvest.Infrastructure/Data/Configurations/PlanProjectConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class PlanProjectConfiguration : IEntityTypeConfiguration<PlanProject>
{
    public void Configure(EntityTypeBuilder<PlanProject> builder)
    {
        // نفس المشروع الفرعي مايتكررش في نفس الخطة
        builder.HasIndex(x => new { x.PlanId, x.SubProjectId }).IsUnique();

        builder.HasOne(x => x.Plan)
               .WithMany(p => p.PlanProjects)
               .HasForeignKey(x => x.PlanId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubProject)
               .WithMany(s => s.PlanProjects)
               .HasForeignKey(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 8: Configure `SubProjectFinancialYear` relations + uniqueness**

Create `Backend/src/SmartInvest.Infrastructure/Data/Configurations/SubProjectFinancialYearConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class SubProjectFinancialYearConfiguration : IEntityTypeConfiguration<SubProjectFinancialYear>
{
    public void Configure(EntityTypeBuilder<SubProjectFinancialYear> builder)
    {
        // نفس المشروع الفرعي مايتربطش بنفس السنة المالية مرتين
        builder.HasIndex(x => new { x.SubProjectId, x.FinancialYearId }).IsUnique();

        builder.HasOne(x => x.SubProject)
               .WithMany(s => s.FinancialYears)
               .HasForeignKey(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FinancialYear)
               .WithMany(f => f.SubProjectFinancialYears)
               .HasForeignKey(x => x.FinancialYearId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 9: Configure `ProjectFollowUp`'s new relation**

In `Backend/src/SmartInvest.Infrastructure/Data/Configurations/ProjectFollowUpConfiguration.cs`, add this block right before the closing `}` of the `Configure` method (after the `Status` block):

```csharp

        builder.HasOne(x => x.SubProjectFinancialYear)
               .WithMany(s => s.ProjectFollowUps)
               .HasForeignKey(x => x.SubProjectFinancialYearId)
               .OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 10: Build and verify**

Run: `dotnet build Backend`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 11: Generate the migration**

Run from `Backend/src/SmartInvest.API`:

```bash
dotnet ef migrations add AddSubProjectFinancialYearTracking --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

Expected: `Done.` Open the generated migration's `Up()` method and confirm it: drops the `ApprovalStatus` column from `PlanProjects`; creates the `SubProjectFinancialYears` table; adds `SuggestionDate` (non-nullable) and `ApprovalDate` (nullable) columns to `Plans`; renames/re-FKs `ProjectFollowUps.PlanProjectId` to `SubProjectFinancialYearId` (EF Core typically expresses this as a drop-old-FK-and-column + add-new-FK-and-column pair rather than a true rename — either form is correct here, both achieve the same end schema).

- [ ] **Step 12: Build and verify again**

Run: `dotnet build Backend`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 13: Apply the migration (skip if no local SQL Server is reachable)**

Run from `Backend/src/SmartInvest.API`:

```bash
dotnet ef database update --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

Expected: `Done.` If this fails with a connection error, skip.

- [ ] **Step 14: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Entities/PlanProject.cs \
        Backend/src/SmartInvest.Domain/Entities/Plan.cs \
        Backend/src/SmartInvest.Domain/Entities/SubProjectFinancialYear.cs \
        Backend/src/SmartInvest.Domain/Entities/SubProject.cs \
        Backend/src/SmartInvest.Domain/Entities/FinancialYear.cs \
        Backend/src/SmartInvest.Domain/Entities/ProjectFollowUp.cs \
        Backend/src/SmartInvest.Infrastructure/Data/Configurations/PlanProjectConfiguration.cs \
        Backend/src/SmartInvest.Infrastructure/Data/Configurations/ProjectFollowUpConfiguration.cs \
        Backend/src/SmartInvest.Infrastructure/Data/Configurations/SubProjectFinancialYearConfiguration.cs \
        Backend/src/SmartInvest.Infrastructure/Migrations/
git commit -m "feat: add SubProjectFinancialYear tracking, shrink PlanProject to archive snapshot"
```

---

### Task 3: FinancialYear CRUD

**Files:**
- Modify: `Backend/src/SmartInvest.Domain/Common/Roles.cs`
- Create: `Backend/src/SmartInvest.Application/DTOs/FinancialYearDtos.cs`
- Create: `Backend/src/SmartInvest.Application/Validators/CreateFinancialYearDtoValidator.cs`
- Create: `Backend/src/SmartInvest.Application/Common/Mappings/FinancialYearMappingProfile.cs`
- Create: `Backend/src/SmartInvest.Application/Interfaces/IFinancialYearService.cs`
- Create: `Backend/src/SmartInvest.Application/Services/FinancialYearService.cs`
- Create: `Backend/src/SmartInvest.API/Controllers/FinancialYearsController.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `FinancialYear` entity (`FinancialYearId, Name, StartDate, EndDate, IsClosed`) — pre-existing, unchanged. `IGenericRepository<T>`/`IUnitOfWork` (pre-existing).
- Produces: `Roles.PlanningStaff` (`"PlanningEmployee,PlanningManager"`) — used by Tasks 4 and 5 too, add it once here. `IFinancialYearService` (`GetAllAsync/GetByIdAsync/CreateAsync/UpdateAsync/DeleteAsync`). `FinancialYearDto.Id` (`int`) — the FK value Tasks 4/5 use as `financialYearId`.

- [ ] **Step 1: Add the `PlanningStaff` composite role constant**

In `Backend/src/SmartInvest.Domain/Common/Roles.cs`, replace the full file contents:

```csharp
namespace SmartInvest.Domain.Common;

public static class Roles
{
    public const string PlanningEmployee = "PlanningEmployee";

    public const string PlanningManager = "PlanningManager";

    /// <summary>مدير + موظف تخطيط.</summary>
    public const string PlanningStaff = "PlanningEmployee,PlanningManager";
}
```

- [ ] **Step 2: DTOs**

Create `Backend/src/SmartInvest.Application/DTOs/FinancialYearDtos.cs`:

```csharp
namespace SmartInvest.Application.DTOs;

public class FinancialYearDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
}

public class CreateFinancialYearDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class UpdateFinancialYearDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
}
```

- [ ] **Step 3: Validators**

Create `Backend/src/SmartInvest.Application/Validators/CreateFinancialYearDtoValidator.cs`:

```csharp
using FluentValidation;
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Validators;

public class CreateFinancialYearDtoValidator : AbstractValidator<CreateFinancialYearDto>
{
    public CreateFinancialYearDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("اسم السنة المالية مطلوب").MaximumLength(50);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية");
    }
}

public class UpdateFinancialYearDtoValidator : AbstractValidator<UpdateFinancialYearDto>
{
    public UpdateFinancialYearDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("اسم السنة المالية مطلوب").MaximumLength(50);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية");
    }
}
```

- [ ] **Step 4: Mapping profile**

Create `Backend/src/SmartInvest.Application/Common/Mappings/FinancialYearMappingProfile.cs`:

```csharp
using AutoMapper;
using SmartInvest.Application.DTOs;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Application.Common.Mappings;

public class FinancialYearMappingProfile : Profile
{
    public FinancialYearMappingProfile()
    {
        CreateMap<FinancialYear, FinancialYearDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.FinancialYearId));
    }
}
```

- [ ] **Step 5: Service interface**

Create `Backend/src/SmartInvest.Application/Interfaces/IFinancialYearService.cs`:

```csharp
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IFinancialYearService
{
    Task<IReadOnlyList<FinancialYearDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<FinancialYearDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<FinancialYearDto> CreateAsync(CreateFinancialYearDto dto, CancellationToken cancellationToken = default);

    Task<FinancialYearDto> UpdateAsync(int id, UpdateFinancialYearDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 6: Service implementation**

Create `Backend/src/SmartInvest.Application/Services/FinancialYearService.cs`:

```csharp
using AutoMapper;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services;

public class FinancialYearService : IFinancialYearService
{
    private readonly IGenericRepository<FinancialYear> _financialYearRepository;
    private readonly IGenericRepository<SubProjectFinancialYear> _subProjectFinancialYearRepository;
    private readonly IGenericRepository<Plan> _planRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public FinancialYearService(
        IGenericRepository<FinancialYear> financialYearRepository,
        IGenericRepository<SubProjectFinancialYear> subProjectFinancialYearRepository,
        IGenericRepository<Plan> planRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _financialYearRepository = financialYearRepository;
        _subProjectFinancialYearRepository = subProjectFinancialYearRepository;
        _planRepository = planRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<FinancialYearDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var years = await _financialYearRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<FinancialYearDto>>(years);
    }

    public async Task<FinancialYearDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var year = await GetOrThrowAsync(id, cancellationToken);
        return _mapper.Map<FinancialYearDto>(year);
    }

    public async Task<FinancialYearDto> CreateAsync(CreateFinancialYearDto dto, CancellationToken cancellationToken = default)
    {
        var year = new FinancialYear
        {
            Name = dto.Name,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsClosed = false,
        };

        await _financialYearRepository.AddAsync(year, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<FinancialYearDto>(year);
    }

    public async Task<FinancialYearDto> UpdateAsync(int id, UpdateFinancialYearDto dto, CancellationToken cancellationToken = default)
    {
        var year = await GetOrThrowAsync(id, cancellationToken);

        year.Name = dto.Name;
        year.StartDate = dto.StartDate;
        year.EndDate = dto.EndDate;
        year.IsClosed = dto.IsClosed;

        _financialYearRepository.Update(year);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<FinancialYearDto>(year);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var year = await GetOrThrowAsync(id, cancellationToken);

        var linkedSubProjects = await _subProjectFinancialYearRepository.FindAsync(x => x.FinancialYearId == id, cancellationToken);
        if (linkedSubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف السنة المالية لوجود مشروعات فرعية مرتبطة بها");
        }

        var linkedPlans = await _planRepository.FindAsync(x => x.FinancialYearId == id, cancellationToken);
        if (linkedPlans.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف السنة المالية لوجود خطط مرتبطة بها");
        }

        _financialYearRepository.Remove(year);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<FinancialYear> GetOrThrowAsync(int id, CancellationToken cancellationToken)
    {
        var year = await _financialYearRepository.GetByIdAsync(id, cancellationToken);
        if (year == null)
        {
            throw new NotFoundException($"السنة المالية رقم {id} غير موجودة");
        }

        return year;
    }
}
```

- [ ] **Step 7: Controller**

Create `Backend/src/SmartInvest.API/Controllers/FinancialYearsController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/financial-years")]
[Authorize]
public class FinancialYearsController : ControllerBase
{
    private readonly IFinancialYearService _financialYearService;

    public FinancialYearsController(IFinancialYearService financialYearService)
    {
        _financialYearService = financialYearService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FinancialYearDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _financialYearService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FinancialYearDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _financialYearService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<FinancialYearDto>> Create(CreateFinancialYearDto dto, CancellationToken cancellationToken)
    {
        var result = await _financialYearService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<FinancialYearDto>> Update(int id, UpdateFinancialYearDto dto, CancellationToken cancellationToken)
    {
        var result = await _financialYearService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _financialYearService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
```

Note: the class-level attribute is bare `[Authorize]` (any authenticated role) with `Create`/`Update`/`Delete` individually narrowed — this is the safe pattern from Global Constraints, never widen at the method level.

- [ ] **Step 8: Register in DI**

In `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`, add this line right after `services.AddScoped<IIdentityService, IdentityService>();`:

```csharp
        services.AddScoped<IFinancialYearService, FinancialYearService>();
```

- [ ] **Step 9: Build and verify**

Run: `dotnet build Backend`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 10: Manual verification**

Start the API (`dotnet run --project Backend/src/SmartInvest.API`). Log in as admin (`admin@gmail.com` / `Admin@123`). `POST /api/financial-years` with `{"name":"2026/2027","startDate":"2026-07-01","endDate":"2027-06-30"}` → expect 201 with `id`. `GET /api/financial-years` → listed. Stop the API process when done.

- [ ] **Step 11: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Common/Roles.cs \
        Backend/src/SmartInvest.Application/DTOs/FinancialYearDtos.cs \
        Backend/src/SmartInvest.Application/Validators/CreateFinancialYearDtoValidator.cs \
        Backend/src/SmartInvest.Application/Common/Mappings/FinancialYearMappingProfile.cs \
        Backend/src/SmartInvest.Application/Interfaces/IFinancialYearService.cs \
        Backend/src/SmartInvest.Application/Services/FinancialYearService.cs \
        Backend/src/SmartInvest.API/Controllers/FinancialYearsController.cs \
        Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs
git commit -m "feat: add FinancialYear CRUD"
```

---

### Task 4: SubProject ↔ FinancialYear link management

**Files:**
- Create: `Backend/src/SmartInvest.Application/DTOs/SubProjectFinancialYearDtos.cs`
- Create: `Backend/src/SmartInvest.Application/Interfaces/ISubProjectFinancialYearService.cs`
- Create: `Backend/src/SmartInvest.Application/Services/SubProjectFinancialYearService.cs`
- Create: `Backend/src/SmartInvest.API/Controllers/SubProjectFinancialYearsController.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `Roles.PlanningStaff`/`Roles.PlanningManager` (Task 3); `SubProjectFinancialYear` entity (Task 2); `IGenericRepository<T>` (pre-existing).
- Produces: `ISubProjectFinancialYearService` (`GetForSubProjectAsync/LinkAsync/UnlinkAsync`) — used only by this task's controller; not consumed elsewhere in this plan.

- [ ] **Step 1: DTOs**

Create `Backend/src/SmartInvest.Application/DTOs/SubProjectFinancialYearDtos.cs`:

```csharp
namespace SmartInvest.Application.DTOs;

public class SubProjectFinancialYearDto
{
    public int Id { get; set; }
    public int FinancialYearId { get; set; }
    public string FinancialYearName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
}

public class LinkFinancialYearDto
{
    public int FinancialYearId { get; set; }
}
```

- [ ] **Step 2: Service interface**

Create `Backend/src/SmartInvest.Application/Interfaces/ISubProjectFinancialYearService.cs`:

```csharp
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface ISubProjectFinancialYearService
{
    Task<IReadOnlyList<SubProjectFinancialYearDto>> GetForSubProjectAsync(int subProjectId, CancellationToken cancellationToken = default);

    Task<SubProjectFinancialYearDto> LinkAsync(int subProjectId, int financialYearId, CancellationToken cancellationToken = default);

    Task UnlinkAsync(int subProjectId, int financialYearId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Service implementation**

Create `Backend/src/SmartInvest.Application/Services/SubProjectFinancialYearService.cs`:

```csharp
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services;

public class SubProjectFinancialYearService : ISubProjectFinancialYearService
{
    private readonly IGenericRepository<SubProjectFinancialYear> _linkRepository;
    private readonly IGenericRepository<SubProject> _subProjectRepository;
    private readonly IGenericRepository<FinancialYear> _financialYearRepository;
    private readonly IGenericRepository<ProjectFollowUp> _followUpRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SubProjectFinancialYearService(
        IGenericRepository<SubProjectFinancialYear> linkRepository,
        IGenericRepository<SubProject> subProjectRepository,
        IGenericRepository<FinancialYear> financialYearRepository,
        IGenericRepository<ProjectFollowUp> followUpRepository,
        IUnitOfWork unitOfWork)
    {
        _linkRepository = linkRepository;
        _subProjectRepository = subProjectRepository;
        _financialYearRepository = financialYearRepository;
        _followUpRepository = followUpRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<SubProjectFinancialYearDto>> GetForSubProjectAsync(int subProjectId, CancellationToken cancellationToken = default)
    {
        var links = await _linkRepository.FindAsync(x => x.SubProjectId == subProjectId, cancellationToken);
        var result = new List<SubProjectFinancialYearDto>();

        foreach (var link in links)
        {
            var year = await _financialYearRepository.GetByIdAsync(link.FinancialYearId, cancellationToken);
            if (year == null)
            {
                continue;
            }

            result.Add(new SubProjectFinancialYearDto
            {
                Id = link.SubProjectFinancialYearId,
                FinancialYearId = year.FinancialYearId,
                FinancialYearName = year.Name,
                StartDate = year.StartDate,
                EndDate = year.EndDate,
                IsClosed = year.IsClosed,
            });
        }

        return result;
    }

    public async Task<SubProjectFinancialYearDto> LinkAsync(int subProjectId, int financialYearId, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetByIdAsync(subProjectId, cancellationToken);
        if (subProject == null)
        {
            throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");
        }

        var year = await _financialYearRepository.GetByIdAsync(financialYearId, cancellationToken);
        if (year == null)
        {
            throw new NotFoundException($"السنة المالية رقم {financialYearId} غير موجودة");
        }

        var existing = await _linkRepository.FindAsync(
            x => x.SubProjectId == subProjectId && x.FinancialYearId == financialYearId, cancellationToken);
        if (existing.Count > 0)
        {
            throw new BusinessRuleException("المشروع الفرعي مرتبط بالفعل بهذه السنة المالية");
        }

        var link = new SubProjectFinancialYear
        {
            SubProjectId = subProjectId,
            FinancialYearId = financialYearId,
        };

        await _linkRepository.AddAsync(link, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SubProjectFinancialYearDto
        {
            Id = link.SubProjectFinancialYearId,
            FinancialYearId = year.FinancialYearId,
            FinancialYearName = year.Name,
            StartDate = year.StartDate,
            EndDate = year.EndDate,
            IsClosed = year.IsClosed,
        };
    }

    public async Task UnlinkAsync(int subProjectId, int financialYearId, CancellationToken cancellationToken = default)
    {
        var link = (await _linkRepository.FindAsync(
            x => x.SubProjectId == subProjectId && x.FinancialYearId == financialYearId, cancellationToken))
            .FirstOrDefault();

        if (link == null)
        {
            throw new NotFoundException("لا يوجد ربط بين هذا المشروع الفرعي وهذه السنة المالية");
        }

        var followUps = await _followUpRepository.FindAsync(
            x => x.SubProjectFinancialYearId == link.SubProjectFinancialYearId, cancellationToken);
        if (followUps.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن فك الربط لوجود بيانات متابعة مسجلة لهذه السنة");
        }

        _linkRepository.Remove(link);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Controller**

Create `Backend/src/SmartInvest.API/Controllers/SubProjectFinancialYearsController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/subprojects/{subProjectId:int}/financial-years")]
[Authorize]
public class SubProjectFinancialYearsController : ControllerBase
{
    private readonly ISubProjectFinancialYearService _linkService;

    public SubProjectFinancialYearsController(ISubProjectFinancialYearService linkService)
    {
        _linkService = linkService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubProjectFinancialYearDto>>> GetAll(int subProjectId, CancellationToken cancellationToken)
    {
        var result = await _linkService.GetForSubProjectAsync(subProjectId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<SubProjectFinancialYearDto>> Link(int subProjectId, LinkFinancialYearDto dto, CancellationToken cancellationToken)
    {
        var result = await _linkService.LinkAsync(subProjectId, dto.FinancialYearId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{financialYearId:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> Unlink(int subProjectId, int financialYearId, CancellationToken cancellationToken)
    {
        await _linkService.UnlinkAsync(subProjectId, financialYearId, cancellationToken);
        return NoContent();
    }
}
```

- [ ] **Step 5: Register in DI**

In `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`, add right after the `IFinancialYearService` line from Task 3:

```csharp
        services.AddScoped<ISubProjectFinancialYearService, SubProjectFinancialYearService>();
```

- [ ] **Step 6: Build and verify**

Run: `dotnet build Backend`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Manual verification**

As admin: pick an existing sub-project id (`GET /api/subprojects`) and the financial year id from Task 3. `POST /api/subprojects/{subProjectId}/financial-years` with `{"financialYearId": 1}` → expect 200. `GET /api/subprojects/{subProjectId}/financial-years` → listed with `financialYearName`. Repeat the same `POST` → expect 400 with the "already linked" Arabic message. `DELETE /api/subprojects/{subProjectId}/financial-years/1` → expect 204. Stop the API process when done.

- [ ] **Step 8: Commit**

```bash
git add Backend/src/SmartInvest.Application/DTOs/SubProjectFinancialYearDtos.cs \
        Backend/src/SmartInvest.Application/Interfaces/ISubProjectFinancialYearService.cs \
        Backend/src/SmartInvest.Application/Services/SubProjectFinancialYearService.cs \
        Backend/src/SmartInvest.API/Controllers/SubProjectFinancialYearsController.cs \
        Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs
git commit -m "feat: add sub-project to financial-year link management"
```

---

### Task 5: Plan CRUD (archive: suggested projects, suggestion/approval dates, printable detail)

**Files:**
- Create: `Backend/src/SmartInvest.Application/DTOs/PlanDtos.cs`
- Create: `Backend/src/SmartInvest.Application/Validators/CreatePlanDtoValidator.cs`
- Create: `Backend/src/SmartInvest.Application/Interfaces/IPlanService.cs`
- Create: `Backend/src/SmartInvest.Application/Services/PlanService.cs`
- Create: `Backend/src/SmartInvest.API/Controllers/PlansController.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `Roles.PlanningStaff`/`Roles.PlanningManager` (Task 3); `Plan`/`PlanProject` entities (Task 2); `IGenericRepository<T>` (pre-existing).
- Produces: `IPlanService` (`GetAllAsync/GetByIdAsync/CreateAsync/UpdateAsync/DeleteAsync/AddSuggestedProjectAsync/RemoveSuggestedProjectAsync/ApproveAsync`) — terminal task, nothing later in this plan consumes it.

**No mapping profile in this task, deliberately:** `PlanService` never calls `IMapper.Map(...)`. Both `Plan → PlanDto` and `Plan → PlanDetailDto` need `FinancialYear.Name`, and `PlanDetailDto` additionally needs each suggested `SubProject`'s `MainProject.MainProjectName` — none of these navigations are eager-loaded by `IGenericRepository<T>.GetByIdAsync` (`DbSet.FindAsync`), so an AutoMapper profile that flattens through them would risk a null reference the first time the cache doesn't already have the related row loaded. The service fetches each related entity explicitly through its own repository instead and builds the DTOs by hand — see Step 4.

- [ ] **Step 1: DTOs**

Create `Backend/src/SmartInvest.Application/DTOs/PlanDtos.cs`:

```csharp
namespace SmartInvest.Application.DTOs;

public class PlanDto
{
    public int Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanStatus { get; set; } = string.Empty;
    public DateTime SuggestionDate { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public int FinancialYearId { get; set; }
    public string FinancialYearName { get; set; } = string.Empty;
}

public class PlanSuggestedProjectDto
{
    public int SubProjectId { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
    public string? SubProjectCode { get; set; }
    public string MainProjectName { get; set; } = string.Empty;
    public decimal BankFunding { get; set; }
    public decimal SelfFunding { get; set; }
    public decimal TotalCost { get; set; }
}

public class PlanDetailDto
{
    public int Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanStatus { get; set; } = string.Empty;
    public DateTime SuggestionDate { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public int FinancialYearId { get; set; }
    public string FinancialYearName { get; set; } = string.Empty;
    public IReadOnlyList<PlanSuggestedProjectDto> SuggestedProjects { get; set; } = new List<PlanSuggestedProjectDto>();
}

public class CreatePlanDto
{
    public string PlanName { get; set; } = string.Empty;
    public int FinancialYearId { get; set; }
}

public class UpdatePlanDto
{
    public string PlanName { get; set; } = string.Empty;
}

public class AddSuggestedProjectDto
{
    public int SubProjectId { get; set; }
}
```

- [ ] **Step 2: Validator**

Create `Backend/src/SmartInvest.Application/Validators/CreatePlanDtoValidator.cs`:

```csharp
using FluentValidation;
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Validators;

public class CreatePlanDtoValidator : AbstractValidator<CreatePlanDto>
{
    public CreatePlanDtoValidator()
    {
        RuleFor(x => x.PlanName).NotEmpty().WithMessage("اسم الخطة مطلوب").MaximumLength(200);
        RuleFor(x => x.FinancialYearId).GreaterThan(0).WithMessage("يجب اختيار السنة المالية");
    }
}

public class UpdatePlanDtoValidator : AbstractValidator<UpdatePlanDto>
{
    public UpdatePlanDtoValidator()
    {
        RuleFor(x => x.PlanName).NotEmpty().WithMessage("اسم الخطة مطلوب").MaximumLength(200);
    }
}
```

- [ ] **Step 3: Service interface**

Create `Backend/src/SmartInvest.Application/Interfaces/IPlanService.cs`:

```csharp
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IPlanService
{
    Task<IReadOnlyList<PlanDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PlanDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<PlanDto> CreateAsync(CreatePlanDto dto, CancellationToken cancellationToken = default);

    Task<PlanDto> UpdateAsync(int id, UpdatePlanDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<PlanDetailDto> AddSuggestedProjectAsync(int planId, int subProjectId, CancellationToken cancellationToken = default);

    Task RemoveSuggestedProjectAsync(int planId, int subProjectId, CancellationToken cancellationToken = default);

    Task<PlanDto> ApproveAsync(int planId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Service implementation**

Create `Backend/src/SmartInvest.Application/Services/PlanService.cs`:

```csharp
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services;

public class PlanService : IPlanService
{
    private readonly IGenericRepository<Plan> _planRepository;
    private readonly IGenericRepository<PlanProject> _planProjectRepository;
    private readonly IGenericRepository<FinancialYear> _financialYearRepository;
    private readonly IGenericRepository<SubProject> _subProjectRepository;
    private readonly IGenericRepository<MainProject> _mainProjectRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PlanService(
        IGenericRepository<Plan> planRepository,
        IGenericRepository<PlanProject> planProjectRepository,
        IGenericRepository<FinancialYear> financialYearRepository,
        IGenericRepository<SubProject> subProjectRepository,
        IGenericRepository<MainProject> mainProjectRepository,
        IUnitOfWork unitOfWork)
    {
        _planRepository = planRepository;
        _planProjectRepository = planProjectRepository;
        _financialYearRepository = financialYearRepository;
        _subProjectRepository = subProjectRepository;
        _mainProjectRepository = mainProjectRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<PlanDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var plans = await _planRepository.FindAsync(_ => true, cancellationToken);
        var result = new List<PlanDto>();

        foreach (var plan in plans)
        {
            result.Add(await MapPlanAsync(plan, cancellationToken));
        }

        return result;
    }

    public async Task<PlanDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var plan = await GetOrThrowAsync(id, cancellationToken);
        return await MapPlanDetailAsync(plan, cancellationToken);
    }

    public async Task<PlanDto> CreateAsync(CreatePlanDto dto, CancellationToken cancellationToken = default)
    {
        var year = await _financialYearRepository.GetByIdAsync(dto.FinancialYearId, cancellationToken);
        if (year == null)
        {
            throw new NotFoundException("السنة المالية المحددة غير موجودة");
        }

        var plan = new Plan
        {
            PlanName = dto.PlanName,
            PlanStatus = "مسودة",
            SuggestionDate = DateTime.UtcNow,
            FinancialYearId = dto.FinancialYearId,
        };

        await _planRepository.AddAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapPlanAsync(plan, cancellationToken);
    }

    public async Task<PlanDto> UpdateAsync(int id, UpdatePlanDto dto, CancellationToken cancellationToken = default)
    {
        var plan = await GetOrThrowAsync(id, cancellationToken);

        plan.PlanName = dto.PlanName;

        _planRepository.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapPlanAsync(plan, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var plan = await GetOrThrowAsync(id, cancellationToken);

        var suggestedProjects = await _planProjectRepository.FindAsync(x => x.PlanId == id, cancellationToken);
        foreach (var suggested in suggestedProjects)
        {
            _planProjectRepository.Remove(suggested);
        }

        _planRepository.Remove(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlanDetailDto> AddSuggestedProjectAsync(int planId, int subProjectId, CancellationToken cancellationToken = default)
    {
        var plan = await GetOrThrowAsync(planId, cancellationToken);

        var subProject = await _subProjectRepository.GetByIdAsync(subProjectId, cancellationToken);
        if (subProject == null)
        {
            throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");
        }

        var existing = await _planProjectRepository.FindAsync(
            x => x.PlanId == planId && x.SubProjectId == subProjectId, cancellationToken);
        if (existing.Count > 0)
        {
            throw new BusinessRuleException("المشروع الفرعي مضاف بالفعل لقائمة المشروعات المقترحة في هذه الخطة");
        }

        await _planProjectRepository.AddAsync(new PlanProject { PlanId = planId, SubProjectId = subProjectId }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapPlanDetailAsync(plan, cancellationToken);
    }

    public async Task RemoveSuggestedProjectAsync(int planId, int subProjectId, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(planId, cancellationToken);

        var link = (await _planProjectRepository.FindAsync(
            x => x.PlanId == planId && x.SubProjectId == subProjectId, cancellationToken))
            .FirstOrDefault();

        if (link == null)
        {
            throw new NotFoundException("المشروع الفرعي غير موجود في قائمة المقترحات لهذه الخطة");
        }

        _planProjectRepository.Remove(link);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlanDto> ApproveAsync(int planId, CancellationToken cancellationToken = default)
    {
        var plan = await GetOrThrowAsync(planId, cancellationToken);

        if (plan.ApprovalDate.HasValue)
        {
            throw new BusinessRuleException("تم اعتماد هذه الخطة بالفعل");
        }

        plan.ApprovalDate = DateTime.UtcNow;
        plan.PlanStatus = "معتمدة";

        _planRepository.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapPlanAsync(plan, cancellationToken);
    }

    private async Task<Plan> GetOrThrowAsync(int id, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetByIdAsync(id, cancellationToken);
        if (plan == null)
        {
            throw new NotFoundException($"الخطة رقم {id} غير موجودة");
        }

        return plan;
    }

    private async Task<PlanDto> MapPlanAsync(Plan plan, CancellationToken cancellationToken)
    {
        var year = await _financialYearRepository.GetByIdAsync(plan.FinancialYearId, cancellationToken);
        return new PlanDto
        {
            Id = plan.PlanId,
            PlanName = plan.PlanName,
            PlanStatus = plan.PlanStatus,
            SuggestionDate = plan.SuggestionDate,
            ApprovalDate = plan.ApprovalDate,
            FinancialYearId = plan.FinancialYearId,
            FinancialYearName = year?.Name ?? string.Empty,
        };
    }

    private async Task<PlanDetailDto> MapPlanDetailAsync(Plan plan, CancellationToken cancellationToken)
    {
        var year = await _financialYearRepository.GetByIdAsync(plan.FinancialYearId, cancellationToken);
        var links = await _planProjectRepository.FindAsync(x => x.PlanId == plan.PlanId, cancellationToken);

        var suggested = new List<PlanSuggestedProjectDto>();
        foreach (var link in links)
        {
            var subProject = await _subProjectRepository.GetByIdAsync(link.SubProjectId, cancellationToken);
            if (subProject == null)
            {
                continue;
            }

            var mainProject = await _mainProjectRepository.GetByIdAsync(subProject.MainProjectId, cancellationToken);

            suggested.Add(new PlanSuggestedProjectDto
            {
                SubProjectId = subProject.SubProjectId,
                SubProjectName = subProject.SubProjectName,
                SubProjectCode = subProject.SubProjectCode,
                MainProjectName = mainProject?.MainProjectName ?? string.Empty,
                BankFunding = subProject.BankFunding,
                SelfFunding = subProject.SelfFunding,
                TotalCost = subProject.TotalCost,
            });
        }

        return new PlanDetailDto
        {
            Id = plan.PlanId,
            PlanName = plan.PlanName,
            PlanStatus = plan.PlanStatus,
            SuggestionDate = plan.SuggestionDate,
            ApprovalDate = plan.ApprovalDate,
            FinancialYearId = plan.FinancialYearId,
            FinancialYearName = year?.Name ?? string.Empty,
            SuggestedProjects = suggested,
        };
    }
}
```

Note: `PlanSuggestedProjectDto.MainProjectName` is fetched through a separate `IGenericRepository<MainProject>` lookup rather than via `SubProject.MainProject` navigation or AutoMapper, because `IGenericRepository<SubProject>.GetByIdAsync` uses `DbSet.FindAsync`, which does not eager-load navigation properties — relying on `subProject.MainProject` here would risk a null reference. `MainProject` is a pre-existing entity; `IGenericRepository<MainProject>` resolves automatically via the same open-generic DI registration every other `IGenericRepository<T>` in this codebase uses, no new repository type needed. `PlanService` has no `IMapper` dependency at all — both `MapPlanAsync` and `MapPlanDetailAsync` build their DTOs by hand.

- [ ] **Step 5: Controller**

Create `Backend/src/SmartInvest.API/Controllers/PlansController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/plans")]
[Authorize]
public class PlansController : ControllerBase
{
    private readonly IPlanService _planService;

    public PlansController(IPlanService planService)
    {
        _planService = planService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlanDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _planService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PlanDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _planService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<PlanDto>> Create(CreatePlanDto dto, CancellationToken cancellationToken)
    {
        var result = await _planService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<PlanDto>> Update(int id, UpdatePlanDto dto, CancellationToken cancellationToken)
    {
        var result = await _planService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _planService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/suggested-projects")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<PlanDetailDto>> AddSuggestedProject(int id, AddSuggestedProjectDto dto, CancellationToken cancellationToken)
    {
        var result = await _planService.AddSuggestedProjectAsync(id, dto.SubProjectId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}/suggested-projects/{subProjectId:int}")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> RemoveSuggestedProject(int id, int subProjectId, CancellationToken cancellationToken)
    {
        await _planService.RemoveSuggestedProjectAsync(id, subProjectId, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:int}/approve")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<PlanDto>> Approve(int id, CancellationToken cancellationToken)
    {
        var result = await _planService.ApproveAsync(id, cancellationToken);
        return Ok(result);
    }
}
```

- [ ] **Step 6: Register in DI**

In `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`, add right after the `ISubProjectFinancialYearService` line from Task 4:

```csharp
        services.AddScoped<IPlanService, PlanService>();
```

- [ ] **Step 7: Build and verify**

Run: `dotnet build Backend`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Manual verification**

As admin: `POST /api/plans` with `{"planName":"خطة 2026/2027","financialYearId":1}` → expect 201, `planStatus:"مسودة"`, `suggestionDate` set, `approvalDate:null`. `POST /api/plans/{id}/suggested-projects` with `{"subProjectId":1}` (use an existing sub-project id) → expect 200 with the sub-project now in `suggestedProjects`. `GET /api/plans/{id}` → confirm full detail including `mainProjectName`/`bankFunding`/`totalCost` for the suggested project. `PUT /api/plans/{id}/approve` → expect 200, `planStatus:"معتمدة"`, `approvalDate` set. Repeat `PUT /api/plans/{id}/approve` → expect 400 "تم اعتماد هذه الخطة بالفعل". Stop the API process when done.

- [ ] **Step 9: Commit**

```bash
git add Backend/src/SmartInvest.Application/DTOs/PlanDtos.cs \
        Backend/src/SmartInvest.Application/Validators/CreatePlanDtoValidator.cs \
        Backend/src/SmartInvest.Application/Interfaces/IPlanService.cs \
        Backend/src/SmartInvest.Application/Services/PlanService.cs \
        Backend/src/SmartInvest.API/Controllers/PlansController.cs \
        Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs
git commit -m "feat: add Plan CRUD (archive: suggested projects, approval, printable detail)"
```

---

## Self-Review Notes

**Spec coverage:**
- سبب فقط المشروع الفرعي بيتربط بالسنة المالية → Task 2 (`SubProjectFinancialYear` links `SubProject`, not `MainProject`).
- علاقة M:N (مشروع فرعي لأكتر من سنة) → Task 2 (join entity, no cardinality cap) + Task 4 (link/unlink endpoints allowing multiple links per sub-project).
- الخطة تبقى أرشيف (اسم، تاريخ اقتراح، تاريخ اعتماد، حالة) → Task 2 (`SuggestionDate`/`ApprovalDate` fields) + Task 5 (`CreateAsync` sets suggestion date, `ApproveAsync` sets approval date).
- قائمة "لقطة" مجمّدة من المشروعات المقترحة → Task 2 (`PlanProject` stripped to pure join) + Task 5 (`AddSuggestedProjectAsync`/`RemoveSuggestedProjectAsync`, independent of live `SubProjectFinancialYear` state).
- إزالة `ApprovalStatus` من الربط التشغيلي → Task 2 (`SubProjectFinancialYear` has no such field) + Task 2 (`PlanProject` also has it removed).
- حذف `InvestmentProject` + الـ Enum الميت → Task 1.
- `ProjectFollowUp` CRUD مؤجل → لم يُبنَ أي controller/service له في هذه الخطة، فقط أعيد توجيه الـ FK بتاعه (Task 2) — متوافق مع "خارج النطاق".
- توليد PDF مؤجل → مفيش مكتبة PDF أو endpoint مخصص اتضاف؛ Task 5's `GetByIdAsync`/`PlanDetailDto` هو الـ endpoint الغني بالبيانات المطلوب فقط.

**Placeholder scan:** none found.

**Type consistency check:** `SubProjectFinancialYear.SubProjectFinancialYearId`/`SubProjectId`/`FinancialYearId` (Task 2) match every later reference in Tasks 4-5. `FinancialYearDto.Id` (Task 3) matches the `int financialYearId` parameters used in Task 4's link DTOs. `Roles.PlanningStaff` (Task 3) matches every controller's `[Authorize(Roles = Roles.PlanningStaff)]` in Tasks 3-5. `IGenericRepository<MainProject>` addition (Task 5, folded into Step 5's note) matches the existing `MainProjectRepository`/`IMainProjectRepository` naming already in the codebase — using the plain generic repository is intentional here since no custom query is needed, just `GetByIdAsync`.
