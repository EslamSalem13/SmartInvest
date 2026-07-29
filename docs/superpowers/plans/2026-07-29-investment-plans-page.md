# Investment Plans Page + Home Redirect Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the two plan-print buttons on the projects page with one "الخطط الاستثمارية" button that opens a new page listing all archived plans as filterable cards, while keeping the ability to generate a new suggested/approved plan snapshot; also redirect already-logged-in users away from the login screen.

**Architecture:** Backend DTO/mapping/query gets three fields added so a plan list row carries enough data to link and label itself. Frontend gets one new standalone page (`plan-list`) that owns the "generate a new plan" flow (moved out of `projects.ts`) plus a card list with a client-side status filter. A new router guard handles the home-page redirect.

**Tech Stack:** .NET 10 (EF Core, AutoMapper) backend; Angular 21 standalone components + Signals frontend, no Reactive Forms.

## Global Constraints

- No unit/integration test suite exists anywhere in this repo (`Frontend/src/app/app.spec.ts` is the only `.spec.ts` file and is the default Angular scaffold; there is no backend test project). Do **not** introduce a new test framework as part of this plan. Each task's "test" step is: build/type-check, then a manual check via the browser preview tool (per this project's own convention — see `docs/PROJECT.md` section on frontend changes).
- Follow existing code conventions exactly: Arabic UI strings, `si-btn`/`si-modal`/`si-overlay`/`si-fld`/`si-err` shared CSS classes (`Frontend/src/styles.css`), Signals (not RxJS state, not plain getters), `[ngModel]`/`(ngModelChange)` (no Reactive Forms), `AuthService.isManager` for manager-gated UI (never compare `role() === 'PlanningManager'` directly).
- Backend: class-level `[Authorize]` with no roles, method-level roles explicit (existing `PlansController` convention — do not change its authorization).
- Never run the dev servers via Bash — use the `preview_start` tool (per this session's tooling rules), when a manual browser check is needed.

---

### Task 1: Backend — expand the plan list DTO

**Files:**
- Modify: `Backend/src/SmartInvest.Application/DTOs/Plan/PlanWithoutProjectsDto.cs`
- Modify: `Backend/src/SmartInvest.Application/Common/Mappings/PlansAndPrograms.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Repositories/PlanRepo.cs`

**Interfaces:**
- Consumes: `Plan` entity (`Backend/src/SmartInvest.Domain/Entities/Plan.cs`) — has `PlanId`, `PlanName`, `PlanStatus` (enum), `StartDate`, `EndDate`, `IsClosed`, `SuggestionDate`, `ApprovalDate`, `FinancialYearId`, `FinancialYear` (nav, has `Name`).
- Produces: `GET /api/plans` now returns, per item: `planId: number`, `financialYearId: number`, `financialYearName: string`, `suggestionDate: string` (ISO), in addition to the existing `planName`, `startDate`, `endDate`, `planStatus`, `approvalDate`. Task 4 (frontend plan-list) consumes this shape.

- [ ] **Step 1: Add the new fields to the DTO**

Replace the full contents of `Backend/src/SmartInvest.Application/DTOs/Plan/PlanWithoutProjectsDto.cs`:

```csharp
namespace SmartInvest.Application.DTOs.Plan
{
    public class PlanWithoutProjectsDto
    {
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PlanStatus { get; set; } = string.Empty;
        public DateTime? ApprovalDate { get; set; }
        public int FinancialYearId { get; set; }
        public string FinancialYearName { get; set; } = string.Empty;
        public DateTime SuggestionDate { get; set; }
    }
}
```

- [ ] **Step 2: Map `FinancialYearName` explicitly**

In `Backend/src/SmartInvest.Application/Common/Mappings/PlansAndPrograms.cs`, find:

```csharp
            CreateMap<Plan, PlanWithoutProjectsDto>()
           .ReverseMap();
```

Replace with:

```csharp
            CreateMap<Plan, PlanWithoutProjectsDto>()
           .ForMember(d => d.FinancialYearName, o => o.MapFrom(s => s.FinancialYear!.Name))
           .ReverseMap();
```

(`PlanId`, `FinancialYearId`, `SuggestionDate` map automatically — same property names on both sides.)

- [ ] **Step 3: Include `FinancialYear` in the list query**

In `Backend/src/SmartInvest.Infrastructure/Repositories/PlanRepo.cs`, find:

```csharp
            public List<Plan>? GetPlanByStatusAndName(PlanStatus? Status, string? PlanName)
            {
                 var  Query = Context.Plans
                    .Include(p => p.PlanProjects)
                    .AsQueryable(); 
```

Replace with:

```csharp
            public List<Plan>? GetPlanByStatusAndName(PlanStatus? Status, string? PlanName)
            {
                 var  Query = Context.Plans
                    .Include(p => p.PlanProjects)
                    .Include(p => p.FinancialYear)
                    .AsQueryable(); 
```

- [ ] **Step 4: Build the backend**

Run:
```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 5: Manual check via Swagger**

Start the backend with the `preview_start` tool using the `backend-api` config from `.claude/launch.json`, then navigate to `https://localhost:7250/swagger`. Execute `GET /api/plans` (no query params, needs a valid bearer token — log in via `POST /api/auth/login` with `superadmin` / `SuperAdmin@123` first and paste the token into the Authorize button). Confirm the JSON response items include non-null `planId`, `financialYearId`, `financialYearName`, `suggestionDate`.

- [ ] **Step 6: Commit**

```bash
git add Backend/src/SmartInvest.Application/DTOs/Plan/PlanWithoutProjectsDto.cs Backend/src/SmartInvest.Application/Common/Mappings/PlansAndPrograms.cs Backend/src/SmartInvest.Infrastructure/Repositories/PlanRepo.cs
git commit -m "feat: include plan id and financial year on the plan list DTO"
```

---

### Task 2: Frontend — home redirect for already-logged-in users

**Files:**
- Modify: `Frontend/src/app/core/guards/auth.guard.ts`
- Modify: `Frontend/src/app/app.routes.ts`

**Interfaces:**
- Consumes: `AuthService.isAuthenticated: Signal<boolean>`, `AuthService.role: Signal<string | null>`, `AuthService.homeRouteForRole(role: string | null): string` (all exist already in `Frontend/src/app/core/services/auth.service.ts`).
- Produces: `guestGuard: CanActivateFn`, exported from `auth.guard.ts`, used only on the `''` route.

- [ ] **Step 1: Add `guestGuard`**

In `Frontend/src/app/core/guards/auth.guard.ts`, append after the existing `roleGuard`:

```ts
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return router.createUrlTree([auth.homeRouteForRole(auth.role())]);
  }

  return true;
};
```

- [ ] **Step 2: Apply the guard to the `''` route**

In `Frontend/src/app/app.routes.ts`, add the import and the guard:

```ts
import { authGuard, guestGuard, roleGuard } from './core/guards/auth.guard';
```

(replaces the existing `import { authGuard, roleGuard } from './core/guards/auth.guard';`)

```ts
  {
    path: '',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/home/home').then((m) => m.Home),
  },
```

(adds `canActivate: [guestGuard]` to the existing `''` route entry — no other change to that block.)

- [ ] **Step 3: Type-check the frontend**

Run:
```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors printed.

- [ ] **Step 4: Manual check in the browser**

Start `frontend-dev` and `backend-api` via `preview_start`. Log in once (any seeded account). Then navigate directly to `http://localhost:4200/` (the bare root) again while still logged in — confirm it lands on `/app/dashboard` (manager/superadmin) or `/app/projects` (other roles) without showing the login form. Then call `logout()` (via the UI logout control) and visit `/` again — confirm the login form now shows normally.

- [ ] **Step 5: Commit**

```bash
git add Frontend/src/app/core/guards/auth.guard.ts Frontend/src/app/app.routes.ts
git commit -m "feat: redirect already-authenticated users away from the login screen"
```

---

### Task 3: Frontend — extend the `Plan` model

**Files:**
- Modify: `Frontend/src/app/core/models/project.models.ts`

**Interfaces:**
- Produces: `Plan` interface used by `PlansService.getAll()` (Task 4) and unchanged by `PlanDetail`/`PlansService.getById()` (that endpoint already returns full data via `PlanInfoDto`, untouched by this plan).

- [ ] **Step 1: Add the new fields**

In `Frontend/src/app/core/models/project.models.ts`, find:

```ts
export interface Plan {
  planName: string;
  startDate: string;
  endDate: string;
  planStatus: string;
  approvalDate: string | null;
}
```

Replace with:

```ts
export interface Plan {
  planId: number;
  planName: string;
  startDate: string;
  endDate: string;
  planStatus: string;
  approvalDate: string | null;
  financialYearId: number;
  financialYearName: string;
  suggestionDate: string;
}
```

- [ ] **Step 2: Type-check the frontend**

Run:
```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors (nothing currently constructs a bare `Plan` object without these fields — `PlanDetail extends Plan` is only ever read from HTTP responses, never built locally).

- [ ] **Step 3: Commit**

```bash
git add Frontend/src/app/core/models/project.models.ts
git commit -m "feat: add planId and financial year fields to the frontend Plan model"
```

---

### Task 4: Frontend — new "الخطط الاستثمارية" plan-list page

**Files:**
- Create: `Frontend/src/app/features/plans/plan-list.ts`
- Create: `Frontend/src/app/features/plans/plan-list.html`
- Create: `Frontend/src/app/features/plans/plan-list.css`
- Modify: `Frontend/src/app/app.routes.ts`

**Interfaces:**
- Consumes: `PlansService.getAll(planStatus?, planName?): Observable<Plan[]>`, `PlansService.create(dto: CreatePlan): Observable<CreatedPlan>`, `PlansService.addExistingProject(planId, subProjectId): Observable<void>`, `PlansService.approve(planId, dto: ApprovePlan): Observable<PlanDetail>` (all exist in `Frontend/src/app/core/services/plans.service.ts`); `FinancialYearsService.getAll(): Observable<FinancialYear[]>`; `ProjectsService.searchSubProjects(params): Observable<PagedResult<SubProjectListItem>>`; `AuthService.isManager: Signal<boolean>`; `Plan` model from Task 3.
- Produces: route `/app/plans` (static, distinct from the existing `/app/plans/:id`).

- [ ] **Step 1: Create `plan-list.ts`**

```ts
import { Component, computed, inject, signal } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { PlansService } from '../../core/services/plans.service';
import { ProjectsService } from '../../core/services/projects.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { AuthService } from '../../core/services/auth.service';
import { FinancialYear, Plan } from '../../core/models/project.models';

@Component({
  selector: 'app-plan-list',
  imports: [FormsModule, RouterLink, SlicePipe],
  templateUrl: './plan-list.html',
  styleUrl: './plan-list.css',
})
export class PlanList {
  private readonly plansService = inject(PlansService);
  private readonly projectsService = inject(ProjectsService);
  private readonly financialYearsService = inject(FinancialYearsService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly isManager = this.auth.isManager;

  // ===== السنة المالية =====
  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly selectedYearId = signal<number | null>(null);
  protected readonly sortedYears = computed(() =>
    [...this.financialYears()].sort((a, b) => b.startDate.localeCompare(a.startDate)),
  );
  protected readonly generating = signal(false);

  protected readonly showApprovedDateForm = signal(false);
  protected readonly approvedDate = signal('');

  // ===== قائمة الخطط =====
  protected readonly plans = signal<Plan[]>([]);
  protected readonly plansLoading = signal(true);
  protected readonly plansError = signal<string | null>(null);
  protected readonly statusFilter = signal<'all' | 'approved' | 'pending'>('all');

  protected readonly filteredPlans = computed(() => {
    const filter = this.statusFilter();
    return [...this.plans()]
      .filter((p) => {
        if (filter === 'approved') return p.planStatus === 'Approved';
        if (filter === 'pending') return p.planStatus !== 'Approved';
        return true;
      })
      .sort((a, b) => b.suggestionDate.localeCompare(a.suggestionDate));
  });

  constructor() {
    this.loadFinancialYears();
    this.loadPlans();
  }

  private loadFinancialYears(): void {
    this.financialYearsService.getAll().subscribe({
      next: (years) => {
        this.financialYears.set(years);
        const sorted = [...years].sort((a, b) => b.startDate.localeCompare(a.startDate));
        if (this.selectedYearId() == null && sorted.length > 0) {
          this.selectedYearId.set(sorted[0].id);
        }
      },
    });
  }

  protected loadPlans(): void {
    this.plansLoading.set(true);
    this.plansError.set(null);
    this.plansService.getAll().subscribe({
      next: (plans) => {
        this.plans.set(plans);
        this.plansLoading.set(false);
      },
      error: () => {
        this.plansError.set('تعذّر تحميل الخطط. تأكد من تشغيل الخادم وتسجيل الدخول.');
        this.plansLoading.set(false);
      },
    });
  }

  protected money(value: number): string {
    return (value ?? 0).toLocaleString('en-US');
  }

  protected statusLabel(status: string): string {
    return status === 'Approved' ? 'معتمدة' : 'مقترحة';
  }

  private toLocalIsoDate(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  // ===== إنشاء خطة مقترحة جديدة =====
  protected generateSuggested(): void {
    const yearId = this.selectedYearId();
    if (!yearId || this.generating()) return;
    const year = this.financialYears().find((y) => y.id === yearId);
    if (!year) return;

    this.generating.set(true);
    this.projectsService.searchSubProjects({ financialYearId: yearId, page: 1, pageSize: 5000 }).subscribe({
      next: (result) => {
        this.plansService
          .create({
            planName: `الخطة المقترحة - ${year.name}`,
            startDate: year.startDate,
            endDate: year.endDate,
            planStatus: 'Suggested',
            financialYearId: yearId,
          })
          .subscribe({
            next: (plan) => this.addAllThenGo(plan.planId, result.items.map((s) => s.id)),
            error: () => {
              this.generating.set(false);
              alert('تعذّر إنشاء الخطة');
            },
          });
      },
      error: () => {
        this.generating.set(false);
        alert('تعذّر تحميل مشروعات السنة المالية');
      },
    });
  }

  private addAllThenGo(planId: number, subProjectIds: number[]): void {
    if (subProjectIds.length === 0) {
      this.generating.set(false);
      this.loadPlans();
      this.router.navigate(['/app/plans', planId]);
      return;
    }
    const calls = subProjectIds.map((id) => this.plansService.addExistingProject(planId, id));
    forkJoin(calls).subscribe({
      next: () => {
        this.generating.set(false);
        this.loadPlans();
        this.router.navigate(['/app/plans', planId]);
      },
      error: () => {
        this.generating.set(false);
        this.loadPlans();
        alert('تعذّر إضافة بعض المشروعات للخطة، قد تكون الخطة المطبوعة غير مكتملة');
        this.router.navigate(['/app/plans', planId]);
      },
    });
  }

  // ===== إنشاء خطة معتمدة جديدة =====
  protected openApprovedGenerate(): void {
    if (!this.selectedYearId()) return;
    this.approvedDate.set(this.toLocalIsoDate(new Date()));
    this.showApprovedDateForm.set(true);
  }

  protected closeApprovedGenerate(): void {
    this.showApprovedDateForm.set(false);
  }

  protected confirmApprovedGenerate(): void {
    const yearId = this.selectedYearId();
    const date = this.approvedDate();
    if (!yearId || !date || this.generating()) return;
    const year = this.financialYears().find((y) => y.id === yearId);
    if (!year) return;

    this.showApprovedDateForm.set(false);
    this.generating.set(true);
    this.projectsService.searchSubProjects({ financialYearId: yearId, page: 1, pageSize: 5000 }).subscribe({
      next: (result) => {
        const approvedIds = result.items.filter((s) => s.isApproved).map((s) => s.id);
        this.plansService
          .create({
            planName: `الخطة المعتمدة - ${year.name}`,
            startDate: year.startDate,
            endDate: year.endDate,
            planStatus: 'Suggested',
            financialYearId: yearId,
          })
          .subscribe({
            next: (plan) => this.addAllThenApprove(plan.planId, approvedIds, date),
            error: () => {
              this.generating.set(false);
              alert('تعذّر إنشاء الخطة');
            },
          });
      },
      error: () => {
        this.generating.set(false);
        alert('تعذّر تحميل مشروعات السنة المالية');
      },
    });
  }

  private addAllThenApprove(planId: number, subProjectIds: number[], approvalDate: string): void {
    const afterAdd = (addFailed: boolean) => {
      if (addFailed) {
        alert('تعذّر إضافة بعض المشروعات للخطة، قد تكون الخطة المطبوعة غير مكتملة');
      }
      this.plansService.approve(planId, { approvalDate }).subscribe({
        next: () => {
          this.generating.set(false);
          this.loadPlans();
          this.router.navigate(['/app/plans', planId]);
        },
        error: () => {
          this.generating.set(false);
          this.loadPlans();
          alert('تعذّر اعتماد الخطة، ستُطبع كخطة غير معتمدة');
          this.router.navigate(['/app/plans', planId]);
        },
      });
    };

    if (subProjectIds.length === 0) {
      afterAdd(false);
      return;
    }
    const calls = subProjectIds.map((id) => this.plansService.addExistingProject(planId, id));
    forkJoin(calls).subscribe({ next: () => afterAdd(false), error: () => afterAdd(true) });
  }
}
```

- [ ] **Step 2: Create `plan-list.html`**

```html
<div class="page">
  <header class="page-head">
    <div>
      <h1>الخطط الاستثمارية</h1>
      <p>أرشيف الخطط المقترحة والمعتمدة</p>
    </div>
  </header>

  <div class="toolbar">
    <select class="mini" [ngModel]="selectedYearId()" (ngModelChange)="selectedYearId.set($event)">
      @for (y of sortedYears(); track y.id) { <option [ngValue]="y.id">{{ y.name }}</option> }
    </select>
    <button class="si-btn" [disabled]="!selectedYearId() || generating()" (click)="generateSuggested()">
      <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M6 9V2h12v7M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2M6 14h12v8H6z" /></svg>
      خطة مقترحة جديدة
    </button>
    @if (isManager()) {
      <button class="si-btn" [disabled]="!selectedYearId() || generating()" (click)="openApprovedGenerate()">
        <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M6 9V2h12v7M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2M6 14h12v8H6z" /></svg>
        خطة معتمدة جديدة
      </button>
    }
    <div class="seg">
      <button [class.on]="statusFilter() === 'all'" (click)="statusFilter.set('all')">كل الخطط</button>
      <button [class.on]="statusFilter() === 'approved'" (click)="statusFilter.set('approved')">معتمدة</button>
      <button [class.on]="statusFilter() === 'pending'" (click)="statusFilter.set('pending')">مقترحة</button>
    </div>
    <div class="grow"></div>
    <button class="si-btn" (click)="loadPlans()">
      <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M21 12a9 9 0 1 1-3-6.7L21 8M21 3v5h-5" /></svg>
      تحديث
    </button>
  </div>

  @if (plansLoading()) {
    <div class="state"><span class="spinner"></span> جاري تحميل الخطط…</div>
  } @else if (plansError()) {
    <div class="state error">{{ plansError() }} <button class="si-btn" (click)="loadPlans()">إعادة المحاولة</button></div>
  } @else if (filteredPlans().length === 0) {
    <div class="state">لا توجد خطط مطابقة.</div>
  } @else {
    <div class="plan-grid">
      @for (plan of filteredPlans(); track plan.planId) {
        <a class="plan-card" [routerLink]="['/app/plans', plan.planId]">
          <div class="plan-card-head">
            <span class="pill" [class.ok]="plan.planStatus === 'Approved'" [class.warn]="plan.planStatus !== 'Approved'">
              {{ statusLabel(plan.planStatus) }}
            </span>
            <span class="fy">{{ plan.financialYearName }}</span>
          </div>
          <h3>{{ plan.planName }}</h3>
          <div class="plan-card-dates">
            <span>{{ plan.startDate }} — {{ plan.endDate }}</span>
          </div>
          <div class="plan-card-foot">
            <span>تاريخ الاقتراح: {{ plan.suggestionDate | slice:0:10 }}</span>
            @if (plan.approvalDate) { <span>تاريخ الاعتماد: {{ plan.approvalDate | slice:0:10 }}</span> }
          </div>
        </a>
      }
    </div>
  }

  @if (showApprovedDateForm()) {
    <div class="si-overlay" (click)="closeApprovedGenerate()">
      <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(440px,100%)">
        <div class="si-modal-head">
          <div class="grow"><h3>تاريخ اعتماد الخطة</h3><p>هذا التاريخ سيُسجَّل كتاريخ اعتماد الخطة الجديدة</p></div>
          <button class="si-x" (click)="closeApprovedGenerate()" aria-label="إغلاق">×</button>
        </div>
        <div class="si-modal-body">
          <div class="si-fld">
            <label>تاريخ الاعتماد <span class="req">*</span></label>
            <input type="date" [ngModel]="approvedDate()" (ngModelChange)="approvedDate.set($event)" />
          </div>
        </div>
        <div class="si-modal-foot">
          <button class="si-btn primary" [disabled]="!approvedDate()" (click)="confirmApprovedGenerate()">إنشاء</button>
          <button class="si-btn" (click)="closeApprovedGenerate()">إلغاء</button>
        </div>
      </div>
    </div>
  }
</div>
```

- [ ] **Step 3: Create `plan-list.css`**

```css
.plan-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 14px;
}

.plan-card {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 16px;
  border-radius: 12px;
  border: 1px solid var(--border, #e2e2e2);
  background: var(--surface, #fff);
  text-decoration: none;
  color: inherit;
  transition: box-shadow 0.15s ease, transform 0.15s ease;
}

.plan-card:hover {
  box-shadow: 0 4px 14px rgba(0, 0, 0, 0.08);
  transform: translateY(-2px);
}

.plan-card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.plan-card-head .fy {
  font-size: 0.85rem;
  color: var(--muted, #666);
}

.plan-card h3 {
  margin: 0;
  font-size: 1.05rem;
}

.plan-card-dates,
.plan-card-foot {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 0.85rem;
  color: var(--muted, #666);
}
```

- [ ] **Step 4: Wire the route**

In `Frontend/src/app/app.routes.ts`, add a new child route before the existing `plans/:id` entry:

```ts
      {
        path: 'plans',
        loadComponent: () =>
          import('./features/plans/plan-list').then((m) => m.PlanList),
      },
      {
        path: 'plans/:id',
        loadComponent: () =>
          import('./features/plans/plan-print').then((m) => m.PlanPrint),
      },
```

(the second block already exists — only the first `plans` block is new, inserted directly above it.)

- [ ] **Step 5: Type-check the frontend**

Run:
```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors.

- [ ] **Step 6: Manual check in the browser**

Start `frontend-dev` and `backend-api` via `preview_start`. Log in as `admin` / `Admin@123` (`PlanningManager`). Navigate to `http://localhost:4200/app/plans`. Confirm:
- Financial year selector is populated.
- Both "خطة مقترحة جديدة" and "خطة معتمدة جديدة" buttons are visible (manager role).
- Clicking "خطة مقترحة جديدة" creates a plan and navigates to its print page (`/app/plans/{id}`), and the printed page renders correctly.
- Going back to `/app/plans`, the new plan appears as a card with the right financial-year name and "مقترحة" badge; the "معتمدة"/"مقترحة" filter tabs correctly show/hide it.
- Clicking the card navigates to its print page.

Then log out and log in as a `PlanningEmployee` seeded account (if none exists, skip this sub-check) or otherwise inspect that `isManager()` correctly hides the approved-generate button for non-manager roles by reading `Frontend/src/app/features/plans/plan-list.html` — confirm the `@if (isManager())` guard wraps only that one button.

- [ ] **Step 7: Commit**

```bash
git add Frontend/src/app/features/plans/plan-list.ts Frontend/src/app/features/plans/plan-list.html Frontend/src/app/features/plans/plan-list.css Frontend/src/app/app.routes.ts
git commit -m "feat: add investment plans list page with filter and plan generation"
```

---

### Task 5: Frontend — consolidate the projects-page buttons

**Files:**
- Modify: `Frontend/src/app/features/projects/projects.ts`
- Modify: `Frontend/src/app/features/projects/projects.html`

**Interfaces:**
- Consumes: existing `RouterLink` (already imported in `projects.ts`'s `@Component` `imports` array).
- Produces: none consumed by later tasks (this is the last content task).

- [ ] **Step 1: Remove the relocated signals and methods from `projects.ts`**

Remove this line (around line 61):

```ts
  protected readonly printing = signal(false);
```

Remove these two lines (around lines 68-69):

```ts
  protected readonly showApprovedDateForm = signal(false);
  protected readonly approvedDate = signal('');
```

Remove the entire `// ===== طباعة الخطة المقترحة =====` block, i.e. everything from:

```ts
  // ===== طباعة الخطة المقترحة =====
  protected printSuggested(): void {
```

through the end of `addAllSuggested`'s closing brace (the block ending right before `// ===== اعتماد المشروع الفرعي =====`). This removes: `printSuggested`, `addAllSuggested`.

Remove the entire `// ===== طباعة الخطة المعتمدة =====` block, i.e. everything from:

```ts
  // ===== طباعة الخطة المعتمدة =====
  protected openApprovedPrint(): void {
```

through the end of `addAllThenApprove`'s closing brace (the block ending right before `// ===== إلغاء اعتماد المشروع الفرعي =====`). This removes: `openApprovedPrint`, `closeApprovedPrint`, `confirmApprovedPrint`, `addAllThenApprove`.

- [ ] **Step 2: Remove the now-unused `PlansService` and `Router` from `projects.ts`**

Remove the import line:

```ts
import { PlansService } from '../../core/services/plans.service';
```

Remove the import line:

```ts
import { Router, RouterLink } from '@angular/router';
```

Replace with:

```ts
import { RouterLink } from '@angular/router';
```

Remove the field:

```ts
  private readonly plansService = inject(PlansService);
```

Remove the field:

```ts
  private readonly router = inject(Router);
```

- [ ] **Step 2b: Confirm no remaining references**

Run:
```bash
cd Frontend && grep -n "plansService\|this\.router\b\|printing()\|showApprovedDateForm()\|approvedDate()" src/app/features/projects/projects.ts
```
Expected: no output (empty). If anything prints, it is a leftover reference that must be removed before proceeding — re-check Step 1 removed the full blocks.

- [ ] **Step 3: Replace the two buttons with one in `projects.html`**

Find (lines 19-28):

```html
    <button class="si-btn" [disabled]="!selectedYearId() || printing()" (click)="printSuggested()">
      <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M6 9V2h12v7M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2M6 14h12v8H6z" /></svg>
      طباعة الخطة المقترحة
    </button>
    @if (isManager()) {
      <button class="si-btn" [disabled]="!selectedYearId() || printing()" (click)="openApprovedPrint()">
        <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M6 9V2h12v7M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2M6 14h12v8H6z" /></svg>
        طباعة الخطة المعتمدة
      </button>
    }
```

Replace with:

```html
    <a class="si-btn" routerLink="/app/plans">
      <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M6 9V2h12v7M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2M6 14h12v8H6z" /></svg>
      الخطط الاستثمارية
    </a>
```

- [ ] **Step 4: Remove the approved-date modal from `projects.html`**

Find and delete this entire block:

```html
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

- [ ] **Step 5: Type-check the frontend**

Run:
```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors.

- [ ] **Step 6: Manual check in the browser**

With `frontend-dev` and `backend-api` still running, navigate to `http://localhost:4200/app/projects`. Confirm:
- Only one button, "الخطط الاستثمارية", appears where the two print buttons used to be (for every role).
- Clicking it navigates to `/app/plans`.
- The rest of the projects page (KPIs, filters, table, sub-project approval/cancel-approval modals, add-year modal) is unaffected.

- [ ] **Step 7: Commit**

```bash
git add Frontend/src/app/features/projects/projects.ts Frontend/src/app/features/projects/projects.html
git commit -m "refactor: replace plan print buttons with a link to the investment plans page"
```

---

### Task 6: Final end-to-end verification

**Files:** none (verification only).

- [ ] **Step 1: Full regression pass in the browser**

With both dev servers running (`frontend-dev`, `backend-api`):
1. Visit `/` logged out → login form shows.
2. Log in as `superadmin` → redirected to `/app/dashboard`.
3. Visit `/` again while logged in → immediately redirected to `/app/dashboard` (no login flash).
4. Go to `/app/projects` → confirm single "الخطط الاستثمارية" button, click it.
5. On `/app/plans`: generate a suggested plan for the current financial year, confirm it prints; generate an approved plan, confirm it prints with the "معتمدة" status; confirm the الكل/معتمدة/مقترحة filter tabs work; confirm both new cards are clickable and route to their print pages.
6. Log out, log back in as `admin` / `Admin@123`, repeat step 5's manager-only "خطة معتمدة جديدة" visibility check.

- [ ] **Step 2: Confirm no stray console errors**

Use `read_console_messages` (onlyErrors: true) during the pass above — expected: empty, or only pre-existing unrelated warnings.

- [ ] **Step 3: Final review of `git status`**

Run:
```bash
git status
```
Confirm only the files touched by Tasks 1-5 show as modified/new (plus any pre-existing uncommitted files noted in `docs/PROJECT.md` section 10, which are out of scope for this plan and should be left untouched).
