# Agencies & Contractors as Profiles (No Login) — Design

> Date: 2026-07-30 | Branch: `main` | Status: Approved

## 1. Problem

`ExecutiveAgency` and `Contractor` are currently full login roles (own `ApplicationUser` account, own JWT role, self-service assignment/change-request workflow). In practice they should be plain data profiles: records that a `PlanningManager`/`PlanningEmployee` creates and manages on the agency's/contractor's behalf. Nobody representing an agency or contractor logs into this system themselves.

This design converts both entities into pure profiles: removes their login/role capability, removes the self-service change-request workflow that only made sense when they could log in, adds a way to see which sub-projects a contractor/agency is currently tied to, and swaps the projects table's "جهة التنفيذ" column for a "المقاول" (contractor) column.

## 2. Backend — Strip Login Capability

### 2.1 `Roles.cs` (`Backend/src/SmartInvest.Domain/Common/Roles.cs`)

Delete `ExecutiveAgency`, `Contractor` constants and the grouped constants that reference them: `StaffAndAgency`, `ManagerAndAgency`, `AssignmentParties`.

Every `[Authorize(Roles = ...)]` using a deleted grouped constant is updated call-by-call to whatever the group reduces to once the agency/contractor part is removed:
- `Roles.StaffAndAgency` (`"PlanningEmployee,PlanningManager,ExecutiveAgency"`) → `Roles.PlanningStaff` (`"PlanningEmployee,PlanningManager"`).
- `Roles.ManagerAndAgency` (`"PlanningManager,ExecutiveAgency"`) → `Roles.PlanningManager`.
- `Roles.AssignmentParties` (all four) → `Roles.PlanningStaff`.

Affected files (found via `grep -rln "Roles\.ExecutiveAgency\|Roles\.Contractor\|StaffAndAgency\|ManagerAndAgency\|AssignmentParties"`):
- `Backend/src/SmartInvest.API/Controllers/ContractorsController.cs`
- `Backend/src/SmartInvest.API/Controllers/ProjectAssignmentsController.cs`
- `Backend/src/SmartInvest.API/Program.cs`
- `Backend/src/SmartInvest.Application/Services/ChangeRequestService.cs` (deleted whole, see §3)
- `Backend/src/SmartInvest.Application/Services/ProjectAssignmentService.cs`
- `Backend/src/SmartInvest.Infrastructure/Identity/IdentityService.cs`

### 2.2 `ApplicationUser` (`Backend/src/SmartInvest.Infrastructure/Identity/ApplicationUser.cs`)

Remove `ExecutiveAgencyId`/`ExecutiveAgency` nav and `ContractorId`/`Contractor` nav. Requires a migration dropping those two FK columns (+ their indexes) from `AspNetUsers`. Follow the project's documented migration procedure: generate, inspect the generated `Up()`/`Down()` for anything unexpected (e.g. an index that needs an explicit drop first, as happened with the `MainProjectCode` filtered index), apply, then run the empty-probe-migration check.

### 2.3 `IdentityService` (`Backend/src/SmartInvest.Infrastructure/Identity/IdentityService.cs`)

Delete: `CreateAgencyUserAsync`, `CreateContractorUserAsync`, `GetUserByExecutiveAgencyIdAsync`, `GetUserByContractorIdAsync`, `ResetPasswordForAgencyAsync`, `ResetPasswordForContractorAsync`, `DeleteUserByExecutiveAgencyIdAsync`, `DeleteUserByContractorIdAsync`, and the `executiveAgencyId`/`contractorId` JWT claim lines. Remove the corresponding method signatures from `IIdentityService`.

### 2.4 `Program.cs`

`string[] roles = { Roles.SuperAdmin, Roles.PlanningEmployee, Roles.PlanningManager, Roles.ExecutiveAgency, Roles.Contractor };` → drop the last two.

### 2.5 `ExecutiveAgencyService` / `ContractorService`

- `CreateAsync`: stop calling `_identityService.CreateAgencyUserAsync`/`CreateContractorUserAsync`. Drop `UserName`/`Password` from `CreateExecutiveAgencyDto`/`CreateContractorDto`.
- `DeleteAsync`: stop calling `DeleteUserByExecutiveAgencyIdAsync`/`DeleteUserByContractorIdAsync`.
- `ResetPasswordAsync` + its interface method: deleted (nothing to reset). `ExecutiveAgenciesController`/`ContractorsController`: delete the `PUT .../{id}/reset-password` endpoint on both.
- `MapWithUserAsync` → replaced by a plain `_mapper.Map<...Dto>(entity)`; drop `UserName` from both DTOs. `ExecutiveAgencyDto.IsActive` currently comes from the login user (`user?.IsActive ?? false`) — since there's no more login user, `ExecutiveAgency` entity gains its own `IsActive` column (default `true`, mirroring `Contractor.IsActive` which already exists as a real column). Needs adding to `Backend/src/SmartInvest.Domain/Entities/ExecutiveAgency.cs` + migration.
- Controller class-level `[Authorize(Roles = Roles.StaffAndAgency)]` → `Roles.PlanningStaff`. `Create`/`Update`/`Delete` action-level attributes keep requiring `PlanningManager` (per the existing convention — already `PlanningManager`-only on `ExecutiveAgenciesController`; `ContractorsController.Create` currently allows `ManagerAndAgency`, becomes `PlanningManager`-only since the agency-self-service path is gone).

## 3. Backend — Remove the Change-Request Workflow

Deleted entirely (per approved decision — it only made sense when agencies/contractors logged in to propose/respond to their own assignment changes):
- `Backend/src/SmartInvest.Application/Services/ChangeRequestService.cs`
- `Backend/src/SmartInvest.Application/Interfaces/IChangeRequestService.cs`
- Its DTOs (`CreateChangeRequestDto`, `ReviewChangeRequestDto`, `ChangeRequestDto`, wherever declared)
- The `ProjectAssignmentChangeRequest` entity, its `DbSet` in `AppDbContext`, and its EF configuration
- The five `change-requests`-related actions on `ProjectAssignmentsController` (`GetChangeRequests`, `SubmitChangeRequest`, `ApproveChangeRequest`, `RejectChangeRequest`, plus the route grouping around them)
- `ProjectAssignmentService.EnsureAgencyOwnership` (already a no-op for every role except `ExecutiveAgency`, which no longer exists as a login role) and its call sites in `GetBySubProjectAsync`/`CreateAsync`/`UpdateGeneralAsync`

Needs a migration dropping the `ProjectAssignmentChangeRequests` table.

`ProjectAssignmentsController`'s remaining actions (`GetAll`, `Create`, `Update`, `Delete`) keep their current role level minus the agency part: `StaffAndAgency` → `PlanningStaff`, `Delete` stays `PlanningManager`-only (unchanged).

## 4. Backend — "Which Sub-Projects Does X Hold"

- `ExecutiveAgencyDto` and `ContractorDto` (both only on the single-item `GetById` fetch, not the list) gain:
  ```csharp
  public List<AssignedSubProjectDto> AssignedSubProjects { get; set; } = new();
  ```
  ```csharp
  public class AssignedSubProjectDto
  {
      public int Id { get; set; }
      public string Name { get; set; } = string.Empty;
      public string MainProjectName { get; set; } = string.Empty;
  }
  ```
  - Agencies: sourced from `SubProject` where `ExecutiveAgencyId == id`.
  - Contractors: sourced from `ProjectAssignment` where `ContractorId == id` (→ `.SubProject`). No "current vs historical" flag exists on assignments beyond `IsLocked` (which means something else — locked-for-editing, not inactive), so a contractor's list shows every sub-project it has ever had an assignment row for.

- `SubProjectListItemDto` gains `public string? ContractorName { get; set; }`, resolved from that sub-project's most recent `ProjectAssignment` (`OrderByDescending(AssignmentDate)`, first or default, null if none). `ExecutiveAgencyName` is untouched — a sub-project's own agency link is a separate, still-valid concept.

## 5. Frontend — Two New Pages

Both follow the existing Users page shape (list table + create/edit `si-modal` form), visible to all planning staff (`authGuard` only, no role restriction — matches "All-staff" decision); create/edit/delete stay `isManager()`-gated inside the page, same convention as every other CRUD page in this app.

### 5.1 Contractors (`/app/contractors`)

- List: contractor name, company type, category, phone, active (toggle/badge).
- Create/edit form: name, company type, national ID / commercial register, phone, email, address, category, active — no username/password fields (removed with the login).
- Detail: clicking a row toggles an inline expansion beneath it (same interaction as the projects table's main-row accordion — no new route) showing assigned sub-projects, each as a link to `/app/projects/:id` with its parent main project name alongside.

### 5.2 Executive Agencies (`/app/agencies`)

Same shape: name, phone, email, address, active; assigned sub-projects list (sourced from `SubProject.ExecutiveAgencyId` this time, not `ProjectAssignment`).

### 5.3 Nav

Two new items in `main-layout.ts`'s `allNav` list — "المقاولون" (`/app/contractors`) and "الجهات التنفيذية" (`/app/agencies`) — both `managerOnly: false` (all-staff visible, per the approved decision). Two new routes in `app.routes.ts`, `authGuard` only (matching `/app/projects`).

## 6. Frontend — Projects Table Column Swap

- `projects.html` header: "جهة التنفيذ" → "المقاول".
- Sub-rows: `{{ s.contractorName ?? 'غير مسند' }}` (replacing `{{ agencyOf(s.mainProjectId) }}`).
- Main rows: column left blank (contractors attach to sub-projects, not main projects — same reasoning already applied to leave المركز blank on main rows).
- `agencyOf()` helper in `projects.ts` and the `agencyByMain` computed become dead code once nothing calls them — removed.
- `MainProjectListItem.executingAgency` (the main project's own free-text agency field, `EXECUTING_AGENCIES` constant list) is untouched everywhere else it's used (main-project-form, advanced filter) — separate, legacy concept, explicitly out of scope.

## 7. Out of Scope

- Migrating `MainProject.ExecutingAgency`'s free-text field to the real `ExecutiveAgency` entity.
- Any change to the sub-project details page's existing `ProjectAssignment` create/edit UI beyond it continuing to work under the trimmed role set (no agency-ownership carve-out to preserve, since that carve-out is deleted per §3).
- A "current vs historical" flag on `ProjectAssignment` — out of scope; contractor's assigned-sub-projects list shows every assignment row that ever existed for them.

## 8. Migrations Needed (single migration, or a small ordered set)

1. Drop `AspNetUsers.ExecutiveAgencyId` / `ContractorId` columns (+ indexes).
2. Add `ExecutiveAgency.IsActive` (bool, default `true`).
3. Drop `ProjectAssignmentChangeRequests` table.

Follow `docs/PROJECT.md` §9's procedure exactly: generate, inspect the raw SQL for anything requiring manual ordering (index drops before column alters, as happened before), apply, empty-probe-migration verify, delete probe files.

## 9. Testing

Manual, via dev servers (no test suite in this repo, per established convention):
- Login seed accounts (`superadmin`, `admin`) unaffected — still work.
- Create a Contractor profile (no username/password fields in the form) → appears in `/app/contractors` list.
- Create an Executive Agency profile → appears in `/app/agencies` list.
- Assign a contractor to a sub-project (existing sub-project-details assignment flow) → contractor's profile page shows that sub-project; projects table's المقاول column shows the contractor's name on that sub-row.
- Projects table: main rows show blank المقاول column; sub-rows with no assignment show "غير مسند".
- Confirm `POST /api/subprojects/{id}/assignments/{id}/change-requests` (and siblings) return 404 (route gone), confirm no compile-time or runtime reference to deleted `Roles.ExecutiveAgency`/`Roles.Contractor` remains.
