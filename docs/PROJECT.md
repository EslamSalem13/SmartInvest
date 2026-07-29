# SmartInvest — Project Reference

> A standalone reference for the SmartInvest project. Written so that any developer (or AI assistant) can start working on this codebase with **zero prior context** and without needing to read any past conversation.
>
> **Last updated**: 29 July 2026 — state at commit `9483917` on `main`.
>
> Keep this file updated whenever something structural changes: architecture, roles/permissions, the API surface, or the data model.

---

## 1. Overview

SmartInvest manages the **investment development plan for Menoufia Governorate**. It handles:

- Tracking programs and projects (main and sub) across financial years.
- Sub-project approval workflow (approve / cancel approval with a written reason).
- Assigning contractors and executive agencies to projects.
- Archiving printable "plans" (suggested / approved).

**Language**: The UI is entirely Arabic (RTL — `<html lang="ar" dir="rtl">`). Code identifiers are English; the few inline comments are Arabic.

**Current phase scope**: Planning department only. The `ExecutiveAgency` and `Contractor` roles exist in the backend and have login accounts, but **there are no dedicated frontend pages for them yet**.

---

## 2. Tech Stack

| Layer | Technologies |
|---|---|
| **Backend** | .NET 10, Onion architecture, EF Core + SQL Server, ASP.NET Core Identity + JWT, AutoMapper (partial), FluentValidation |
| **Frontend** | Angular 21 (standalone components, no NgModules), Signals for state, `FormsModule` with `[ngModel]`/`(ngModelChange)` (**no Reactive Forms**) |
| **Database** | Local SQL Server — `Server=.;Database=SmartInvestDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True` |

### Folder Layout

```
InvestmentPlan/
├── Backend/src/
│   ├── SmartInvest.Domain/          # Entities, enums, Roles, repository contracts
│   ├── SmartInvest.Application/     # DTOs, services, validators, interfaces, exceptions
│   ├── SmartInvest.Infrastructure/  # AppDbContext, migrations, repositories, IdentityService
│   └── SmartInvest.API/             # Controllers, Program.cs, middleware, CurrentUserService
├── Frontend/
│   ├── public/                      # Static assets (menoufia-emblem.svg, favicon.ico)
│   └── src/app/
│       ├── core/                    # guards, interceptors, models, services
│       ├── features/                # Pages (home, dashboard, projects, users, plans)
│       └── layout/main-layout/      # App shell (sidebar + topbar + router-outlet)
├── docs/PROJECT.md                  # This file
└── .claude/launch.json              # Dev server configs (untracked in git)
```

### Backend Layers (Onion)

- **`SmartInvest.Domain`** — Entities, enums, `Common/Roles.cs` (role constants), `IGenericRepository<T>` / `IUnitOfWork` (contracts only, no implementations).
- **`SmartInvest.Application`** — DTOs, services (business logic), validators, interfaces, and `Common/Exceptions`: `NotFoundException`, `BusinessRuleException`, `ForbiddenAccessException`.
- **`SmartInvest.Infrastructure`** — `AppDbContext`, migrations, repository implementations, `Identity/IdentityService.cs`, `DependencyInjection.cs`.
- **`SmartInvest.API`** — Controllers, `Program.cs` (registration + pipeline + data seeding), `Middleware/ExceptionHandlingMiddleware` (maps exceptions to correct HTTP status codes — **must stay registered**), `Common/CurrentUserService` (reads claims from the JWT), `Common/SuperAdminAuthorizationHandler`.

### ⚠️ Two Coexisting Data-Access Patterns (intentional)

Two patterns run side by side in this codebase:

1. **The general pattern (most of the project)** — `IGenericRepository<T>` (`GetByIdAsync`, `FindAsync`, `AddAsync`, `Update`, `Remove`) + `IUnitOfWork.SaveChangesAsync()`, with the service building the DTO **by hand** (no AutoMapper).
   Examples: `FinancialYearService`, `SubProjectService`, `ExecutiveAgencyService`.

2. **The Plan/Program pattern** — dedicated repositories (`IPlanRepo`, `IProgramRepo`) with purpose-specific methods + **AutoMapper** (`PlansAndPrograms` profile). Note that `PlanService`/`PlansController` sometimes return the raw entity rather than a DTO (e.g. `AddPlan` returns `Plan` directly).

**Rule**: If you're adding to the Plan/Program area, follow the AutoMapper pattern. Anywhere else, follow the general pattern. **Do not try to unify the two without an explicit request** — both work and serve different areas.

---

## 3. Data Model

### Project Hierarchy

```
MainProgram → SubProgram → MainProject → SubProject
```

Each `SubProject` links to:
- `Markaz` (district) — which links to `Governorate` and `Village`.
- `ProjectPriority` and `ProjectStatus`.
- `ExecutiveAgency` — **optional** (`ExecutiveAgencyId` is nullable).
- Collections: `ProjectSpecification` (technical specs), `ProjectAssignment`, `PlanProject`, `SubProjectFinancialYear`.

### Key `SubProject` Fields

| Field | Note |
|---|---|
| `SubProjectCode` | Project code, nullable |
| `BankFunding` + `SelfFunding` | `decimal(18,2)` |
| `TotalCost` | **`[NotMapped]`** — computed as `BankFunding + SelfFunding`, not a DB column |
| `IsApproved` | Approval state |
| `ApprovedAt` | Approval timestamp, nullable |
| `ApprovalCancellationReason` | Reason for cancelling approval (max 1000 chars), nullable |
| `ApprovalCancelledAt` | Cancellation timestamp, nullable |
| `Latitude` / `Longitude` | Coordinates, nullable |

> **Important historical note**: Before `IsApproved` existed, the code used the presence of `SubProjectCode` as an implicit proxy for "approved". That convention is **stale — do not follow it**. Always check `IsApproved` explicitly.

### Financial Years

`FinancialYear` (`Name`, `StartDate`, `EndDate`, `IsClosed`, optional `Budget`) ↔ `SubProject` via the **`SubProjectFinancialYear`** join table (many-to-many — a sub-project can span multiple financial years).

### Plans (archive)

`Plan` (`Name`, `StartDate`, `EndDate`, `PlanStatus` enum: `Suggested`/`Approved`, `SuggestionDate`, nullable `ApprovalDate`, linked to a `FinancialYear`) ↔ `SubProject` via **`PlanProject`**.

A plan is **archive-only** — created from a print button on the projects page. There is **no standalone plan list/management page**.

### Contractor Assignment

- `ExecutiveAgency` and `Contractor` are independent entities, each tied to a login account (`ApplicationUser`) **one-to-one**.
- `ProjectAssignment` links a `SubProject` to a `Contractor` (optional) via a `ContractType`.
- `ProjectAssignment.IsLocked` — set **automatically** when a sub-project's executive agency changes. Locked assignments can only be edited by `PlanningManager` or `SuperAdmin`.
- `ProjectAssignmentChangeRequest` — requests to modify an existing assignment.

### Entities Without a UI

- **`ProjectFollowUp`** (progress / financial disbursement percentage, linked to `SubProjectFinancialYear`) — backend is ready, **no frontend** (out of current phase scope).
- **`Notification`** — an **orphan** entity: the file exists but there is **no `DbSet` in `AppDbContext`**, and no controller or service. The 🔔 bell icon in the UI is decorative only.
- **`ProjectAttachment`**, **`DelayReason`**, **`AuditLog`** — present (the last one has a controller).

---

## 4. Roles and Permissions

Five roles, defined in `Backend/src/SmartInvest.Domain/Common/Roles.cs`:

| Role | Description |
|---|---|
| `SuperAdmin` | Full access to everything (see mechanism below) |
| `PlanningManager` | Approves projects and plans, creates accounts |
| `PlanningEmployee` | Standard CRUD operations |
| `ExecutiveAgency` | One account per agency; can create contractor accounts |
| `Contractor` | One account per contractor |

### Grouped Constants (for `[Authorize(Roles = ...)]`)

```csharp
Roles.PlanningStaff     = "PlanningEmployee,PlanningManager"
Roles.StaffAndAgency    = "PlanningEmployee,PlanningManager,ExecutiveAgency"
Roles.ManagerAndAgency  = "PlanningManager,ExecutiveAgency"
Roles.AssignmentParties = "PlanningEmployee,PlanningManager,ExecutiveAgency,Contractor"
```

### SuperAdmin Mechanism

`SuperAdminAuthorizationHandler` (in `SmartInvest.API/Common/`, registered in `Program.cs` via `AddSingleton<IAuthorizationHandler, ...>`) extends `AuthorizationHandler<RolesAuthorizationRequirement>` and calls `context.Succeed(requirement)` when the user is in the `SuperAdmin` role.

Result: **any `SuperAdmin` user passes every `[Authorize(Roles=...)]` check across the entire project automatically** — no need to add `SuperAdmin` manually to each controller or action.

> ⚠️ **Critical exception**: This mechanism applies to the **backend only**. The **frontend route guards** (`roleGuard([...])` in `app.routes.ts`) are a completely separate list and require `Roles.SuperAdmin` to be added **manually**. If you forget, a SuperAdmin will log in successfully but the router will refuse to route them, producing an **infinite redirect loop** (the screen appears frozen). This bug occurred in practice and has been fixed.

### ⚠️ Critical: Class-level + Method-level `[Authorize]` Composition

`[Authorize(Roles=X)]` on the controller and `[Authorize(Roles=Y)]` on the action **intersect (AND), they do not union (OR)**.

So: class `[Authorize(Roles = Roles.PlanningStaff)]` + method `[Authorize(Roles = Roles.ExecutiveAgency)]` = **nobody can access it** (the two sets don't intersect).

**Project convention**: the class always carries a plain `[Authorize]` **with no roles**, and every action declares its own role list explicitly.

This has bitten us more than once — **read the whole controller before adding or changing any permission**.

### Who Can Create Whom

| Creator | Can create |
|---|---|
| `SuperAdmin` | Any account, **including `PlanningManager`** |
| `PlanningManager` | `PlanningEmployee`, `ExecutiveAgency`, `Contractor` — **not `PlanningManager`** |
| `ExecutiveAgency` | `Contractor` only |

- The `PlanningManager` restriction is enforced in `IdentityService.CreateEmployeeAsync` (throws `ForbiddenAccessException`), and the option is hidden in the UI behind `@if (isSuperAdmin())`.
- `ExecutiveAgency`/`Contractor` accounts are created **together with** their entity in the same request. **Deleting the entity cascades to delete its account.**
- **There is no self-registration at all** — every account is created manually by someone with permission. This is **intentional**: an internally-managed system, not exposed to a public network.

---

## 5. API Surface

Base URL: `https://localhost:7250/api`

| Controller | Responsibility |
|---|---|
| `AuthController` | Login, change password — **no Register endpoint** |
| `UsersController` | CRUD for `PlanningEmployee`/`PlanningManager` accounts (activate/deactivate/reset password) |
| `MainProjectsController` | CRUD for main projects |
| `SubProjectsController` | CRUD for sub-projects + approval (details below) |
| `SubProjectFinancialYearsController` | Link/unlink a sub-project to a financial year — `api/subprojects/{id}/financial-years` |
| `FinancialYearsController` | CRUD for financial years |
| `PlansController` | CRUD for plans + `GET Current` + add project (new/existing) + approval |
| `ProgramController` | Read main/sub programs |
| `LookupsController` | Lookup lists (priorities, statuses, districts, villages, governorates) |
| `ExecutiveAgenciesController` | CRUD for executive agencies (+ their login account) |
| `ContractorsController` | CRUD for contractors — `Create` allowed for `ManagerAndAgency` |
| `ContractTypesController` | CRUD for contract types |
| `ProjectAssignmentsController` | Assign a contractor to a sub-project + change requests |
| `ProjectSpecificationsController` | Technical specifications for a sub-project |
| `AuditLogsController` | Change log |

### `SubProjectsController` — Detail

| Verb + Route | Roles | Note |
|---|---|---|
| `GET /api/subprojects` | any authenticated | `Search` — supports a `financialYearId` filter and pagination |
| `GET /api/subprojects/{id}` | any authenticated | |
| `POST /api/subprojects` | any authenticated | |
| `PUT /api/subprojects/{id}` | any authenticated | |
| `PUT /api/subprojects/{id}/executive-agency` | `PlanningStaff` | Changing it locks existing assignments |
| `PUT /api/subprojects/{id}/approve` | `PlanningManager` | Double-approve returns **400** |
| `PATCH /api/subprojects/{id}/cancel-approval` | `PlanningManager` | **`PATCH`, not `PUT`** — requires a reason |
| `DELETE /api/subprojects/{id}` | `PlanningManager` | |

### `PlansController` — Detail

`GET /api/plans` · `GET /{id}` · `GET /Current` · `POST` · `PUT /{id}` · `DELETE /{id}` · `POST /{planId}/newProject` · `POST /{planId}/existingProject/{projectId}` · `DELETE /{planId}/projects/{projectId}` · `PUT /{id}/approve` (`PlanningManager` only — takes the approval date from the caller).

---

## 6. Frontend

### Routing (`app.routes.ts`)

| Route | Guard | Note |
|---|---|---|
| `/` | none | Combined identity + login screen |
| `/login` | — | `redirectTo: ''` |
| `/app/*` | `authGuard` | Main shell |
| `/app/dashboard` | `roleGuard([PlanningManager, SuperAdmin])` | |
| `/app/projects` | `authGuard` only | The primary working page |
| `/app/projects/:id` | `authGuard` only | Sub-project details |
| `/app/users` | `roleGuard([PlanningManager, SuperAdmin])` | |
| `/app/plans/:id` | `authGuard` only | Plan print page |
| `**` | — | `redirectTo: ''` |

> **Don't forget**: every new `roleGuard` must include `Roles.SuperAdmin` explicitly (see the Roles section).

### Existing Pages

`home` · `dashboard` · `projects` (+ `projects/:id`) · `users` · `plans/:id`

**There are still no dedicated pages for the `ExecutiveAgency` / `Contractor` roles.**

### The `home` Page — Combined Screen

The landing page and login are **merged into a single screen**. Structure:

- Top bar (`.topbar`) — SmartInvest wordmark.
- Official emblem — `<img src="/menoufia-emblem.svg">` (a real SVG file in `Frontend/public/`, **not hand-drawn inline SVG**).
- Titles (`.titles`) — republic line / decorative divider / system name / phase.
- Login card (`.lcard`).

**Implementation details that matter**:

- Layout is **vertical, in normal document flow** (`.hero` is a flex column). **Do not use `position:absolute` + `transform:scale`** for layout here — the previous version did, and it broke (elements overlapping) at any unexpected window height.
- Emblem size: `clamp(180px, 26vw, 340px)`.
- **Field focus**: the `<div class="box">` wrapping each `<input>` has `(click)="uInput.focus()"` plus `cursor: text`. Without these, clicking the padding around the input does not focus it.
- **Autofill background fix**: Chrome forces a white background on inputs filled from a saved password. The fix lives in `.box input:-webkit-autofill` (`-webkit-text-fill-color` + inset `box-shadow` + a very long `transition`).
- The decorative divider (`.divider`) uses `::before` (the line) + `::after` (the ◆ diamond) **inside the element's bounds**. The old version used `top: -11px`, which inflated page height by 18px and caused a scrollbar.
- **Deliberately removed** (at the user's request): the flying-doves animation, the glowing ring behind the emblem, and the floating background particles (18 animated elements — a performance drag).
- Remaining animation: `.reveal` (one-shot staggered fade-in) and `.spinner`.

### Frontend Conventions

- **Shared CSS** lives in `Frontend/src/styles.css`: `si-btn`, `si-modal`, `si-overlay`, `si-grid`, `si-fld`, `si-err`, `si-x`. **Use these instead of redefining equivalents.**
- **`AuthService.isManager`** = `PlanningManager` **or** `SuperAdmin`. Use it for UI gating — **do not check `role() === 'PlanningManager'` directly**.
- **`AuthService.isSuperAdmin`** for SuperAdmin-exclusive UI.
- **`homeRouteForRole(role)`** returns `/app/dashboard` for `PlanningManager`/`SuperAdmin`, `/app/projects` otherwise.
- **Signals** for state — not RxJS state management, not plain getters.
- **Per-component CSS budget** (`angular.json` → `budgets` → `anyComponentStyle`): warning at **4kB**, error at **12kB**. Several files currently exceed the warning threshold (non-blocking): `projects.css`, `sub-project-details.css`, `users.css`.
- Arabic role labels are duplicated in two places — `main-layout.ts` (`roleLabel` computed) and `users.ts` (`roleLabel(role)` method). **Adding a new role means updating both.**

---

## 7. Seeded Accounts

Seeded automatically on API startup if they don't already exist (`Program.cs`):

| Account | Username | Email | Password | Role |
|---|---|---|---|---|
| Super admin | `superadmin` | `superadmin@gmail.com` | `SuperAdmin@123` | `SuperAdmin` |
| Default admin | `admin` | `admin@gmail.com` | `Admin@123` | `PlanningManager` |

> These are local development credentials. **They must be changed before any real deployment.**

Also seeded when their tables are empty: `Governorate`, `MainPrograms`, `ProjectPriority`, `ProjectStatus`.

---

## 8. Running Locally

### Prerequisites
- .NET 10 SDK
- Node.js + npm
- Local SQL Server (default instance `.`)

### Backend

```bash
cd Backend/src/SmartInvest.API && dotnet run --launch-profile https
```

> ⚠️ **The `--launch-profile https` flag is required.** The default `http` profile binds only to port 5187, but the frontend expects HTTPS on **7250** — without this flag every frontend request fails.

Serves on `https://localhost:7250` (and `http://localhost:5187`).

**Swagger UI** is available at `https://localhost:7250/swagger` — the fastest way to explore the API and try endpoints without the frontend.

### Frontend

```bash
cd Frontend && npm install && npx ng serve
```

Serves on `http://localhost:4200`.

> ⚠️ **Port 4200 is mandatory** — backend CORS is configured for exactly `http://localhost:4200` (`appsettings.json` → `Cors:AllowedOrigins`). Running on a different port gets requests rejected. If the port is occupied, kill the process holding it rather than changing the port.

### Migrations

```bash
cd Backend/src/SmartInvest.API && dotnet ef database update --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

> The `--project` / `--startup-project` flags are required because the `dotnet-ef` tool version differs from the SDK version.

**Current migrations** (only two — the history was squashed):
1. `20260728031443_IRebuild_EF_Core_migrations_from_current_model`
2. `20260728121535_AddFinancialYearBudget`

### Dev Servers via Claude Code

`.claude/launch.json` defines two configs: `frontend-dev` (port 4200) and `backend-api` (port 7250). Start them with `preview_start` by name — **never launch a dev server through Bash**.

---

## 9. ⚠️ Migrations — Danger Zone

The migration history has been **merged, rebased, and squashed multiple times** due to conflicts between team members' branches. Two real failures occurred; know both.

### Problem 1: Broken Chronological Ordering

When two branches each add migrations, a git merge preserves all the files but **does not fix their ordering**. The result: an older-timestamped migration tries to alter a table that doesn't exist yet, because the migration that creates it has a newer timestamp.

**Symptom**: `dotnet ef database update` fails with `Cannot find the object "X" because it does not exist or you do not have permissions` (SQL Error 4902).

**Diagnosis**: run `dotnet ef database update --verbose` — it shows exactly which SQL statement in which migration failed.

### Problem 2: The Lying Snapshot

`AppDbContextModelSnapshot.cs` is a text file, so git auto-merges it. A merge can leave the snapshot claiming a column exists when **no migration actually creates it**. The result: newly generated migrations come out **empty** (they diff against the wrong snapshot).

### 🔬 Verification Technique: The Empty Probe Migration

After **any** merge or migration edit, run:

```bash
dotnet ef migrations add ProbeCheck --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

- If `Up()` and `Down()` are **both empty** → the snapshot matches the model ✅ — delete the probe files.
- If they contain any operations → **the snapshot is wrong**. Restore a known-good version (`git show <commit>:<path>`) and regenerate the migration.

**Never assume the snapshot is correct just because the build succeeds.**

---

## 10. Repository State (Git)

- **Current branch**: `main`
- **Latest pushed commit**: `9483917` — a merge reconciling work from three sources (the user's work, Marwa's Plan work, and ahmedshalaby03's sub-project approval workflow).
- **`main` is in sync with `origin/main`** (no unpushed commits).

### Branches

| Branch | Status |
|---|---|
| `main` | ✅ Primary branch, up to date and pushed, contains everything |
| `feature/plan-mine-final` | Fully merged into `main` — archival |
| `feature/financial-year-frontend` | Old, merged — archival |
| `backup/financial-year-frontend-pre-rebase` | Safety copy taken before a rebase — for comparison only |
| `origin/feature/agency-contractor-assignment` | Remote branch belonging to another team member |
| `origin/feature/financial-management` | Remote branch belonging to another team member |

**Start any new work from `main`.**

### ⚠️ Uncommitted Changes at Time of Writing

```
 D Frontend/public/menofialogo.png                     (removed unused legacy asset)
 M Frontend/src/app/app.routes.ts                      (added SuperAdmin to route guards)
 M Frontend/src/app/features/home/home.css             (layout rebuild + autofill fix)
 M Frontend/src/app/features/home/home.html            (real SVG emblem + click-to-focus)
 M Frontend/src/app/features/home/home.ts              (removed dove animation logic)
 M Frontend/src/app/layout/main-layout/main-layout.ts  (SuperAdmin role label)
?? Frontend/public/menoufia-emblem.svg                 (new emblem asset)
?? .claude/                                            (local tooling config)
```

This work is **tested and builds cleanly** but is **not yet committed**. It should be committed before anything else.

---

## 11. Known Issues and Gaps

| # | Issue |
|---|---|
| 1 | **Plan DTOs are incomplete**: `PlanInfoDto` and `PlanWithoutProjectsDto` lack `PlanId`, `FinancialYearId`/name, and `SuggestionDate`. The print page compensates by showing `StartDate`–`EndDate` instead of the financial year name, and omits the suggestion date. |
| 2 | **Fake pagination**: print buttons on the projects page fetch with `pageSize: 5000`. Adequate at current data volume, **not a real solution**. |
| 3 | **`Notification` is an orphan entity** — no `DbSet`, controller, or service. The bell icon is decorative. |
| 4 | **No PDF library** — printing relies on the browser's `window.print()`. |
| 5 | **`ProjectFollowUp` has no UI** — backend is ready, no pages built. |
| 6 | **No pages for `ExecutiveAgency`/`Contractor` roles** — they can log in and use the API, but there's no UI for them. |
| 7 | **CSS budget warnings** — `projects.css` (7.3kB), `sub-project-details.css` (5.1kB), `users.css` (4.3kB) exceed the 4kB warning threshold. Warnings only; they don't fail the build. |
| 8 | **EF warning** — `ProjectFollowUp.ProgressPercentage` is a decimal with no precision/scale specified. Logged at startup; can silently truncate values. |
| 9 | **Exposed credentials** — seeded account passwords are hardcoded in `Program.cs`. Must be changed before deployment. |

---

## 12. Common Pitfalls (avoid these)

1. **Adding a new `roleGuard` without `Roles.SuperAdmin`** → infinite redirect loop (screen freezes after a successful login).
2. **Putting `[Authorize(Roles=...)]` on both the class and the method** → intersection (AND), not union (OR) — can lock everyone out.
3. **Assuming `AppDbContextModelSnapshot.cs` is correct after a merge** → use a probe migration.
4. **Running the backend without `--launch-profile https`** → the frontend can't reach it.
5. **Running the frontend on a port other than 4200** → CORS rejects it.
6. **Using `!!subProject.code` as a proxy for approval** → stale convention; use `IsApproved`.
7. **Using `PUT` instead of `PATCH` on `cancel-approval`** → 405.
8. **Unifying the two data-access patterns unprompted** → both are intentional.
9. **Using `position:absolute` + `scale` for the `home` page layout** → breaks at unexpected window heights.

---

## 13. Note on Conversation History

If you're reinstalling Claude Code: conversation transcripts live in
`C:\Users\Eslam\.claude\projects\D--InvestmentPlan\` (`.jsonl` files, currently ~15MB).

**This document is intended to replace the need to consult those transcripts** — every architectural decision and discovered pitfall is captured here. But if you want to keep the raw history, copy that folder before reinstalling.
