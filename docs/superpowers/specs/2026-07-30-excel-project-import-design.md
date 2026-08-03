# Excel Project Import — Design

> Date: 2026-07-30 | Branch: `main` | Status: Approved

## 1. Problem

Planning staff currently add projects one at a time through the "إضافة مشروع" form. The governorate's actual planning process starts from a bulk Excel plan document (confirmed against a real sample: `نسخة من الخطة المعتمدة.xlsx`, sheet `data`, 85 sub-project rows across 58 main-project codes). Staff need to bulk-import that document instead of re-typing every row.

**Confirmed against the real sample file:** every column in the source Excel maps to a field that already exists in the schema — no new entity properties are needed. The gap is entirely in the import *pipeline*: several referenced entities (Markaz, MainProgram, SubProgram, ExecutiveAgency) currently have no write path at all (`LookupsController` is GET-only), and the source data has real messiness that must be handled deliberately: sub-project codes are not unique (85 rows, 80 distinct codes), and at least one main-project code (`45181`) appears under two different Main Programs with the same name — a probable data-entry inconsistency in the source, not a bug to silently paper over.

**Explicit dependency:** this feature creates Markaz/MainProgram/SubProgram/ExecutiveAgency records as part of reconciliation (see §4). ExecutiveAgency already has a real write API (from the agencies-as-profiles work). Markaz/MainProgram/SubProgram do **not** yet — that gap is being closed by the separate "Lookup Management + Custom Measurements" spec, which this feature depends on and must be implemented after.

## 2. Column Mapping (confirmed against the real file)

| Excel column (Arabic) | Target |
|---|---|
| البرنامج الرئيسي | `MainProgram` (by name, via reconciliation if unmatched) |
| البرنامج الفرعي | `SubProgram` (by name + resolved MainProgram, via reconciliation if unmatched) |
| كود المشروع الرئيسى | `MainProject.MainProjectCode` |
| المشروع الرئيسى | `MainProject.MainProjectName` |
| مستوى المشروع | `SubProject.ProjectLevel` (existing values `محلي`/`مشترك` already cover the sample) |
| الجهة المنفذة | `SubProject.ExecutiveAgencyId` — resolved to an `ExecutiveAgency` profile by name (reconciliation if unmatched). **Not** `MainProject.ExecutingAgency` (that free-text field is a separate, legacy concept, left untouched). |
| المركز | `SubProject.MarkazId` (by name, via reconciliation if unmatched) |
| كود المشروع | `SubProject.SubProjectCode` (stored as-is; not unique, not used as a de-dup key — see §5) |
| المشروع الفرعى | `SubProject.SubProjectName` |
| المكوّن العيني | `SubProject.ComponentType` (free-text column; sample values all match the current frontend's fixed suggestion list, stored regardless of match) |
| بنك | `SubProject.BankFunding` |
| ذاتي | `SubProject.SelfFunding` |
| الوحدة الحسابية | `SubProject.AccountingUnit` — exists in the schema but no current form sets it; the import is the first writer of this field |

**Not present in the source file, given defaults:** `PriorityId` → `منخفضة` (Low, seeded id 3), `StatusId` → `جديد` (New, seeded id 1), `ProjectNature` → empty string. Staff can edit any imported row afterward through the existing sub-project form (which will need `ProjectNature`/`AccountingUnit` fields added to be fully editable post-import — noting this as a small necessary addition to `sub-project-form.ts`, currently these two fields exist on the model/DTO but aren't in the form UI).

**Financial year:** every imported sub-project is attached to whichever financial year is currently selected on the projects page at the time the import is run (same as manually adding a project today) — not a separate picker in the import flow.

## 3. Flow — Two Phases, Not One Silent Action

### 3.1 Preview (`POST /api/subprojects/import/preview`)

- Accepts a single `.xlsx` file (multipart upload), size-limited (10 MB).
- Parsed server-side via **ClosedXML** (new NuGet dependency on `SmartInvest.Infrastructure` — MIT-licensed, no Excel/Office interop required, works cross-platform).
- Locates the header row (matches the exact Arabic header set from §2) and reads every data row until the sheet ends.
- Groups rows by `MainProjectCode` to build the Main Project → Sub Projects tree the commit step will create.
- Cross-references every distinct name value against the DB for: Markaz, MainProgram, SubProgram, ExecutiveAgency. Anything not found by exact name match is returned as an **unresolved reference** (grouped by type), not auto-created.
- Also detects and returns **main-project code conflicts**: any `MainProjectCode` that maps to more than one distinct (name, MainProgram) combination in the file (the `45181` case) — flagged as its own reconciliation category, not resolved automatically either way.
- The parsed-but-not-yet-committed data (rows + detected groupings + conflicts) is cached server-side against a short-lived import-session id (returned to the frontend), so the commit step doesn't require re-uploading the file.
- Response: `{ importId, mainProjectCount, subProjectCount, unresolvedMarkaz: [...], unresolvedMainPrograms: [...], unresolvedSubPrograms: [...], unresolvedAgencies: [...], mainProjectCodeConflicts: [...] }` (each unresolved-name entry includes which rows reference it, for context in the UI).

### 3.2 Reconcile (client-side, no dedicated endpoint — resolutions are submitted with commit)

For each unresolved name in each of the four categories, staff picks one of:
- **"جديد" (new)** — a new record will be created with this exact name during commit.
- **map to existing** — a searchable dropdown over the existing records of that type; staff picks the real match (e.g. maps "المنوفية" to nothing since it isn't a real Markaz in this context / maps "قويسنا" variants together).

For each main-project-code conflict, staff picks which of the conflicting (name, MainProgram) pairs is correct for that code (or confirms both are genuinely intended as separate main projects that happen to share a code — in which case the import proceeds without merging them, since `MainProjectCode` is not treated as a uniqueness constraint by the schema).

No AI-assisted fuzzy matching in this version (explicitly deferred by the user — a future enhancement, not part of this spec).

### 3.3 Commit (`POST /api/subprojects/import/commit`)

- Body: `{ importId, financialYearId, resolutions: { markaz: [...], mainPrograms: [...], subPrograms: [...], agencies: [...] }, mainProjectCodeResolutions: [...] }`.
- Server re-fetches the cached parse result by `importId` (rejects if expired/not found — reasonable TTL, e.g. 30 minutes, matching the time a staff member might spend reconciling).
- Creates records in dependency order: (1) any Markaz/MainProgram/SubProgram/ExecutiveAgency marked "new" in the resolutions, (2) Main Projects (one per distinct resolved main-project group), (3) Sub Projects (one per source row, always inserted — never matched against existing rows for update, since `SubProjectCode` is not a reliable key here; this is a bulk *add*, not a sync).
- **Best-effort commit**, per the user's explicit answer: each sub-project row is validated and inserted independently inside its own try/catch; a failure on one row does not roll back others. The response lists the names of any sub-projects that failed, with the validation reason, so staff can add those specific ones manually via the existing single-add form afterward.
- Response: `{ mainProjectsCreated, subProjectsCreated, failedSubProjects: [{ name, reason }] }`.

## 4. Frontend

"إضافة مشروع" becomes a split button on the projects page: primary action unchanged (opens the existing single-add form), with a dropdown/second option "إضافة عن طريق Excel" opening a new import wizard modal:

- **Step 1 — Upload:** file picker (`.xlsx` only), calls the preview endpoint on submit, shows a spinner during parse.
- **Step 2 — Reconcile:** shown only if there are unresolved references or conflicts; one section per category (Markaz / Main Programs / Sub Programs / Executive Agencies / Main-Project-Code Conflicts), each unresolved name shown with its row count and the new-vs-map-to-existing choice described in §3.2. If everything resolved cleanly (no unmatched names, no conflicts), this step is skipped entirely and the wizard goes straight to a commit-confirmation summary.
- **Step 3 — Commit & Results:** shows a summary before commit (X main projects, Y sub projects, into financial year Z), a confirm button, then after commit shows the results: created counts + a list of any failed sub-project names with their reasons (if the best-effort path hit any).
- On successful commit, the wizard closes and the projects table refreshes (same `load()` pattern used elsewhere in this app).

## 5. Key Decisions (from user Q&A during brainstorming)

- **Executing Agency → `ExecutiveAgency` profile**, not `MainProject.ExecutingAgency` free text — keeps imported agencies visible on the `/app/agencies` page with their assigned sub-projects, consistent with the agencies-as-profiles work.
- **Unresolved lookup references are surfaced for manual staff reconciliation** (new vs. map-to-existing), not auto-created and not auto-rejected. No fuzzy/AI matching in this version.
- **Missing required fields get sensible defaults** (Low priority, New status, empty project nature) rather than an extra picker in the import flow — matches how the manual single-add form already defaults these implicitly by requiring a selection, so imported rows are simply pre-filled with the lowest-friction valid values and are fully editable afterward.
- **Financial year = whatever's selected on the page**, no separate picker.
- **Sub-project code is not a de-dup/update key** — every row becomes a new `SubProject`, matching the user's own framing ("auto-add the projects"), and consistent with the source data itself having non-unique codes.
- **Commit is best-effort, not all-or-nothing** — valid rows commit; failed rows are named (not silently dropped) so staff can add them manually.

## 6. Out of Scope

- AI-assisted fuzzy matching of near-duplicate lookup names (e.g. "قويسنا" vs "مركز قويسنا") — explicitly deferred by the user to a future enhancement.
- Migrating `MainProject.ExecutingAgency`'s free-text field to anything — untouched, separate legacy concept.
- Re-importing/updating previously-imported rows (this spec is additive-only; there is no "this row already exists, update it" detection).
- Supporting `.xls` (legacy binary Excel) or `.csv` — `.xlsx` only, matching the real sample file's format.

## 7. Testing

Manual, via dev servers (no test suite in this repo, per established convention):
- Import the real sample file (`نسخة من الخطة المعتمدة.xlsx`, `data` sheet) end-to-end: confirm the preview correctly reports unresolved Markaz/MainProgram/SubProgram/Agency names and the `45181` main-project-code conflict; resolve them; commit; confirm the resulting Main/Sub Projects, Executive Agency profiles, and lookup records appear correctly in their respective pages, attached to the selected financial year.
- Re-run the same file a second time without resolving anything as "map to existing" (all "new") and confirm it does NOT fail or silently dedupe — every row becomes new records, proving the "code is not a key" decision.
- Corrupt one row (e.g. non-numeric funding value) and confirm best-effort commit: other rows succeed, the bad row's sub-project name appears in `failedSubProjects` with a reason.
- Confirm a `.docx` or other non-`.xlsx` upload is rejected cleanly at the preview step.
