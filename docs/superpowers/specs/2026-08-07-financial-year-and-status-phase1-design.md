# Phase 1: Financial-Year Budget UX, Project Type, and Stalled-Status — Design

## Goal

Four small, independent improvements requested as "Phase 1" of a larger roadmap (see [2026-08-07-project-tracking-phase3-design.md](./2026-08-07-project-tracking-phase3-design.md) for Phase 3, which depends on item 3 here). None of these require new tables — all extend existing entities/endpoints that are already mostly in place.

## Item 1 — Structured budget entry (مليار/مليون/ألف/جنيه)

**Problem:** `FinancialYear.Budget` is a single decimal typed as one big number (e.g. `8000000`) — easy to mistype a zero.

**Design:** On the add-year form (`Frontend/src/app/features/projects/projects.ts`/`.html`, `showAddYearForm`) and the new edit-year page (item 2), replace the single budget `<input type="number">` with four small number inputs — مليار / مليون / ألف / جنيه — that combine into one value on submit: `budget = billions*1_000_000_000 + millions*1_000_000 + thousands*1_000 + units`. Reverse the split when populating the edit form from an existing `FinancialYear.Budget` value (integer div/mod chain). No backend change — `CreateFinancialYearDto`/`UpdateFinancialYearDto` still just carry the combined decimal.

Existing `money()` display helper (already used throughout — `(value ?? 0).toLocaleString('en-US')`) is kept as-is for display; no new formatting utility needed.

## Item 2 — Edit financial year, as its own Settings page

**Already exists, unused:** `FinancialYearsController.Update` (backend) and `FinancialYearsService.update()` (frontend, `Frontend/src/app/core/services/financial-years.service.ts`) — both fully implemented, never called from any UI.

**Design:** New Settings card "السنوات المالية" in `Frontend/src/app/features/settings/settings-index.ts` (`cards` array), routed similarly to the existing lookup pages (`app.routes.ts`, under the `settings` children). New page lists all financial years (name, dates, budget, isClosed) and lets a manager click one to edit name/dates/budget (using item 1's split-input control)/isClosed via the existing `update()` call. **Creating** a new year stays exactly where it is today (Projects page toolbar, `showAddYearForm`) — this page is edit-only, per explicit scope decision.

Reuse the existing `settings-lookup-table`/`settings-lookup-page` list+edit UI pattern rather than inventing a new one, adapted for financial-year's richer field set (dates, budget, isClosed) instead of a plain name string.

## Item 3 — Project type (توريدات / مقاولات)

**Already exists, unused:** `SubProject.ProjectNature` (string column) — currently free text, never exposed in any create/edit UI (confirmed via grep — the sub-project-details page shows it read-only, currently always "—" since nothing ever sets it).

**Design:** Add a required dropdown "نوع المشروع" to `sub-project-form.ts` (create + edit), with exactly two options whose values are the literal strings `"توريدات"` and `"مقاولات"` (these exact strings are what Phase 3's execution-order branching will compare against — see Phase 3 spec). No migration — same existing column, just a constrained UI input plus basic required-field validation (`CreateSubProjectDto`/`UpdateSubProjectDto` already carry `ProjectNature: string`, no DTO shape change needed, just make it required in the form and reject empty/other values server-side).

## Item 4 — Mark an approved project as متعثر (stalled), with reactivate

**Root design decision (from audit):** `IsApproved` stays `true` when a project is marked stalled — it genuinely was approved, it just stopped progressing. Only `StatusId` changes to the متعثر `ProjectStatus` lookup value, plus a reason is recorded. This is intentionally a **separate action from `ApproveAsync`**, not a reuse of it.

**Audit findings that must be fixed as part of this item** (full audit context in conversation; codebase already has dead متعثر-handling code from a prior، removed feature that assumed `IsApproved` flips to `false` — it never does today):

- `Backend/src/SmartInvest.Application/Services/SubProjectService.cs` — add two new methods, `MarkStalledAsync(id, reason)` and `ReactivateAsync(id)`, both manager-only (same authorization pattern as `ApproveAsync`). `MarkStalledAsync` sets `StatusId` → متعثر lookup id, `ApprovalCancellationReason` = reason, `ApprovalCancelledAt` = now. `ReactivateAsync` sets `StatusId` back to the normal in-progress status ("قيد التنفيذ"), clears `ApprovalCancellationReason`/`ApprovalCancelledAt`. **Does not touch `IsApproved`** in either direction.
- New endpoints on `SubProjectsController`: `PUT /api/subprojects/{id}/mark-stalled` (body: reason) and `PUT /api/subprojects/{id}/reactivate`.
- `Frontend/src/app/features/projects/details/sub-project-details.html` — the status-pill logic (`@if isApproved → معتمد @else if statusName==='متعثر' → متعثر`) must be **reordered**: check `statusName === 'متعثر'` *first*, before `isApproved`, since a stalled project now has `isApproved = true`. Likewise the "سبب إلغاء الاعتماد" reason card (currently gated on `!isApproved && approvalCancellationReason`) must re-gate on `statusName === 'متعثر'` instead of `!isApproved`.
- Add manager-only "تعثر" button (opens a small reason-entry modal) and, when already stalled, an "إلغاء التعثر" button, both on the sub-project details page next to the existing status pill.
- `Frontend/src/app/features/projects/projects.ts` — `kpiApproved`/`kpiPending` currently a straight `isApproved` split; add a third bucket `kpiStalled` (status === متعثر), and exclude stalled from `kpiApproved`. `approvalFilter` (`'all' | 'approved' | 'pending'`) gets a fourth value `'stalled'`, with a new toolbar segment button, matching the KPI split.
- Everything else the audit checked (`ApproveAsync`'s own re-approval guard, Excel-import's `!IsApproved` skip-checks, `MainProject.IsApproved` — unrelated field, `plan-list.ts`'s approved-plan snapshot filter, funding KPI sums) is **confirmed fine as-is** — a stalled project is still a real, funded, approved project for those purposes; only the Projects-page KPI/filter and the status-pill/reason-card UI needed the fix above.

## Testing

No automated test suite in this repo (established pattern all session) — verify via `dotnet build` + `ng build` + live browser walkthrough: split-budget entry round-trips correctly through create/edit, new السنوات المالية settings page lists and edits a year, project-type dropdown persists and displays on the details page, mark-stalled → status pill flips to متعثر immediately (not معتمد), reason card shows the entered reason, KPI/filter counts move the project out of معتمد into متعثر, reactivate flips it back and KPI/filter move it back to معتمد.
