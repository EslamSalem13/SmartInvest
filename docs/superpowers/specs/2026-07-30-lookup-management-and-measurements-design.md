# Lookup Management + Custom Measurements — Design

> Date: 2026-07-30 | Branch: `main` | Status: Approved

## 1. Problem

Several reference lists this app depends on have no write path at all today:

- **Real DB tables, read-only API:** `MainProgram`, `SubProgram`, `Markaz`, `Governorate`, `Village`, `ProjectPriority`, `ProjectStatus` — all exposed only via `LookupsController` (GET-only). `ContractType` already has a full CRUD controller (`ContractTypesController`) but no frontend page.
- **Not even DB tables — hardcoded frontend string lists:** `ComponentType` (`sub-project-form.ts`'s `componentTypes` array) and `ProjectLevel` (a two-option inline `<select>`).
- **Free text, no list at all:** `AccountingUnit` on `SubProject` (currently just a plain string, defaulted to `''` and not even exposed in the create form).

Planning staff need to manage all of these themselves instead of requiring a code change every time a new Markaz, program, or component type is needed.

Separately, sub-projects need **custom measurements** — height, distance, counts, "and so on" — that vary per sub-program and can't be modeled as fixed columns. Staff need to define measurement types themselves and decide which sub-program(s) each one applies to (many-to-many), then record actual values per sub-project.

## 2. Lookup Management

### 2.1 Scope (confirmed with user)

All 11 of the following become manageable via CRUD, including converting the two hardcoded frontend lists and the free-text field into real DB-backed tables:

| Lookup | Current state | Parent |
|---|---|---|
| MainProgram | real table, read-only API | — |
| SubProgram | real table, read-only API | MainProgram |
| Governorate | real table, read-only API | — |
| Markaz | real table, read-only API | Governorate |
| Village | real table, read-only API | Markaz |
| ProjectPriority | real table, read-only API | — |
| ProjectStatus | real table, read-only API | — |
| ContractType | real table, full CRUD API, no frontend page | — |
| ComponentType | **new table** (currently hardcoded frontend list) | — |
| ProjectLevel | **new table** (currently hardcoded frontend list) | — |
| AccountingUnit | **new table** (currently free text) | — |

`ComponentType`/`ProjectLevel`/`AccountingUnit` become simple `{ Id, Name }` tables; `SubProject`'s corresponding columns switch from plain strings to FK ids (`ComponentTypeId`, `ProjectLevelId`, `AccountingUnitId`), each with a migration that creates the new table, seeds it from the distinct values already sitting in existing `SubProject` rows (so no existing data is orphaned), then converts the column.

### 2.2 Architecture — one generic system, not 11 bespoke pages (confirmed with user)

**Backend:** a single generic pattern reusing the existing `IGenericRepository<T>`:
- `ISimpleLookupService<TEntity>` with `GetAllAsync`/`CreateAsync(name)`/`UpdateAsync(id, name)`/`DeleteAsync(id)` — for the 8 flat (`{Id, Name}`-shaped) lookups: MainProgram, Governorate, ProjectPriority, ProjectStatus, ContractType, ComponentType, ProjectLevel, AccountingUnit.
- A parent-aware variant (`ISimpleChildLookupService<TEntity>`, adding a required parent id to create/update) for the 3 parent-child lookups: SubProgram (→MainProgram), Markaz (→Governorate), Village (→Markaz).
- One `SettingsController` (`api/settings/{type}`) with a fixed set of routes per lookup type (ASP.NET doesn't cleanly route to open-generic controllers, so the controller itself is concrete — but every action body is a 1-line delegation to the shared generic service, not per-type copy-pasted logic). `[Authorize(Roles = Roles.PlanningStaff)]` class-level, create/update/delete narrowed to `Roles.PlanningManager` (matching every other manager-gated mutation in this app).
- Delete guards: a lookup cannot be deleted while any row references it (same "cannot delete, X rows depend on it" pattern already used for Contractor/ExecutiveAgency deletion).

**Frontend:** one generic settings page at `/app/settings`, all-staff visible (nav entry `managerOnly: false`, matching Contractors/Agencies), with a sidebar/tab list of the 11 lookup types. A single reusable Angular component renders the table + create/edit `si-modal` for whichever type is selected, parameterized by `{ apiPath, label, hasParent, parentApiPath, parentLabel }` — for parent-child types the modal includes a parent-select dropdown; for flat types it's just a name field. Create/edit/delete gated behind `isManager()`, matching every other CRUD page in this app; all-staff can view.

## 3. Custom Measurements

### 3.1 Data model

```
Measurement            { Id, Name, Unit }                         -- e.g. "الارتفاع" / "متر"
MeasurementSubProgram   { Id, MeasurementId, SubProgramId }         -- many-to-many join
SubProjectMeasurementValue { Id, SubProjectId, MeasurementId, Value (decimal) }
```

- Value is **numeric only** (confirmed with user) — the unit is fixed on the `Measurement` definition itself, so a value is just a number in that unit.
- A `Measurement` can be linked to one or more `SubProgram`s; a `SubProgram` can have one or more `Measurement`s (confirmed many-to-many).
- A sub-project's *applicable* measurements are resolved via `SubProject → MainProject → SubProgram → (MeasurementSubProgram) → Measurement` — not a direct link on `SubProject` itself, since the applicability rule lives at the sub-program level.
- Deleting a `Measurement` is blocked while it has any recorded `SubProjectMeasurementValue` rows (same delete-guard convention as everywhere else in this app) or any `MeasurementSubProgram` links — staff must unlink it from every sub-program first, then it can be deleted.
- Recording a value for a given measurement on a given sub-project is optional — staff can leave any/all of them blank; nothing is required.

### 3.2 Management UI

A dedicated page (not folded into the fully generic settings pattern, since it needs the sub-program multi-link picker rather than a plain name field) — `/app/measurements`, all-staff visible, mutations manager-gated:
- Table: measurement name, unit, linked sub-programs (as a chip list), actions.
- Create/edit modal: name, unit, and a multi-select of sub-programs (checkboxes or a tag-style picker) to link.

### 3.3 Recording values

Added as **Step 4** to the existing sub-project creation/edit wizard (`sub-project-form.ts`, currently steps: 1 basic info, 2 sub-project data, 3 financial years):
- Once the sub-project's Main Project (and therefore its Sub Program) is known, fetch the measurements linked to that sub-program.
- If none are linked, this step shows nothing (or is skipped) — no dead UI for sub-programs with no custom measurements defined.
- Each linked measurement renders as one optional numeric input, labeled `{Name} ({Unit})`.
- Values save alongside the rest of the sub-project on submit (same request, or an immediate follow-up call — implementation detail for the plan stage).
- On edit, previously-recorded values are pre-filled; changing the sub-project's Main Project (and thus possibly its Sub Program) after values were recorded is an edge case the plan stage should handle explicitly (most likely: re-resolve the applicable measurement set and keep values only for measurements still applicable, since a value for a since-unlinked measurement no longer means anything in context).

## 4. Out of Scope

- Aggregating/reporting on measurement values (totals, charts) — just capture and display for now.
- Measurement value history/versioning — a value is overwritten on edit, no audit trail beyond whatever the app already does generally.
- Any change to `ContractTypesController`'s existing backend (already has full CRUD) beyond adding its frontend settings-page entry.

## 5. Testing

Manual, via dev servers (no test suite in this repo, per established convention):
- Each of the 11 lookup types: create, edit, delete (including the delete-guard when in use), and confirm parent-child types (SubProgram/Markaz/Village) correctly require and display their parent.
- Confirm the migration converting `ComponentType`/`ProjectLevel`/`AccountingUnit` from free-text to FK correctly seeds existing distinct values from current `SubProject` rows with no data loss (spot-check a few existing sub-projects' values are unchanged after migration).
- Create a measurement, link it to a sub-program, confirm it appears on Step 4 of the sub-project form only for sub-projects under that sub-program (not others).
- Record a value, save, re-open the sub-project, confirm the value is pre-filled.
- Unlink a measurement from a sub-program that has recorded values on some of its sub-projects, confirm those values are handled per the plan's chosen edge-case behavior (§3.3) without crashing.
- Attempt to delete a measurement still linked to a sub-program, and a lookup still referenced by existing rows — confirm both are blocked with a clear message.
