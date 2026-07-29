# Investment Plans Page + Home Redirect — Design

> Date: 2026-07-29 | Branch: `main` | Status: Approved

## 1. Problem

1. The projects page has two separate print buttons ("طباعة الخطة المقترحة" / "طباعة الخطة المعتمدة"). Each click silently creates a new archived `Plan` and jumps straight to the print view — there is no way to browse or filter plans already created. We want one entry-point button that leads to a proper list/browse page, while keeping the ability to generate new plan snapshots.
2. The home page always shows the login form, even for a user who already has a valid session. It should skip straight to their landing route.

## 2. Backend Changes

### 2.1 `PlanWithoutProjectsDto` (`Backend/src/SmartInvest.Application/DTOs/Plan/PlanWithoutProjectsDto.cs`)

Add fields so the frontend list can link to a plan and show its financial year:

```csharp
public int PlanId { get; set; }
public int FinancialYearId { get; set; }
public string FinancialYearName { get; set; } = string.Empty;
public DateTime SuggestionDate { get; set; }
```

### 2.2 `PlansAndPrograms` mapping profile

`CreateMap<Plan, PlanWithoutProjectsDto>()` needs:
```csharp
.ForMember(d => d.FinancialYearName, o => o.MapFrom(s => s.FinancialYear!.Name))
```
(`PlanId`, `FinancialYearId`, `SuggestionDate` map automatically by name.)

Drop `.ReverseMap()` concern: the reverse direction (`PlanWithoutProjectsDto` → `Plan`) is never actually used by the controller (`AddAndEditPlanInfoDto` is used for writes instead) — leave `.ReverseMap()` in place, unaffected.

### 2.3 `PlanRepo.GetPlanByStatusAndName` (`Backend/src/SmartInvest.Infrastructure/Repositories/PlanRepo.cs`)

Add `.Include(p => p.FinancialYear)` to the query — currently only includes `PlanProjects`, so `FinancialYear` would be null and break the new mapping.

## 3. Frontend Changes

### 3.1 `Plan` model (`Frontend/src/app/core/models/project.models.ts`)

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

### 3.2 New page: `Frontend/src/app/features/plans/plan-list.ts` / `.html` / `.css`

Route `/app/plans` (new — added to `app.routes.ts`, `authGuard` only, same access level as `/app/projects`). Registered before or after `plans/:id` — order doesn't matter, different segment count.

Responsibilities (moved verbatim from `projects.ts`, then extended):
- Financial-year selector (reuses `FinancialYearsService.getAll()`), defaulting to the most recent year — same logic as `projects.ts` today.
- "خطة مقترحة جديدة" button → same flow as today's `printSuggested`/`addAllSuggested`: fetch subprojects for the selected year (`pageSize: 5000`), create a `Suggested` plan, attach all subprojects, navigate to `/app/plans/:id`.
- "خطة معتمدة جديدة" button (`isManager()`-gated) → opens the existing approved-date modal, then runs the current `confirmApprovedPrint`/`addAllThenApprove` flow (create plan, attach only approved subprojects, call `PlansService.approve`, navigate to print page).
- Segmented filter: الكل / معتمدة / بانتظار الاعتماد → calls `PlansService.getAll(planStatus)` with `undefined` / `'Approved'` / `'Suggested'`.
- Cards grid below: one card per plan — name, `financialYearName`, status badge, `startDate`–`endDate`, `suggestionDate` (+ `approvalDate` if approved). Whole card is a `routerLink` to `/app/plans/{{planId}}`.
- Reload the plans list after generating a new plan (in addition to navigating to print).

Error handling: reuse the existing `alert()` pattern already used throughout `projects.ts` — no new pattern introduced.

### 3.3 `projects.ts` / `projects.html`

Remove (relocated to `plan-list.ts`):
- Signals: `printing`, `showApprovedDateForm`, `approvedDate`.
- Methods: `printSuggested`, `addAllSuggested`, `openApprovedPrint`, `closeApprovedPrint`, `confirmApprovedPrint`, `addAllThenApprove`.
- The approved-date modal block in the template.
- The two print buttons in the toolbar.

Add: one button, `routerLink="/app/plans"`, label "الخطط الاستثمارية", visible to all (no `isManager` gate on the button itself — the manager-only gate moves inside `plan-list`).

Untouched: sub-project approval/cancel-approval (`showApprovalModal`, `showCancelApprovalModal`, etc.) — unrelated feature, stays as-is.

### 3.4 Home redirect

New guard in `Frontend/src/app/core/guards/auth.guard.ts`:

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

Applied to the `''` route in `app.routes.ts` as `canActivate: [guestGuard]`. Runs before `Home` renders — no login-page flash for an already-authenticated user.

## 4. Out of Scope

- No delete/edit action on plan cards (not requested).
- No change to `plan-print.ts`/`.html` (print page itself untouched).
- No change to the "current plan" (`GET /Current`) concept.

## 5. Testing

Manual, via the dev servers:
- Login as `PlanningEmployee` → projects page shows single "الخطط الاستثمارية" button → navigates to `/app/plans` → only "خطة مقترحة جديدة" visible, no approved-generate button.
- Login as `PlanningManager`/`SuperAdmin` → both generate buttons visible on `/app/plans`.
- Generate a suggested plan → appears in list, filter tabs correctly include/exclude it, card navigates to its print page with correct content.
- Generate an approved plan → same, shows "معتمدة" badge.
- Visit `/` while already logged in → immediately redirected to `/app/dashboard` (manager) or `/app/projects` (others), no login form flash.
- Visit `/` while logged out → login form shows as before.
