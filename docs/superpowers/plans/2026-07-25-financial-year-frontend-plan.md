# Financial-Year & Plan Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a financial-year dropdown to the Projects page that scopes which sub-projects are shown, let new sub-projects link to one or more financial years via checkboxes, and let staff generate + print (browser print-to-PDF) a "suggested" or "approved" plan document for the selected year — plus the three small backend additions this requires.

**Architecture:** Three small ASP.NET Core backend additions (budget field, a search filter, a caller-supplied approval date) on top of the already-merged `FinancialYear`/`SubProjectFinancialYear`/`Plan` schema, followed by an Angular frontend slice that matches every existing UI convention in this codebase exactly (`si-btn`/`si-modal`/`si-grid` shared CSS classes, signals, `FormsModule` two-way `ngModel` binding, `forkJoin` for parallel HTTP calls).

**Tech Stack:** ASP.NET Core 10 / EF Core 10 (backend), Angular (standalone components, signals, zoneless-style) with RTL Arabic UI (frontend).

## Global Constraints

- All user-facing strings are Arabic, matching every existing string in both the backend and the Angular app.
- Backend: follow existing conventions exactly — `[Column(TypeName = "decimal(18,2)")]` for currency-like decimals, `NotFoundException`→404, `BusinessRuleException`→400 (already wired, do not touch the middleware), FluentValidation `.WithMessage(...)` in Arabic. `[Authorize]` composition: class-level and method-level attributes AND-compose in ASP.NET Core — never add a method-level role that isn't a subset of the controller's class-level gate (confirmed real, previously-fixed bug in this codebase).
- Frontend: reuse the global CSS classes already defined in `Frontend/src/styles.css` (`si-btn`, `si-btn.primary`, `si-btn.gold`, `si-overlay`, `si-modal`, `si-modal-head`, `si-modal-body`, `si-modal-foot`, `si-grid`, `si-fld`, `si-step`, `si-note`, `si-err`) — do not invent new modal/button styling. Match the existing signal + `computed()` + `FormsModule`/`ngModel` pattern used throughout `Frontend/src/app/features/*` — do not introduce Angular Reactive Forms or `ChangeDetectorRef`.
- Run `dotnet build Backend` (0 errors) after backend tasks; there is no automated test suite in either project (confirmed: no `.Tests.csproj` in Backend, no `*.spec.ts` runner wired beyond the Angular CLI default which isn't used here) — verification is build success plus manual HTTP/UI checks, matching this codebase's existing convention throughout.
- EF migrations: run from `Backend/src/SmartInvest.API` as `dotnet ef migrations add <Name> --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .` (same flags for `database update`). The plain form without flags fails with a migrations-assembly mismatch on this machine.
- Print flow explicitly does NOT paginate — every "print" action must fetch the complete list of sub-projects linked to the selected financial year (not just the on-screen page), per the design spec.

---

## File Structure Overview

```
Backend/src/SmartInvest.Domain/
  Entities/FinancialYear.cs                          [MODIFY]
  Interfaces/ISubProjectRepository.cs                 [MODIFY]

Backend/src/SmartInvest.Infrastructure/
  Repositories/SubProjectRepository.cs                [MODIFY]
  Migrations/..._AddFinancialYearBudget               [CREATE via dotnet ef]

Backend/src/SmartInvest.Application/
  DTOs/FinancialYearDtos.cs                            [MODIFY]
  Validators/CreateFinancialYearDtoValidator.cs        [MODIFY]
  Services/FinancialYearService.cs                     [MODIFY]
  Interfaces/ISubProjectService.cs                     [MODIFY]
  Services/SubProjectService.cs                        [MODIFY]
  DTOs/PlanDtos.cs                                     [MODIFY]
  Interfaces/IPlanService.cs                           [MODIFY]
  Services/PlanService.cs                              [MODIFY]

Backend/src/SmartInvest.API/
  Controllers/SubProjectsController.cs                 [MODIFY]
  Controllers/PlansController.cs                       [MODIFY]

Frontend/src/
  styles.css                                           [MODIFY]
  app/core/models/project.models.ts                    [MODIFY]
  app/core/services/financial-years.service.ts         [CREATE]
  app/core/services/plans.service.ts                   [CREATE]
  app/core/services/projects.service.ts                [MODIFY]
  app/features/projects/projects.ts                    [MODIFY]
  app/features/projects/projects.html                  [MODIFY]
  app/features/projects/projects.css                   [MODIFY]
  app/features/projects/sub-project-form.ts             [MODIFY]
  app/features/plans/plan-print.ts                      [CREATE]
  app/features/plans/plan-print.html                    [CREATE]
  app/features/plans/plan-print.css                     [CREATE]
  app/app.routes.ts                                     [MODIFY]
```

---

### Task 1: `FinancialYear.Budget` field

**Files:**
- Modify: `Backend/src/SmartInvest.Domain/Entities/FinancialYear.cs`
- Modify: `Backend/src/SmartInvest.Application/DTOs/FinancialYearDtos.cs`
- Modify: `Backend/src/SmartInvest.Application/Validators/CreateFinancialYearDtoValidator.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/FinancialYearService.cs`

**Interfaces:**
- Consumes: nothing from other tasks in this plan.
- Produces: `FinancialYearDto.Budget` (`decimal?`) — consumed by Task 4 (frontend `FinancialYear` model) and Task 5 (add-year modal budget input).

- [ ] **Step 1: Add the field to the entity**

In `Backend/src/SmartInvest.Domain/Entities/FinancialYear.cs`, add this block right after `public bool IsClosed { get; set; }`:

```csharp

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Budget { get; set; }
```

- [ ] **Step 2: Add it to the three DTOs**

In `Backend/src/SmartInvest.Application/DTOs/FinancialYearDtos.cs`, replace the full file contents:

```csharp
namespace SmartInvest.Application.DTOs;

public class FinancialYearDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public decimal? Budget { get; set; }
}

public class CreateFinancialYearDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal? Budget { get; set; }
}

public class UpdateFinancialYearDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public decimal? Budget { get; set; }
}
```

- [ ] **Step 3: Validate it's non-negative when provided**

In `Backend/src/SmartInvest.Application/Validators/CreateFinancialYearDtoValidator.cs`, add this rule inside `CreateFinancialYearDtoValidator`'s constructor, right after the existing `EndDate` rule:

```csharp

        RuleFor(x => x.Budget).GreaterThanOrEqualTo(0).When(x => x.Budget.HasValue)
            .WithMessage("الموازنة لا يمكن أن تكون سالبة");
```

Add the identical block inside `UpdateFinancialYearDtoValidator`'s constructor, in the same relative position (right after its `EndDate` rule).

- [ ] **Step 4: Set it in the service**

In `Backend/src/SmartInvest.Application/Services/FinancialYearService.cs`, in `CreateAsync`, add `Budget = dto.Budget,` to the `FinancialYear` object initializer (after `IsClosed = false,`):

```csharp
        var year = new FinancialYear
        {
            Name = dto.Name,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsClosed = false,
            Budget = dto.Budget,
        };
```

In `UpdateAsync`, add this line right after `year.IsClosed = dto.IsClosed;`:

```csharp
        year.Budget = dto.Budget;
```

- [ ] **Step 5: Build and verify**

Run: `dotnet build Backend`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Generate the migration**

Run from `Backend/src/SmartInvest.API`:

```bash
cd Backend/src/SmartInvest.API
dotnet ef migrations add AddFinancialYearBudget --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

Expected: `Done.` Open the generated migration's `Up()` and confirm it contains exactly one `AddColumn` operation for a nullable `Budget` decimal(18,2) column on the `FinancialYears` table — nothing else.

- [ ] **Step 7: Apply the migration (skip if no local SQL Server is reachable)**

```bash
dotnet ef database update --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

- [ ] **Step 8: Manual verification**

Start the API, log in as admin. `POST /api/financial-years` with `{"name":"سنة اختبار","startDate":"2026-07-01","endDate":"2027-06-30","budget":5000000}` → expect 201 with `budget: 5000000` in the response. `PUT /api/financial-years/{id}` with `budget: null` → expect `budget: null` in the response. Delete the test year afterward (`DELETE /api/financial-years/{id}`) so it doesn't pollute later manual tests. Stop the API process when done.

- [ ] **Step 9: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Entities/FinancialYear.cs \
        Backend/src/SmartInvest.Application/DTOs/FinancialYearDtos.cs \
        Backend/src/SmartInvest.Application/Validators/CreateFinancialYearDtoValidator.cs \
        Backend/src/SmartInvest.Application/Services/FinancialYearService.cs \
        Backend/src/SmartInvest.Infrastructure/Migrations/
git commit -m "feat: add optional budget field to FinancialYear"
```

---

### Task 2: `financialYearId` filter on sub-project search

**Files:**
- Modify: `Backend/src/SmartInvest.Domain/Interfaces/ISubProjectRepository.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Repositories/SubProjectRepository.cs`
- Modify: `Backend/src/SmartInvest.Application/Interfaces/ISubProjectService.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/SubProjectService.cs`
- Modify: `Backend/src/SmartInvest.API/Controllers/SubProjectsController.cs`

**Interfaces:**
- Consumes: `SubProject.FinancialYears` (`ICollection<SubProjectFinancialYear>`, pre-existing).
- Produces: `GET /api/subprojects?financialYearId={id}` — consumed by Task 4/5 (frontend search).

The new parameter is inserted in the SAME position in all five signatures below: right after `statusId`, right before `searchTerm`. Keep this position identical everywhere — the four layers call each other positionally.

- [ ] **Step 1: Repository interface**

In `Backend/src/SmartInvest.Domain/Interfaces/ISubProjectRepository.cs`, replace the `SearchAsync` line:

```csharp
    Task<(IReadOnlyList<SubProject> Items, int TotalCount)> SearchAsync(int? mainProjectId, int? mainProgramId, int? subProgramId, int? markazId, int? priorityId, int? statusId, int? financialYearId, string? searchTerm, int page, int pageSize, CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Repository implementation**

In `Backend/src/SmartInvest.Infrastructure/Repositories/SubProjectRepository.cs`, replace the `SearchAsync` method signature line:

```csharp
    public async Task<(IReadOnlyList<SubProject> Items, int TotalCount)> SearchAsync(
        int? mainProjectId,
        int? mainProgramId,
        int? subProgramId,
        int? markazId,
        int? priorityId,
        int? statusId,
        int? financialYearId,
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
```

Add this block right after the existing `statusId` filter (`if (statusId.HasValue) { ... }`) and before the `searchTerm` filter:

```csharp

        if (financialYearId.HasValue)
        {
            query = query.Where(x => x.FinancialYears.Any(y => y.FinancialYearId == financialYearId));
        }
```

- [ ] **Step 3: Application service interface**

In `Backend/src/SmartInvest.Application/Interfaces/ISubProjectService.cs`, replace the `SearchAsync` line:

```csharp
    Task<PagedResultDto<SubProjectListItemDto>> SearchAsync(int? mainProjectId, int? mainProgramId, int? subProgramId, int? markazId, int? priorityId, int? statusId, int? financialYearId, string? searchTerm, int page, int pageSize, CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Application service implementation**

In `Backend/src/SmartInvest.Application/Services/SubProjectService.cs`, replace the `SearchAsync` method:

```csharp
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
```

- [ ] **Step 5: Controller**

In `Backend/src/SmartInvest.API/Controllers/SubProjectsController.cs`, replace the `Search` action:

```csharp
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<SubProjectListItemDto>>> Search(
        [FromQuery] int? mainProjectId,
        [FromQuery] int? mainProgramId,
        [FromQuery] int? subProgramId,
        [FromQuery] int? markazId,
        [FromQuery] int? priorityId,
        [FromQuery] int? statusId,
        [FromQuery] int? financialYearId,
        [FromQuery] string? searchTerm,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var effectivePage = page <= 0 ? 1 : page;
        var effectivePageSize = pageSize <= 0 ? 20 : pageSize;

        var result = await _subProjectService.SearchAsync(
            mainProjectId, mainProgramId, subProgramId, markazId,
            priorityId, statusId, financialYearId, searchTerm, effectivePage, effectivePageSize, cancellationToken);

        return Ok(result);
    }
```

- [ ] **Step 6: Build and verify**

Run: `dotnet build Backend`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Manual verification**

As admin: pick a `financialYearId` you know has at least one linked sub-project (or link one first via `POST /api/subprojects/{id}/financial-years`). `GET /api/subprojects?financialYearId={id}&page=1&pageSize=20` → expect only sub-projects linked to that year. `GET /api/subprojects?financialYearId=999999&page=1&pageSize=20` → expect `totalCount: 0`, empty `items`. `GET /api/subprojects?page=1&pageSize=20` (no `financialYearId`) → expect unfiltered results identical to before this change. Stop the API process when done.

- [ ] **Step 8: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Interfaces/ISubProjectRepository.cs \
        Backend/src/SmartInvest.Infrastructure/Repositories/SubProjectRepository.cs \
        Backend/src/SmartInvest.Application/Interfaces/ISubProjectService.cs \
        Backend/src/SmartInvest.Application/Services/SubProjectService.cs \
        Backend/src/SmartInvest.API/Controllers/SubProjectsController.cs
git commit -m "feat: add financialYearId filter to sub-project search"
```

---

### Task 3: Caller-supplied plan approval date

**Files:**
- Modify: `Backend/src/SmartInvest.Application/DTOs/PlanDtos.cs`
- Modify: `Backend/src/SmartInvest.Application/Interfaces/IPlanService.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/PlanService.cs`
- Modify: `Backend/src/SmartInvest.API/Controllers/PlansController.cs`

**Interfaces:**
- Consumes: nothing from other tasks in this plan.
- Produces: `ApprovePlanDto { DateTime ApprovalDate }`, `PUT /api/plans/{id}/approve` now requires this body — consumed by Task 4 (frontend `ApprovePlan` model) and Task 5 (approved-print flow).

- [ ] **Step 1: Add the DTO**

In `Backend/src/SmartInvest.Application/DTOs/PlanDtos.cs`, add this class at the end of the file:

```csharp

public class ApprovePlanDto
{
    public DateTime ApprovalDate { get; set; }
}
```

- [ ] **Step 2: Update the service interface**

In `Backend/src/SmartInvest.Application/Interfaces/IPlanService.cs`, replace the `ApproveAsync` line:

```csharp
    Task<PlanDto> ApproveAsync(int planId, DateTime approvalDate, CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Update the service implementation**

In `Backend/src/SmartInvest.Application/Services/PlanService.cs`, replace the `ApproveAsync` method:

```csharp
    public async Task<PlanDto> ApproveAsync(int planId, DateTime approvalDate, CancellationToken cancellationToken = default)
    {
        var plan = await GetOrThrowAsync(planId, cancellationToken);

        if (plan.ApprovalDate.HasValue)
        {
            throw new BusinessRuleException("تم اعتماد هذه الخطة بالفعل");
        }

        plan.ApprovalDate = approvalDate;
        plan.PlanStatus = "معتمدة";

        _planRepository.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapPlanAsync(plan, cancellationToken);
    }
```

- [ ] **Step 4: Update the controller**

In `Backend/src/SmartInvest.API/Controllers/PlansController.cs`, replace the `Approve` action:

```csharp
    [HttpPut("{id:int}/approve")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<PlanDto>> Approve(int id, ApprovePlanDto dto, CancellationToken cancellationToken)
    {
        var result = await _planService.ApproveAsync(id, dto.ApprovalDate, cancellationToken);
        return Ok(result);
    }
```

- [ ] **Step 5: Build and verify**

Run: `dotnet build Backend`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Manual verification**

As admin: create a plan (`POST /api/plans`), then `PUT /api/plans/{id}/approve` with `{"approvalDate":"2026-06-15"}` → expect 200, `approvalDate: "2026-06-15T00:00:00"` (exact value you sent, not today's date), `planStatus: "معتمدة"`. Repeat the same call → expect 400 "تم اعتماد هذه الخطة بالفعل". Stop the API process when done.

- [ ] **Step 7: Commit**

```bash
git add Backend/src/SmartInvest.Application/DTOs/PlanDtos.cs \
        Backend/src/SmartInvest.Application/Interfaces/IPlanService.cs \
        Backend/src/SmartInvest.Application/Services/PlanService.cs \
        Backend/src/SmartInvest.API/Controllers/PlansController.cs
git commit -m "feat: accept caller-supplied approval date on Plan approve"
```

---

### Task 4: Frontend models and services

**Files:**
- Modify: `Frontend/src/app/core/models/project.models.ts`
- Create: `Frontend/src/app/core/services/financial-years.service.ts`
- Create: `Frontend/src/app/core/services/plans.service.ts`
- Modify: `Frontend/src/app/core/services/projects.service.ts`

**Interfaces:**
- Consumes: the three backend additions from Tasks 1-3 (`FinancialYearDto.budget`, `financialYearId` search param, `ApprovePlanDto`).
- Produces: `FinancialYear`, `CreateFinancialYear`, `UpdateFinancialYear`, `SubProjectFinancialYear`, `Plan`, `PlanDetail`, `PlanSuggestedProject`, `CreatePlan`, `UpdatePlan`, `ApprovePlan` TypeScript interfaces; `FinancialYearsService`, `PlansService` classes; `ProjectsService.getSubProjectFinancialYears/linkFinancialYear/unlinkFinancialYear` methods; `SubProjectSearchParams.financialYearId`. All consumed by Tasks 5-7.

- [ ] **Step 1: Add the new model interfaces**

In `Frontend/src/app/core/models/project.models.ts`, add this block at the end of the file:

```typescript

export interface FinancialYear {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  isClosed: boolean;
  budget: number | null;
}

export interface CreateFinancialYear {
  name: string;
  startDate: string;
  endDate: string;
  budget?: number | null;
}

export interface UpdateFinancialYear {
  name: string;
  startDate: string;
  endDate: string;
  isClosed: boolean;
  budget?: number | null;
}

export interface SubProjectFinancialYear {
  id: number;
  financialYearId: number;
  financialYearName: string;
  startDate: string;
  endDate: string;
  isClosed: boolean;
}

export interface Plan {
  id: number;
  planName: string;
  planStatus: string;
  suggestionDate: string;
  approvalDate: string | null;
  financialYearId: number;
  financialYearName: string;
}

export interface PlanSuggestedProject {
  subProjectId: number;
  subProjectName: string;
  subProjectCode: string | null;
  mainProjectName: string;
  bankFunding: number;
  selfFunding: number;
  totalCost: number;
}

export interface PlanDetail extends Plan {
  suggestedProjects: PlanSuggestedProject[];
}

export interface CreatePlan {
  planName: string;
  financialYearId: number;
}

export interface UpdatePlan {
  planName: string;
}

export interface ApprovePlan {
  approvalDate: string;
}
```

- [ ] **Step 2: Add `financialYearId` to the search params**

In `Frontend/src/app/core/models/project.models.ts`, in `SubProjectSearchParams`, add this line right after `statusId?: number;`:

```typescript
  financialYearId?: number;
```

- [ ] **Step 3: `FinancialYearsService`**

Create `Frontend/src/app/core/services/financial-years.service.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateFinancialYear, FinancialYear, UpdateFinancialYear } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class FinancialYearsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/financial-years`;

  getAll(): Observable<FinancialYear[]> {
    return this.http.get<FinancialYear[]>(this.base);
  }

  getById(id: number): Observable<FinancialYear> {
    return this.http.get<FinancialYear>(`${this.base}/${id}`);
  }

  create(dto: CreateFinancialYear): Observable<FinancialYear> {
    return this.http.post<FinancialYear>(this.base, dto);
  }

  update(id: number, dto: UpdateFinancialYear): Observable<FinancialYear> {
    return this.http.put<FinancialYear>(`${this.base}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
```

- [ ] **Step 4: `PlansService`**

Create `Frontend/src/app/core/services/plans.service.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApprovePlan, CreatePlan, Plan, PlanDetail, UpdatePlan } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class PlansService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/plans`;

  getAll(): Observable<Plan[]> {
    return this.http.get<Plan[]>(this.base);
  }

  getById(id: number): Observable<PlanDetail> {
    return this.http.get<PlanDetail>(`${this.base}/${id}`);
  }

  create(dto: CreatePlan): Observable<Plan> {
    return this.http.post<Plan>(this.base, dto);
  }

  update(id: number, dto: UpdatePlan): Observable<Plan> {
    return this.http.put<Plan>(`${this.base}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  addSuggestedProject(planId: number, subProjectId: number): Observable<PlanDetail> {
    return this.http.post<PlanDetail>(`${this.base}/${planId}/suggested-projects`, { subProjectId });
  }

  removeSuggestedProject(planId: number, subProjectId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${planId}/suggested-projects/${subProjectId}`);
  }

  approve(planId: number, dto: ApprovePlan): Observable<Plan> {
    return this.http.put<Plan>(`${this.base}/${planId}/approve`, dto);
  }
}
```

- [ ] **Step 5: Extend `ProjectsService`**

In `Frontend/src/app/core/services/projects.service.ts`:

1. Add `SubProjectFinancialYear` to the import from `'../models/project.models'` (add it to the existing named-import list).

2. In `searchSubProjects`, add `'financialYearId'` to the `optional` array:

```typescript
    const optional: (keyof SubProjectSearchParams)[] = [
      'mainProjectId', 'mainProgramId', 'subProgramId', 'markazId',
      'priorityId', 'statusId', 'financialYearId', 'searchTerm',
    ];
```

3. Add these three methods at the end of the class, right before the final closing `}`:

```typescript

  // ===== السنوات المالية للمشروع الفرعي =====
  getSubProjectFinancialYears(subProjectId: number): Observable<SubProjectFinancialYear[]> {
    return this.http.get<SubProjectFinancialYear[]>(`${this.base}/subprojects/${subProjectId}/financial-years`);
  }

  linkFinancialYear(subProjectId: number, financialYearId: number): Observable<SubProjectFinancialYear> {
    return this.http.post<SubProjectFinancialYear>(
      `${this.base}/subprojects/${subProjectId}/financial-years`,
      { financialYearId },
    );
  }

  unlinkFinancialYear(subProjectId: number, financialYearId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/subprojects/${subProjectId}/financial-years/${financialYearId}`);
  }
```

- [ ] **Step 6: Build and verify**

Run: `cd Frontend && npx ng build`
Expected: build succeeds with no TypeScript errors (existing budget-warning messages about CSS file sizes, if any, are pre-existing and unrelated).

- [ ] **Step 7: Commit**

```bash
git add Frontend/src/app/core/models/project.models.ts \
        Frontend/src/app/core/services/financial-years.service.ts \
        Frontend/src/app/core/services/plans.service.ts \
        Frontend/src/app/core/services/projects.service.ts
git commit -m "feat: add FinancialYear/Plan models and services"
```

---

### Task 5: Projects page — financial-year dropdown, add-year, print actions

**Files:**
- Modify: `Frontend/src/app/features/projects/projects.ts`
- Modify: `Frontend/src/app/features/projects/projects.html`
- Modify: `Frontend/src/app/features/projects/projects.css`

**Interfaces:**
- Consumes: `FinancialYearsService`, `PlansService`, `ProjectsService.searchSubProjects` (with `financialYearId`), `FinancialYear`, `CreateFinancialYear`, `CreatePlan`, `ApprovePlan` (Task 4).
- Produces: `Projects.selectedYearId` (`Signal<number | null>`) — consumed by Task 6 (passed to `SubProjectForm` as the pre-check default).

- [ ] **Step 1: Add imports and injected services**

In `Frontend/src/app/features/projects/projects.ts`, replace the import block at the top of the file:

```typescript
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ProjectsService } from '../../core/services/projects.service';
import { LookupsService } from '../../core/services/lookups.service';
import { AuthService } from '../../core/services/auth.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { PlansService } from '../../core/services/plans.service';
import {
  EXECUTING_AGENCIES,
  FinancialYear,
  Lookup,
  MainProjectListItem,
  MarkazLookup,
  SubProgramLookup,
  SubProjectListItem,
} from '../../core/models/project.models';
import { MainProjectForm } from './main-project-form';
import { SubProjectForm, LockedParent } from './sub-project-form';
```

In the `Projects` class, add these injected services right after `private readonly auth = inject(AuthService);`:

```typescript
  private readonly financialYearsService = inject(FinancialYearsService);
  private readonly plansService = inject(PlansService);
  private readonly router = inject(Router);
```

- [ ] **Step 2: Add financial-year state**

Add this block right after the `subs` signal declaration (`protected readonly subs = signal<SubProjectListItem[]>([]);`):

```typescript

  // ===== السنة المالية =====
  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly selectedYearId = signal<number | null>(null);
  protected readonly sortedYears = computed(() =>
    [...this.financialYears()].sort((a, b) => b.startDate.localeCompare(a.startDate)),
  );
  protected readonly printing = signal(false);

  protected readonly showAddYearForm = signal(false);
  protected readonly newYearBudget = signal<number | null>(null);
  protected readonly addYearError = signal<string | null>(null);
  protected readonly savingYear = signal(false);

  protected readonly showApprovedDateForm = signal(false);
  protected readonly approvedDate = signal('');
```

- [ ] **Step 3: Load financial years and re-run search on year change**

Replace the constructor:

```typescript
  constructor() {
    this.loadFinancialYears();
    this.loadLookups();
    effect(() => {
      this.searchTerm();
      this.approvalFilter();
      this.fMainProgram(); this.fSubProgram(); this.fLevel();
      this.fAgency(); this.fMarkaz(); this.fPriority(); this.fFunding();
      this.page.set(1);
    });
  }
```

Replace the `load()` method:

```typescript
  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      mains: this.projectsService.getMainProjects(),
      subs: this.projectsService.searchSubProjects({
        page: 1,
        pageSize: 1000,
        financialYearId: this.selectedYearId() ?? undefined,
      }),
    }).subscribe({
      next: ({ mains, subs }) => {
        this.mains.set(mains);
        this.subs.set(subs.items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذّر تحميل المشروعات. تأكد من تشغيل الخادم وتسجيل الدخول.');
        this.loading.set(false);
      },
    });
  }

  private loadFinancialYears(): void {
    this.financialYearsService.getAll().subscribe({
      next: (years) => {
        this.financialYears.set(years);
        const sorted = [...years].sort((a, b) => b.startDate.localeCompare(a.startDate));
        if (this.selectedYearId() == null && sorted.length > 0) {
          this.selectedYearId.set(sorted[0].id);
        }
        this.load();
      },
      error: () => {
        this.load();
      },
    });
  }

  protected onYearChange(id: number): void {
    this.selectedYearId.set(id);
    this.load();
  }
```

- [ ] **Step 4: Add-year flow**

Add these methods right before the final closing `}` of the `Projects` class:

```typescript

  // ===== إضافة سنة مالية =====
  protected computeNextYear(): { name: string; startDate: string; endDate: string } {
    const latest = this.sortedYears()[0];
    let start: Date;
    if (latest) {
      start = new Date(latest.endDate);
      start.setDate(start.getDate() + 1);
    } else {
      start = new Date();
    }
    const end = new Date(start);
    end.setFullYear(end.getFullYear() + 1);
    end.setDate(end.getDate() - 1);
    const toIso = (d: Date) => d.toISOString().slice(0, 10);
    return { name: `${start.getFullYear()}/${end.getFullYear()}`, startDate: toIso(start), endDate: toIso(end) };
  }

  protected openAddYear(): void {
    this.newYearBudget.set(null);
    this.addYearError.set(null);
    this.showAddYearForm.set(true);
  }

  protected closeAddYear(): void {
    this.showAddYearForm.set(false);
  }

  protected confirmAddYear(): void {
    if (this.savingYear()) return;
    const next = this.computeNextYear();
    this.savingYear.set(true);
    this.addYearError.set(null);
    this.financialYearsService
      .create({ name: next.name, startDate: next.startDate, endDate: next.endDate, budget: this.newYearBudget() })
      .subscribe({
        next: (year) => {
          this.savingYear.set(false);
          this.showAddYearForm.set(false);
          this.financialYears.update((list) => [...list, year]);
          this.selectedYearId.set(year.id);
          this.load();
        },
        error: (err) => {
          this.savingYear.set(false);
          this.addYearError.set(err?.error?.message ?? 'تعذّر إضافة السنة المالية');
        },
      });
  }

  // ===== طباعة الخطة المقترحة =====
  protected printSuggested(): void {
    const yearId = this.selectedYearId();
    if (!yearId || this.printing()) return;
    const year = this.financialYears().find((y) => y.id === yearId);
    if (!year) return;

    this.printing.set(true);
    this.projectsService.searchSubProjects({ financialYearId: yearId, page: 1, pageSize: 5000 }).subscribe({
      next: (result) => {
        this.plansService.create({ planName: `الخطة المقترحة - ${year.name}`, financialYearId: yearId }).subscribe({
          next: (plan) => this.addAllSuggested(plan.id, result.items.map((s) => s.id)),
          error: () => {
            this.printing.set(false);
            alert('تعذّر إنشاء الخطة');
          },
        });
      },
      error: () => {
        this.printing.set(false);
        alert('تعذّر تحميل مشروعات السنة المالية');
      },
    });
  }

  private addAllSuggested(planId: number, subProjectIds: number[]): void {
    if (subProjectIds.length === 0) {
      this.printing.set(false);
      this.router.navigate(['/app/plans', planId]);
      return;
    }
    const calls = subProjectIds.map((id) => this.plansService.addSuggestedProject(planId, id));
    forkJoin(calls).subscribe({
      next: () => {
        this.printing.set(false);
        this.router.navigate(['/app/plans', planId]);
      },
      error: () => {
        this.printing.set(false);
        this.router.navigate(['/app/plans', planId]);
      },
    });
  }

  // ===== طباعة الخطة المعتمدة =====
  protected openApprovedPrint(): void {
    if (!this.selectedYearId()) return;
    this.approvedDate.set(new Date().toISOString().slice(0, 10));
    this.showApprovedDateForm.set(true);
  }

  protected closeApprovedPrint(): void {
    this.showApprovedDateForm.set(false);
  }

  protected confirmApprovedPrint(): void {
    const yearId = this.selectedYearId();
    const date = this.approvedDate();
    if (!yearId || !date || this.printing()) return;
    const year = this.financialYears().find((y) => y.id === yearId);
    if (!year) return;

    this.showApprovedDateForm.set(false);
    this.printing.set(true);
    this.projectsService.searchSubProjects({ financialYearId: yearId, page: 1, pageSize: 5000 }).subscribe({
      next: (result) => {
        const approvedIds = result.items.filter((s) => !!s.code).map((s) => s.id);
        this.plansService.create({ planName: `الخطة المعتمدة - ${year.name}`, financialYearId: yearId }).subscribe({
          next: (plan) => this.addAllThenApprove(plan.id, approvedIds, date),
          error: () => {
            this.printing.set(false);
            alert('تعذّر إنشاء الخطة');
          },
        });
      },
      error: () => {
        this.printing.set(false);
        alert('تعذّر تحميل مشروعات السنة المالية');
      },
    });
  }

  private addAllThenApprove(planId: number, subProjectIds: number[], approvalDate: string): void {
    const afterAdd = () => {
      this.plansService.approve(planId, { approvalDate }).subscribe({
        next: () => {
          this.printing.set(false);
          this.router.navigate(['/app/plans', planId]);
        },
        error: () => {
          this.printing.set(false);
          this.router.navigate(['/app/plans', planId]);
        },
      });
    };

    if (subProjectIds.length === 0) {
      afterAdd();
      return;
    }
    const calls = subProjectIds.map((id) => this.plansService.addSuggestedProject(planId, id));
    forkJoin(calls).subscribe({ next: afterAdd, error: afterAdd });
  }
```

- [ ] **Step 5: Toolbar markup**

In `Frontend/src/app/features/projects/projects.html`, replace the toolbar block (from `<!-- شريط الأدوات العلوي... -->` through its closing `</div>`, i.e. the block starting `<div class="toolbar">`):

```html
  <!-- شريط الأدوات العلوي: السنة + فلتر الحالة + الإضافة -->
  <div class="toolbar">
    <select class="mini" [ngModel]="selectedYearId()" (ngModelChange)="onYearChange($event)">
      @for (y of sortedYears(); track y.id) { <option [ngValue]="y.id">{{ y.name }}</option> }
    </select>
    <button class="si-btn" (click)="openAddYear()">
      <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 5v14M5 12h14" /></svg>
      إضافة سنة مالية
    </button>
    <button class="si-btn" [disabled]="!selectedYearId() || printing()" (click)="printSuggested()">
      <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M6 9V2h12v7M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2M6 14h12v8H6z" /></svg>
      طباعة الخطة المقترحة
    </button>
    <button class="si-btn" [disabled]="!selectedYearId() || printing()" (click)="openApprovedPrint()">
      <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M6 9V2h12v7M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2M6 14h12v8H6z" /></svg>
      طباعة الخطة المعتمدة
    </button>
    <div class="seg">
      <button [class.on]="approvalFilter() === 'all'" (click)="approvalFilter.set('all')">كل المشروعات</button>
      <button [class.on]="approvalFilter() === 'approved'" (click)="approvalFilter.set('approved')">معتمدة</button>
      <button [class.on]="approvalFilter() === 'pending'" (click)="approvalFilter.set('pending')">بانتظار الاعتماد</button>
    </div>
    <div class="grow"></div>
    <div class="menu-wrap">
      <button class="si-btn primary" (click)="toggleAddMenu()">
        <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 5v14M5 12h14" /></svg>
        إضافة مشروع
        <svg viewBox="0 0 24 24" width="13" fill="none" stroke="currentColor" stroke-width="2"><path d="m6 9 6 6 6-6" /></svg>
      </button>
      @if (addMenuOpen()) {
        <div class="menu">
          <button (click)="openAddMain()"><span class="mi main">◆</span> مشروع رئيسي</button>
          <button (click)="openAddSub()"><span class="mi sub">◆</span> مشروع فرعي</button>
        </div>
      }
    </div>
    <button class="si-btn" (click)="load()">
      <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M21 12a9 9 0 1 1-3-6.7L21 8M21 3v5h-5" /></svg>
      تحديث
    </button>
  </div>
```

- [ ] **Step 6: Add-year and approved-date modals**

In `Frontend/src/app/features/projects/projects.html`, add these two blocks right before the final closing `</div>` of the file (after the `<app-sub-project-form ... />` line):

```html

  <!-- نافذة إضافة سنة مالية -->
  @if (showAddYearForm()) {
    <div class="si-overlay" (click)="closeAddYear()">
      <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(440px,100%)">
        <div class="si-modal-head">
          <div class="grow"><h3>إضافة سنة مالية جديدة</h3><p>{{ computeNextYear().name }} ({{ computeNextYear().startDate }} — {{ computeNextYear().endDate }})</p></div>
          <button class="si-x" (click)="closeAddYear()" aria-label="إغلاق">×</button>
        </div>
        <div class="si-modal-body">
          @if (addYearError()) { <div class="si-err">{{ addYearError() }}</div> }
          <div class="si-fld">
            <label>الموازنة (اختياري)</label>
            <input type="number" [ngModel]="newYearBudget()" (ngModelChange)="newYearBudget.set($event)" placeholder="0" />
          </div>
        </div>
        <div class="si-modal-foot">
          <button class="si-btn primary" [disabled]="savingYear()" (click)="confirmAddYear()">
            @if (savingYear()) { جاري الإضافة… } @else { إضافة السنة }
          </button>
          <button class="si-btn" (click)="closeAddYear()">إلغاء</button>
        </div>
      </div>
    </div>
  }

  <!-- نافذة تاريخ اعتماد الخطة -->
  @if (showApprovedDateForm()) {
    <div class="si-overlay" (click)="closeApprovedPrint()">
      <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(440px,100%)">
        <div class="si-modal-head">
          <div class="grow"><h3>تاريخ اعتماد الخطة</h3><p>هذا التاريخ سيُسجَّل كتاريخ اعتماد الخطة المطبوعة</p></div>
          <button class="si-x" (click)="closeApprovedPrint()" aria-label="إغلاق">×</button>
        </div>
        <div class="si-modal-body">
          <div class="si-fld">
            <label>تاريخ الاعتماد <span class="req">*</span></label>
            <input type="date" [ngModel]="approvedDate()" (ngModelChange)="approvedDate.set($event)" />
          </div>
        </div>
        <div class="si-modal-foot">
          <button class="si-btn primary" [disabled]="!approvedDate()" (click)="confirmApprovedPrint()">طباعة</button>
          <button class="si-btn" (click)="closeApprovedPrint()">إلغاء</button>
        </div>
      </div>
    </div>
  }
```

- [ ] **Step 7: Toolbar CSS tweak**

In `Frontend/src/app/features/projects/projects.css`, the existing `.toolbar` rule already wraps (`flex-wrap: wrap`) and has `select.mini`/`.si-btn` spacing handled globally — no changes needed here. Confirm this by inspection; do not add new rules for this step.

- [ ] **Step 8: Build and verify**

Run: `cd Frontend && npx ng build`
Expected: build succeeds with no TypeScript errors.

- [ ] **Step 9: Manual verification**

Start the API and the Angular dev server (`cd Frontend && npx ng serve`). Log in, go to `/app/projects`. Confirm: the year dropdown shows real financial years (not the old hardcoded "2026/2027"); switching years reloads the table to that year's sub-projects only; "إضافة سنة مالية" opens a modal showing a computed next-year name/date range, accepts an optional budget, and on confirm adds+selects the new (now empty) year; "طباعة الخطة المقترحة" navigates to `/app/plans/{id}` after creating a plan; "طباعة الخطة المعتمدة" prompts for a date first, then navigates. (The `/app/plans/:id` page itself doesn't exist yet until Task 7 — expect a blank/broken route for now, that's expected at this point in the plan.)

- [ ] **Step 10: Commit**

```bash
git add Frontend/src/app/features/projects/projects.ts \
        Frontend/src/app/features/projects/projects.html \
        Frontend/src/app/features/projects/projects.css
git commit -m "feat: add financial-year dropdown, add-year, and plan print actions to Projects page"
```

---

### Task 6: Sub-project form — link to one or more financial years

**Files:**
- Modify: `Frontend/src/app/features/projects/sub-project-form.ts`
- Modify: `Frontend/src/app/features/projects/projects.html`

**Interfaces:**
- Consumes: `FinancialYearsService`, `ProjectsService.getSubProjectFinancialYears/linkFinancialYear/unlinkFinancialYear` (Task 4), `Projects.selectedYearId` (Task 5).
- Produces: nothing consumed by later tasks — this is the last consumer of the checkbox-linking mechanism.

- [ ] **Step 1: Wire the new input from the parent**

In `Frontend/src/app/features/projects/projects.html`, replace the `<app-sub-project-form ... />` line:

```html
  <app-sub-project-form [open]="showSubForm()" [edit]="subEdit()" [locked]="subLocked()" [mains]="mains()" [defaultYearId]="selectedYearId()" (close)="closeModals()" (saved)="onSaved()" />
```

- [ ] **Step 2: Extend the component**

In `Frontend/src/app/features/projects/sub-project-form.ts`:

1. Replace the import block at the top:

```typescript
import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ProjectsService } from '../../core/services/projects.service';
import { LookupsService } from '../../core/services/lookups.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import {
  FinancialYear,
  Lookup,
  MainProjectListItem,
  MarkazLookup,
  SubProjectListItem,
} from '../../core/models/project.models';
```

2. Leave the `@Component` decorator's `imports: [FormsModule],` line exactly as-is — the checkbox markup added in Step 3 below uses plain `(change)` handlers, not `ngModel`, so no new import is needed there.

3. Add a new input right after `readonly mains = input<MainProjectListItem[]>([]);`:

```typescript
  readonly defaultYearId = input<number | null>(null);
```

4. Add `private readonly financialYearsService = inject(FinancialYearsService);` right after `private readonly lookups = inject(LookupsService);`.

5. Add this block right after `protected readonly markazList = signal<MarkazLookup[]>([]);`:

```typescript

  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly checkedYearIds = signal<Set<number>>(new Set());
  private originalYearIds = new Set<number>();
```

6. Replace `ensureLookups` to also load financial years:

```typescript
  private ensureLookups(done: () => void): void {
    if (this.lookupsLoaded) {
      done();
      return;
    }
    forkJoin({
      priorities: this.lookups.getPriorities(),
      statuses: this.lookups.getStatuses(),
      markaz: this.lookups.getMarkaz(),
      financialYears: this.financialYearsService.getAll(),
    }).subscribe({
      next: ({ priorities, statuses, markaz, financialYears }) => {
        this.priorities.set(priorities);
        this.statuses.set(statuses);
        this.markazList.set(markaz);
        this.financialYears.set(financialYears);
        this.lookupsLoaded = true;
        done();
      },
      error: () => this.error.set('تعذّر تحميل القوائم'),
    });
  }
```

7. Replace `prefill()` to also load/pre-check financial years:

```typescript
  private prefill(): void {
    this.resetForm();
    const e = this.edit();
    const lockedParent = this.locked();

    if (lockedParent) {
      this.mainProjectId.set(lockedParent.id);
    }

    if (e) {
      // جلب التفاصيل الكاملة للتعديل
      this.projectsService.getSubProject(e.id).subscribe({
        next: (d) => {
          this.mainProjectId.set(d.mainProjectId);
          this.name.set(d.name);
          this.projectLevel.set(d.projectLevel);
          this.componentType.set(d.componentType);
          this.markazId.set(d.markazId);
          this.priorityId.set(d.priorityId);
          this.statusId.set(d.statusId);
          this.bankFunding.set(d.bankFunding);
          this.selfFunding.set(d.selfFunding);
          this.description.set(d.description ?? '');
        },
        error: () => this.error.set('تعذّر تحميل بيانات المشروع الفرعي'),
      });

      this.projectsService.getSubProjectFinancialYears(e.id).subscribe({
        next: (links) => {
          const ids = new Set(links.map((l) => l.financialYearId));
          this.originalYearIds = ids;
          this.checkedYearIds.set(new Set(ids));
        },
      });
    } else {
      this.originalYearIds = new Set<number>();
      const defaultId = this.defaultYearId();
      this.checkedYearIds.set(defaultId != null ? new Set([defaultId]) : new Set<number>());
    }
  }
```

8. Add `toggleYear` right after `resetForm()`:

```typescript

  protected toggleYear(id: number): void {
    this.checkedYearIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }
```

9. In `submit()`, find this exact block (the last part of the method, currently the last thing in the class):

```typescript
    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.emit();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر حفظ المشروع الفرعي');
      },
    });
  }
}
```

Replace it with (note the final `}` that used to close the class is now AFTER the new `syncFinancialYears` method — the class closes one line later than before):

```typescript
    req.subscribe({
      next: (result) => {
        const subProjectId = editing ? editing.id : result.id;
        this.syncFinancialYears(subProjectId);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر حفظ المشروع الفرعي');
      },
    });
  }

  private syncFinancialYears(subProjectId: number): void {
    const desired = this.checkedYearIds();
    const toLink = [...desired].filter((id) => !this.originalYearIds.has(id));
    const toUnlink = [...this.originalYearIds].filter((id) => !desired.has(id));
    const calls = [
      ...toLink.map((id) => this.projectsService.linkFinancialYear(subProjectId, id)),
      ...toUnlink.map((id) => this.projectsService.unlinkFinancialYear(subProjectId, id)),
    ];

    if (calls.length === 0) {
      this.saving.set(false);
      this.saved.emit();
      return;
    }

    forkJoin(calls).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.emit();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر تحديث ربط السنوات المالية');
      },
    });
  }
}
```

- [ ] **Step 3: Add the checkbox section to the template**

In the `template` literal inside `sub-project-form.ts`, add this block right after the closing `</div>` of the existing `si-grid` (the one containing `الاسم/المستوى/المكوّن العيني/...`) and before the closing `</div>` of `si-modal-body`:

```html

            <div class="si-step"><span class="n">3</span><h4>السنوات المالية</h4></div>
            <div class="si-years">
              @for (y of financialYears(); track y.id) {
                <label class="si-year-chk">
                  <input type="checkbox" [checked]="checkedYearIds().has(y.id)" (change)="toggleYear(y.id)" />
                  {{ y.name }}
                </label>
              } @empty {
                <p class="hint">لا توجد سنوات مالية بعد.</p>
              }
            </div>
```

- [ ] **Step 4: Add checkbox-grid CSS**

In `sub-project-form.ts`'s `styles` array (currently a single string with the `.mini-sp`/`@keyframes` rules), append this to the same string (before the closing backtick):

```css
.si-years{display:flex;flex-wrap:wrap;gap:10px;margin-bottom:16px}.si-year-chk{display:flex;align-items:center;gap:7px;border:1px solid var(--line-strong);border-radius:9px;padding:8px 12px;font-size:13px;font-weight:700;background:var(--surface)}.si-years .hint{font-size:12px;color:var(--muted)}
```

- [ ] **Step 5: Build and verify**

Run: `cd Frontend && npx ng build`
Expected: build succeeds with no TypeScript errors.

- [ ] **Step 6: Manual verification**

Start the API and dev server. On `/app/projects` with a year selected, click "إضافة مشروع" → "مشروع فرعي": confirm the year checkboxes appear and the currently-selected dropdown year is pre-checked. Save the new sub-project, then re-open it for editing: confirm the same year is checked (reflecting the real link, not just UI state). Check an additional year, save, re-open: confirm both years are now checked. Uncheck one, save, re-open: confirm only the remaining one is checked. Stop the API/dev server when done.

- [ ] **Step 7: Commit**

```bash
git add Frontend/src/app/features/projects/sub-project-form.ts \
        Frontend/src/app/features/projects/projects.html
git commit -m "feat: link sub-projects to one or more financial years via checkboxes"
```

---

### Task 7: Plan print page

**Files:**
- Create: `Frontend/src/app/features/plans/plan-print.ts`
- Create: `Frontend/src/app/features/plans/plan-print.html`
- Create: `Frontend/src/app/features/plans/plan-print.css`
- Modify: `Frontend/src/app/app.routes.ts`
- Modify: `Frontend/src/styles.css`

**Interfaces:**
- Consumes: `PlansService.getById` (Task 4), `PlanDetail`/`PlanSuggestedProject` models (Task 4). Reached via `router.navigate(['/app/plans', planId])` from Task 5.
- Produces: nothing consumed by later tasks — terminal task.

- [ ] **Step 1: Component**

Create `Frontend/src/app/features/plans/plan-print.ts`:

```typescript
import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PlansService } from '../../core/services/plans.service';
import { PlanDetail } from '../../core/models/project.models';

@Component({
  selector: 'app-plan-print',
  imports: [RouterLink, DatePipe],
  templateUrl: './plan-print.html',
  styleUrl: './plan-print.css',
})
export class PlanPrint {
  private readonly route = inject(ActivatedRoute);
  private readonly plansService = inject(PlansService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly plan = signal<PlanDetail | null>(null);

  protected readonly totalBank = computed(
    () => this.plan()?.suggestedProjects.reduce((a, p) => a + p.bankFunding, 0) ?? 0,
  );
  protected readonly totalSelf = computed(
    () => this.plan()?.suggestedProjects.reduce((a, p) => a + p.selfFunding, 0) ?? 0,
  );
  protected readonly totalCost = computed(
    () => this.plan()?.suggestedProjects.reduce((a, p) => a + p.totalCost, 0) ?? 0,
  );

  constructor() {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.plansService.getById(id).subscribe({
      next: (p) => {
        this.plan.set(p);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذّر تحميل الخطة');
        this.loading.set(false);
      },
    });
  }

  protected money(value: number): string {
    return (value ?? 0).toLocaleString('en-US');
  }

  protected print(): void {
    window.print();
  }
}
```

- [ ] **Step 2: Template**

Create `Frontend/src/app/features/plans/plan-print.html`:

```html
<div class="print-page">
  @if (loading()) {
    <div class="state">جاري التحميل…</div>
  } @else if (error() || !plan()) {
    <div class="state error">{{ error() ?? 'الخطة غير موجودة' }}</div>
  } @else {
    <div class="toolbar no-print">
      <a class="si-btn" routerLink="/app/projects">
        <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="1.9"><path d="M9 6l6 6-6 6" /></svg>
        رجوع للمشروعات
      </a>
      <button class="si-btn primary" (click)="print()">
        <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M6 9V2h12v7M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2M6 14h12v8H6z" /></svg>
        طباعة
      </button>
    </div>

    <div class="sheet">
      <header class="sheet-head">
        <h1>{{ plan()!.planName }}</h1>
        <div class="meta">
          <span>السنة المالية: {{ plan()!.financialYearName }}</span>
          <span>الحالة: {{ plan()!.planStatus }}</span>
          <span>تاريخ الاقتراح: {{ plan()!.suggestionDate | date: 'yyyy/MM/dd' }}</span>
          @if (plan()!.approvalDate) {
            <span>تاريخ الاعتماد: {{ plan()!.approvalDate | date: 'yyyy/MM/dd' }}</span>
          }
        </div>
      </header>

      <table>
        <thead>
          <tr><th>#</th><th>المشروع الرئيسي</th><th>المشروع الفرعي</th><th>الكود</th><th>تمويل بنكي</th><th>تمويل ذاتي</th><th>الإجمالي</th></tr>
        </thead>
        <tbody>
          @for (p of plan()!.suggestedProjects; track p.subProjectId; let i = $index) {
            <tr>
              <td>{{ i + 1 }}</td>
              <td>{{ p.mainProjectName }}</td>
              <td>{{ p.subProjectName }}</td>
              <td>{{ p.subProjectCode ?? '—' }}</td>
              <td class="tnum">{{ money(p.bankFunding) }}</td>
              <td class="tnum">{{ money(p.selfFunding) }}</td>
              <td class="tnum">{{ money(p.totalCost) }}</td>
            </tr>
          } @empty {
            <tr><td colspan="7" class="empty">لا توجد مشروعات في هذه الخطة.</td></tr>
          }
        </tbody>
        <tfoot>
          <tr>
            <td colspan="4">الإجمالي</td>
            <td class="tnum">{{ money(totalBank()) }}</td>
            <td class="tnum">{{ money(totalSelf()) }}</td>
            <td class="tnum">{{ money(totalCost()) }}</td>
          </tr>
        </tfoot>
      </table>
    </div>
  }
</div>
```

- [ ] **Step 3: Styles**

Create `Frontend/src/app/features/plans/plan-print.css`:

```css
.print-page { padding: 24px 28px; }
.toolbar { display: flex; gap: 10px; margin-bottom: 16px; }
.sheet { background: var(--surface); border: 1px solid var(--line); border-radius: var(--radius); box-shadow: var(--shadow); padding: 28px; }
.sheet-head { margin-bottom: 20px; border-bottom: 2px solid var(--green-700); padding-bottom: 14px; }
.sheet-head h1 { font-size: 20px; margin-bottom: 8px; }
.meta { display: flex; gap: 18px; flex-wrap: wrap; color: var(--muted); font-size: 13px; font-weight: 700; }
table { width: 100%; border-collapse: collapse; }
th, td { padding: 10px 12px; border: 1px solid var(--line); font-size: 13px; text-align: start; }
th { background: var(--surface-2); font-weight: 700; }
tfoot td { font-weight: 800; background: var(--surface-2); }
.empty { text-align: center; color: var(--muted); padding: 20px; }
.state { padding: 40px; text-align: center; color: var(--muted); }
.state.error { color: #b32a39; }

@media print {
  .no-print { display: none !important; }
  .print-page { padding: 0; }
  .sheet { box-shadow: none; border: none; }
}
```

- [ ] **Step 4: Hide the app shell when printing**

In `Frontend/src/styles.css`, add this block at the end of the file:

```css

/* ===== طباعة: إخفاء هيكل التطبيق (الشريط الجانبي والترويسة) ===== */
@media print {
  .side, .topbar { display: none !important; }
  .content { background: #fff !important; }
}
```

This targets `.side`/`.topbar`/`.content` from `Frontend/src/app/layout/main-layout/main-layout.css` — those rules are scoped to that component and cannot be overridden from outside it, so the print-hide rule must live in this global stylesheet, not in `plan-print.css`.

- [ ] **Step 5: Route**

In `Frontend/src/app/app.routes.ts`, add this route object to the `app` route's `children` array, right after the `projects/:id` route block:

```typescript
      {
        path: 'plans/:id',
        loadComponent: () =>
          import('./features/plans/plan-print').then((m) => m.PlanPrint),
      },
```

- [ ] **Step 6: Build and verify**

Run: `cd Frontend && npx ng build`
Expected: build succeeds with no TypeScript errors.

- [ ] **Step 7: Manual verification**

Start the API and dev server. From `/app/projects`, click "طباعة الخطة المقترحة" (or navigate directly to `/app/plans/{id}` for a plan id created during Task 5's verification). Confirm: the plan name/year/dates/status render correctly, the project table shows every linked sub-project (not just one page's worth) with correct totals in the footer, and clicking "طباعة" opens the browser's print dialog. Open the browser's print preview and confirm the sidebar/topbar are not visible in the preview. Stop the API/dev server when done.

- [ ] **Step 8: Commit**

```bash
git add Frontend/src/app/features/plans/plan-print.ts \
        Frontend/src/app/features/plans/plan-print.html \
        Frontend/src/app/features/plans/plan-print.css \
        Frontend/src/app/app.routes.ts \
        Frontend/src/styles.css
git commit -m "feat: add printable plan detail page"
```

---

## Self-Review Notes

**Spec coverage:**
- إضافة موازنة للسنة المالية → Task 1.
- فلتر السنة المالية على البحث → Task 2.
- تاريخ اعتماد يحدده المستخدم → Task 3.
- قائمة السنة المالية المنسدلة في صفحة المشروعات، تُحمّل مشروعات السنة تلقائيًا → Task 5 (Steps 2-3, 5).
- زر "إضافة سنة مالية" بحساب السنة القادمة تلقائيًا + موازنة اختيارية → Task 5 (Step 4, add-year flow).
- زرّي "طباعة الخطة المقترحة"/"طباعة الخطة المعتمدة"، خطتان منفصلتان لكل سنة، القائمة الكاملة غير المُرقّمة → Task 5 (print flows, `pageSize: 5000`).
- صندوق اختيار متعدد للسنوات المالية في نموذج المشروع الفرعي، محدد مسبقًا على السنة الحالية، يعمل للإنشاء والتعديل (تمديد المشروع لسنة إضافية) → Task 6.
- صفحة طباعة الخطة، بدون عنصر قائمة جانبية، تنسيق طباعة يخفي الشريط الجانبي → Task 7.
- الربط الجماعي: أُلغي أثناء النقاش، لا يوجد له Task — صحيح، غير مطلوب.

**Placeholder scan:** none found — every step has complete code.

**Type consistency check:** `FinancialYearDto.Budget`/`FinancialYear.budget` (Task 1/4) match. `SearchAsync`'s `financialYearId` parameter position (right after `statusId`, before `searchTerm`) is identical across all 4 backend layers in Task 2 and the frontend `optional` array position in Task 4/`SubProjectSearchParams`. `ApprovePlanDto.ApprovalDate`/`ApprovePlan.approvalDate` (Task 3/4) match, and `IPlanService.ApproveAsync(int planId, DateTime approvalDate, ...)`'s new signature is used correctly by the Task 3 controller. `Projects.selectedYearId` (Task 5) matches the `defaultYearId` input type (`number | null`) consumed in Task 6. `ProjectsService.getSubProjectFinancialYears/linkFinancialYear/unlinkFinancialYear` (Task 4) method names and parameter order match their Task 6 call sites exactly.
