# Excel Project Import — Suggested vs. Approved Plan — Design

> Date: 2026-08-01 | Branch: `main` | Status: Approved
> Supersedes the flow described in `2026-07-30-excel-project-import-design.md` §3 (Flow) and §5 (Key Decisions on plan integration). That doc's §2 (Column Mapping) and §1's ClosedXML/dependency choice still apply and are not repeated in full here — read that doc first for the base mechanics.

## 1. Problem

The governorate produces two different Excel documents at different points in the planning cycle, for the same set of projects:

1. **The suggested plan** — every row's "كود المشروع" (project code) column is empty. These are proposed projects, not yet approved.
2. **The approved plan** — the same projects, later, with "كود المشروع" now filled in per row (the code assigned on approval).

Staff need to upload either file through the same "استيراد من Excel" entry point and have the system figure out which kind it is and act accordingly — create new suggested projects for the first, or find-and-approve the already-existing ones for the second — rather than staff picking a mode manually.

**No AI required.** Every decision in this flow is a deterministic rule: the column layout is fixed and known, suggested-vs-approved detection is one column-emptiness check, and approved-row matching is an exact name match (not fuzzy) — confirmed by the user as the intended behavior ("the only unique identifier of each project" — matched by name, not fuzzy-matched). AI-assisted fuzzy matching remains explicitly out of scope for this version (per the base spec's §6), unchanged by this update.

**Single governorate, confirmed by the user.** This entire application is scoped to one governorate — `LookupSeeder.cs` seeds exactly one `Governorate` row (`"المنوفية"`) and only ever runs once (`if (!context.Set<Governorate>().Any())`), so there is always exactly one `Governorate` in the database. Wherever the base spec's reconciliation flow (§3.2) creates a "new" `Markaz` — `CreateMarkazDto` requires a `GovernorateId` — that id is resolved automatically to the one existing `Governorate` (`_governorateRepository.GetAllAsync()` → single row → its id), with no governorate picker or reconciliation category in the UI. If the query ever returns zero or more than one governorate (a broken/atypical database), the import fails fast with a clear message rather than guessing.

## 2. Detection: One File, Two Behaviors

After the same upload + column-mapping step described in the base spec (§2), inspect the "كود المشروع" column across all data rows:

- **Every row's code is empty/blank** → **Suggested-file mode** (§3 below).
- **Every row's code is non-empty** → **Approved-file mode** (§4 below).
- **Mixed** (some rows have a code, some don't) → reject the whole file at the preview step with a clear message ("الملف يحتوي على مشروعات بأكواد وأخرى بدون أكواد — يجب أن يكون الملف إما خطة مقترحة (بدون أكواد) أو خطة معتمدة (بكل الأكواد)"). Not silently guessed either way.

This matches the codebase's own existing convention: `SubProject.IsApproved` is already derived as `code != null` everywhere a sub-project is created or updated (`SubProjectService.CreateAsync`/`UpdateAsync`) — this feature extends that same rule to bulk import instead of inventing a new one.

## 3. Suggested-File Mode

Reuses the base spec's full pipeline unchanged:

- **Preview** (`POST /api/subprojects/import/preview`): parse, group by `MainProjectCode`, cross-reference Markaz/MainProgram/SubProgram/ExecutiveAgency names against the DB, detect main-project-code conflicts (the `45181` case) — all exactly as in the base spec §3.1.
- **Reconcile**: staff resolves unmatched names and code conflicts — exactly as in the base spec §3.2.
- **Commit** (`POST /api/subprojects/import/commit`): creates Markaz/MainProgram/SubProgram/ExecutiveAgency records marked "new", then Main Projects, then Sub Projects (one per row, always a new `SubProject`, best-effort per-row as in the base spec §3.3) — with every created `SubProject.SubProjectCode = null` and `IsApproved = false` (guaranteed, since this mode only runs when every row's code is blank).

**New in this mode — Plan linkage, after the commit above succeeds:**

- Resolve the target `Plan`: find an existing `Plan` with `PlanStatus == Suggested` and `FinancialYearId` equal to the financial year selected on the projects page (same financial-year source as the base spec §2's "Financial year" note). If one exists, reuse it (add to it). If none exists, create one:
  - `PlanName`: `"الخطة المقترحة – {financialYearName}"` (e.g. `"الخطة المقترحة – 2026/2027"`).
  - `StartDate`/`EndDate`: copied from the selected `FinancialYear`'s own `StartDate`/`EndDate`.
  - `PlanStatus`: `Suggested`.
  - `FinancialYearId`: the selected financial year's id.
  - `SuggestionDate`: now (server default, matches `Plan.SuggestionDate`'s existing default).
- For every `SubProject` successfully created in the commit above, create a `PlanProject` row linking it to that `Plan`.
- This reuses the existing "one Suggested plan per financial year" rule already enforced by `PlanService.AddPlan` — the importer does not bypass or duplicate it, it participates in it (find-or-create, not always-create).

**Response** (extends the base spec's commit response): `{ mainProjectsCreated, subProjectsCreated, failedSubProjects: [...], planId, planName }`.

## 4. Approved-File Mode

A different commit path — this mode never creates a `SubProject` from scratch on a successful match; it finds and approves existing ones.

### 4.1 Preview (same endpoint, mode auto-detected as in §2)

- Parse the file with the same column reader as the base spec (§2's mapping), but only 3 columns are actually needed for matching/action in this mode: **البرنامج الرئيسى** (main project name), **المشروع الفرعى** (sub project name), **كود المشروع** (the code to assign). The remaining columns (funding, markaz, level, agency, etc.) are read for display in the reconciliation UI (so staff can see enough context to judge a match) but are **not** written anywhere — confirmed: on a matched row, only the sub-project's `SubProjectCode`, `IsApproved`, `ApprovedAt`, and `StatusId` change; every other field (funding, markaz, level, component type, agency…) is left exactly as it already is from the suggested import. This deliberately allows the approved figures to differ from the suggested ones without the import silently overwriting anything — if staff need to correct a field based on the approved file, they do it manually afterward through the existing sub-project edit form, same as any other correction.
- For each row, resolve the **Main Project** by exact `MainProjectName` match against existing `MainProject` rows, and the **Sub Project** by exact `SubProjectName` match against existing `SubProject` rows **within that resolved main project** (name uniqueness is only assumed within a main project, not globally — matches how `SubProjectService.CreateAsync`'s own name-uniqueness check already scopes... actually that check is currently global via `NameExistsAsync(name, ...)` with no main-project scoping; for this import's matching purposes, still search within the resolved main project first since that's the meaningful disambiguator when the same main project's sub-projects are being approved — if the resolved main project has no matching sub-project by that name, treat as a full unmatched-row per §4.2, do not fall back to a global cross-project name search).
- Rows where the main project name has no exact match, or the main project matched but no sub-project under it matches the name, are **unresolved rows** — collected the same way the base spec collects unresolved lookup names (grouped, shown with row context).
- Response: `{ importId, mode: 'approved', matchedCount, unresolvedRows: [{ rowIndex, mainProjectName, subProjectName, code }], targetFinancialYearId }`.

### 4.2 Reconcile (client-side, submitted with commit — same pattern as base spec §3.2)

For each unresolved row, staff picks one of:
- **Map to an existing sub-project** — a searchable dropdown (scoped to all sub-projects, not just the resolved main project, since the mismatch might be because the main project name itself didn't match) — staff picks the real match.
- **Create new, already approved** — a new `MainProject` (if its name also didn't match) and/or `SubProject` is created fresh with that name, and is created directly with `SubProjectCode` set and `IsApproved = true` (skipping the "suggested" state entirely — there is nothing to have suggested, since this row never appeared, or matched, in any prior suggested import).

No fuzzy/AI-assisted suggestions in the mapping dropdown beyond the standard searchable-by-typing behavior already used elsewhere in this app (e.g. the existing sub-program picker) — same "no AI" decision as §1.

### 4.3 Commit (`POST /api/subprojects/import/commit`, same endpoint, `mode: 'approved'` branch)

For every row (matched directly in preview, or resolved by staff in §4.2):

- Set `SubProject.SubProjectCode` to the row's code, `IsApproved = true`, `ApprovedAt = now`, and `StatusId` resolved the same way the existing single-project approve flow does it (`SubProjectService.ApproveAsync` resolves to `"قيد التنفيذ"` — reuse that exact status-name resolution, not a new default).
- Best-effort per row, same as the base spec's commit (§3.3) — one row's failure doesn't roll back others; failures are named in the response with a reason.
- **Skip** (report as a failed/skipped row, not an error) any row whose resolved sub-project is *already* approved — approving an already-approved sub-project is a no-op error today (`SubProjectService.ApproveAsync` throws `"المشروع الفرعي معتمد بالفعل"`); the import treats that thrown message as this row's failure reason rather than aborting the row's siblings.

**Plan handling, after all rows above are processed:**

- Resolve the financial year the same way as suggested-mode: whichever is selected on the projects page.
- Look for an existing `Plan` with `PlanStatus == Suggested` for that financial year.
  - **If one exists:** call the same approval path as the existing single-plan "اعتماد الخطة" feature (`PlanService.ApproveAsync(planId, approvalDate)` — sets `Plan.ApprovalDate` and flips `PlanStatus` to `Approved`) using the approval date supplied by staff at commit time (§4.4). Any of this import's successfully-approved sub-projects that aren't already linked to that plan via `PlanProject` get linked now (covers rows that were created fresh in §4.2 with no prior suggested-import link, or approved sub-projects that happened to belong to a different plan/no plan).
  - **If none exists** (no prior suggested plan for that year): create a new `Plan` directly with `PlanStatus = Approved`, `ApprovalDate` = the supplied date, `PlanName`: `"الخطة المعتمدة – {financialYearName}"`, `StartDate`/`EndDate` copied from the `FinancialYear`, `FinancialYearId` = the selected year — then link every successfully-approved sub-project from this commit to it via `PlanProject`.
- This two-branch resolution is exactly the pair of choices already confirmed: approve-the-existing-suggested-plan when one exists, else create-directly-as-approved.

**Response:** `{ subProjectsApproved, subProjectsCreatedAndApproved, failedRows: [{ mainProjectName, subProjectName, reason }], planId, planName, planStatus }`.

### 4.4 Frontend — approval date prompt

The commit-confirmation screen (base spec §4 Step 3) gains one more field **only when the detected mode is `approved`**: a required "تاريخ الاعتماد" date picker, defaulting to today, submitted as part of the commit request body. Suggested-mode's commit-confirmation screen is unchanged (no date field, matches the base spec).

## 5. Frontend — Single Entry Point, Auto-Branching

The wizard from the base spec (§4) is unchanged at Step 1 (upload) — staff does not pick "suggested" or "approved" up front; the system decides from the file's own content per §2 above, after the preview call returns. From Step 2 (Reconcile) onward, the wizard's *content* differs by the detected mode:

- **Suggested mode:** exactly the base spec's Step 2/3 (Markaz/MainProgram/SubProgram/Agency reconciliation, main-project-code conflicts, commit summary, results).
- **Approved mode:** Step 2 shows unresolved *project* rows (§4.2) instead of unresolved *lookup names* — a different reconciliation UI, same modal shell. Step 3's commit-confirmation gains the approval-date field (§4.4); the results screen shows `subProjectsApproved`/`subProjectsCreatedAndApproved`/`failedRows` instead of the suggested-mode's counts.

A banner at the top of Step 2 (or Step 3 if nothing needs reconciling) states plainly which mode was detected and why (e.g. `"تم اكتشاف: خطة معتمدة (كل الصفوف تحتوي على كود مشروع)"` / `"تم اكتشاف: خطة مقترحة (لا يوجد أكواد مشروعات)"`), so staff can catch a wrong-file upload before committing.

## 6. Out of Scope (in addition to the base spec's §6)

- Editing/overwriting fields other than code+approval status on an approved-mode match, even if the approved file's row has different funding/markaz/etc. values than what's currently stored (§4.1) — a deliberate, explicit non-goal per the user's confirmed answer, not an oversight.
- Un-approving or reverting an approved-mode commit — same as every other write in this app, undo is manual (edit the sub-project / cancel its approval through the existing UI), not a feature of the importer.
- Detecting or warning about a main-project name that matches multiple existing `MainProject` rows (e.g. two distinct main projects that happen to share a name) — treated as an implementation-time edge case to handle defensively (pick a deterministic tie-break, e.g. reject as unresolved rather than guessing) rather than a design decision requiring its own UX.

## 7. Testing

Manual, via dev servers (no test suite in this repo, per established convention), in addition to the base spec's §7 checklist (which covers suggested-mode end-to-end):

- Upload a file where every row has a code: confirm the wizard banner reports "approved" mode, confirm rows matching existing (suggested-imported) sub-projects by main+sub project name get their code assigned and become approved, confirm non-matching rows appear for reconciliation, confirm mapping one to an existing sub-project approves it and creating one fresh produces an already-approved new sub-project.
- Confirm approving via the approved-file import correctly flips the financial year's existing Suggested plan to Approved (same plan id, `ApprovalDate` set) rather than creating a second plan, when a suggested plan already exists for that year.
- Confirm importing an approved file for a financial year with no prior suggested plan creates a new plan directly as Approved.
- Confirm a mixed file (some rows coded, some not) is rejected cleanly at the preview step with the stated message, before any reconciliation UI appears.
- Confirm an approved-mode commit does NOT change a matched sub-project's funding/markaz/level/etc. — only code + approval fields — by comparing before/after values on a row whose approved-file data intentionally differs from what was originally suggested.
