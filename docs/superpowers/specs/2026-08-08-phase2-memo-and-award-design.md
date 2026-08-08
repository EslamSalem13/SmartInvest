# Phase 2: مذكرة العرض + الترسية (Award) — Design

## Goal

Phase 2 of the roadmap. Two areas:

1. **مذكرة العرض** — make the project picker usable and correctly scoped, and require the legal-affairs committee decision before a memo can be completed.
2. **الخطوة السادسة — الترسية (`ProcurementStage.ContractAward`)** — turn the award stage from "two files + a boolean" into the real contractual record: which contractor won, the advance payment with its funding split, the maximum execution duration, when the site was handed over, and the penalty clause.

Phase 1 is already merged (`fb7ab0f`) — `SubProject.ProjectNature` is now a required dropdown constrained to `"توريدات"` / `"مقاولات"`, which this phase's advance-payment branching depends on.

Phase 3 ([2026-08-07-project-tracking-phase3-design.md](./2026-08-07-project-tracking-phase3-design.md)) consumes what this phase produces: the site-handover button it describes writes the field defined here, and its `ExecutionStage` rows hang off projects whose الترسية is complete.

## Branch note

`feature/premium-redesign` is abandoned. Its PR was merged into `main` and then reverted (`57244af`), which also removed `20260802163140_AddDynamicRoles` and the whole permission-based authorization layer. `main` is role-based: `[Authorize(Roles = Roles.PlanningStaff)]` / `Roles.PlanningManager`. Match that — do **not** reintroduce `[HasPermission]` or `Perm`.

Verified on `main` at `6994bf1`: backend builds with 0 errors, frontend type-checks with 0 errors, and an empty probe migration confirms `AppDbContextModelSnapshot.cs` matches the model. There is no migration conflict to resolve.

---

## Item 1 — مذكرة العرض: search + financial-year scoping

**Problem.** `Frontend/src/app/features/financial/presentation-memos.ts:96` calls `this.financial.getSubProjects()` with no argument. `financial.service.ts` then omits `financialYearId` from the query string, and `ProcurementService` short-circuits its `Where` to return every sub-project ever created. The picker itself (`presentation-memos.html:220-236`) is an unbounded checkbox list with no search — the existing `searchTerm` signal filters the *memo list*, not the picker.

**Design.** No backend change; the endpoint already accepts the filter.

- Add a `selectedYearId` signal to the component, defaulted the same way the sibling pages do it (fetch years, sort by `startDate` desc, take `[0]` — `financial-list.ts:139-151`).
- Pass it: `getSubProjects(this.selectedYearId() ?? undefined)`.
- Add a `pickerSearch` signal and a `filteredSubProjects` computed over `subProjectName` and `subProjectCode`, rendered as a search input above the checkbox list.
- Keep already-checked projects visible even when they don't match the search, so a filter keystroke can't silently hide a selection the user is about to submit.

The memo's own file upload already exists (`file-drop` zone, `presentation-memos.html:104-133`) — nothing to add there.

## Item 2 — قرار لجنة الشؤون القانونية required at completion

**Design.** A second file on the memo version, plus its own upload timestamp.

`PresentationMemoVersion` gains:
- `StoredFile? LegalAffairsCommitteeDecision`
- `DateTime? LegalAffairsDecisionUploadedAt`

Note this is *not* the technical committee. `TechnicalEvaluationVersion.FirstCommitteeReport` / `SecondCommitteeReport` are stage 4 and unrelated.

`PresentationMemoService`:
- `UploadVersionAsync` accepts the optional second file; when present, stamps `LegalAffairsDecisionUploadedAt = DateTime.UtcNow`.
- `SetCompletionAsync` (`:254`) gains a check alongside the existing "no versions" guard at `:262`: the latest version must carry a legal-affairs decision, else `BusinessRuleException("يجب إرفاق قرار لجنة الشؤون القانونية قبل إكمال مذكرة العرض")`.

Attachment retention comes free — versions are append-only, never updated or deleted (`:177` already blocks deleting a memo that has versions).

Frontend: a second `file-drop` in the upload form, and the decision's filename + `LegalAffairsDecisionUploadedAt` rendered in the version list.

## Item 3 — الترسية: contractor assignment

**Decision: reuse `ProjectAssignment`.** It already carries `ContractorId`, `ContractTypeId`, `ContractNumber`, `ContractValue`, `AssignmentDate`, `ExpectedStartDate`, `ExpectedEndDate`, `Notes`, `IsLocked`. Adding a parallel `ContractorId` to `ContractAward` would give two tables an opinion on who holds the project, and Phase 3's متابعة المشروعات + contractor profile both need one answer.

**Design.** The award stage's UI gains a contractor section: a dropdown of contractors (`GET /api/contractors`, already exists and is consumed by the contractors page), contract type, contract number, contract value.

Completing الترسية creates the `ProjectAssignment` row if absent, or updates it if the stage is reopened and re-completed. This becomes a third condition in the stage's `extraCompletionCheck`: a contractor must be selected.

`ContractAward` gains `int? ProjectAssignmentId` purely as the link back, so the award knows which assignment it produced.

## Item 4 — الدفعة المقدمة (advance payment)

**Today:** `ContractAward.AdvancePaymentDone` is a bare bool, and `BuildStages` hard-codes `"يجب تأكيد صرف الدفعة المقدمة 25% قبل إكمال هذه المرحلة"` — the 25% is a string literal, not data.

**Rules.**
- `توريدات` → no advance is paid. The whole section is hidden and the completion check skips it.
- `مقاولات` → the employee enters a free percentage (not a fixed 25). The system shows what that percentage is in money. The employee then splits it between ذاتي and بنكي. Proof of disbursement is attached.

**Design.** `ContractAward` gains:
- `decimal? AdvancePaymentPercentage`
- `decimal? AdvancePaymentSelfAmount`
- `decimal? AdvancePaymentBankAmount`

`ContractAwardVersion` gains a `StoredFile? AdvancePaymentProof`, registered as a **conditional** file slot.

`AdvancePaymentDone` is kept — it stays the employee's explicit confirmation, now backed by real numbers instead of standing alone.

**Server-side validation** (on the advance-payment endpoint, and again at completion):
- `0 < AdvancePaymentPercentage <= 100`
- `AdvancePaymentSelfAmount + AdvancePaymentBankAmount == round(TotalCost * Percentage / 100)`, to 2dp
- `AdvancePaymentSelfAmount <= SubProject.SelfFunding` and `AdvancePaymentBankAmount <= SubProject.BankFunding` — the money leaves two real pools and neither can go negative
- proof file present

**Frontend.** The award stage shows the project's `SelfFunding` / `BankFunding` / `TotalCost` from the moment it opens (the requirement's "مع إظهار ميزانية المشروع من البداية"). Typing a percentage live-computes the total advance beneath the input. The two split inputs show a running remainder so the employee can see when they balance.

The existing `extraCompletionCheck` (`ProcurementService.cs:337-339`) becomes type-aware — it needs the sub-project's `ProjectNature`, which the current `Func<TDoc, string?>` signature doesn't provide. Widen it to receive the sub-project, or resolve the nature inside the check via the `AppDbContext` the `StageOps` already holds.

## Item 5 — مدة التنفيذ وتسليم الأرضية

**Requirement.** At الترسية the employee sets a maximum execution duration (e.g. 5 months and 15 days). The clock does not start at award — it starts when the site (أرضية المشروع) is handed to the contractor. Two cases:

- Site already handed over → the countdown starts at award.
- Site not yet handed over → award completes anyway, and a "تم تسليم الأرضية" button in متابعة المشروعات (Phase 3) starts the countdown when clicked.

**Design.** `ContractAward` gains:
- `int? ExecutionDurationMonths`
- `int? ExecutionDurationDays`
- `SiteHandoverMode` — new enum in `Domain/Enums`: `AtAward = 1`, `Pending = 2`
- `DateTime? SiteHandoverDate` — set to the completion date when mode is `AtAward`; stays null under `Pending` until Phase 3's button writes it

Delivery deadline is **derived, not stored**: `SiteHandoverDate?.AddMonths(Months).AddDays(Days)`. Storing it would let it drift out of sync with a corrected handover date. Expose it as a computed DTO property so both the award page and Phase 3 read the same value.

Required at completion: duration set (months + days not both zero) and a handover mode chosen.

## Item 6 — الشرط الجزائي

**Decision: manual amount.** `ContractAward` gains `decimal? PenaltyAmount` — filled in when the derived delivery deadline is passed. No rate, no automatic accrual.

This is the **project-level** penalty, tied to the contractual delivery date. It is distinct from Phase 3's `ExecutionStage.PenaltyAmount`, which is per-stage and fires on a stage deadline. Both exist; they answer different questions. Worth revisiting once Phase 3 lands, in case the project-level one turns out to be redundant.

The award page shows the derived delivery date and, once it is in the past, surfaces the penalty input.

---

## Migration

**One migration** — `AddPhase2AwardAndMemoFields`:

| Table | Added |
|---|---|
| `ContractAwards` | `ProjectAssignmentId int NULL`, `AdvancePaymentPercentage decimal(5,2) NULL`, `AdvancePaymentSelfAmount decimal(18,2) NULL`, `AdvancePaymentBankAmount decimal(18,2) NULL`, `ExecutionDurationMonths int NULL`, `ExecutionDurationDays int NULL`, `SiteHandoverMode int NULL`, `SiteHandoverDate datetime2 NULL`, `PenaltyAmount decimal(18,2) NULL` |
| `ContractAwardVersions` | owned `AdvancePaymentProof` (`_FileName`, `_FileExtension`, `_FileSize`, `_Content`), all nullable |
| `PresentationMemoVersions` | owned `LegalAffairsCommitteeDecision` (same four), `LegalAffairsDecisionUploadedAt datetime2 NULL` |

Every column is nullable — no data backfill, no default, existing rows keep working. Explicit `[Column(TypeName = "decimal(18,2)")]` on every money field, matching the codebase convention (and avoiding the `ProjectFollowUp.ProgressPercentage` mistake that still logs a precision warning at startup).

**Procedure**, per `docs/PROJECT.md` §9:

1. Make all entity changes first, then generate one migration.
2. Run the empty-probe check afterwards — `dotnet ef migrations add ProbeCheck …` must produce empty `Up()`/`Down()`, then remove it.
3. **Known gotcha:** `dotnet ef migrations add`/`remove` rewrites `AppDbContextModelSnapshot.cs`, re-emitting every `ToTable("X")` as `ToTable("X", (string)null)` — 59 cosmetic lines. The installed `dotnet-ef` tool version differs from the SDK's EF version. Semantically identical, but check the diff and don't commit the churn.

## Implementation order

1. Item 1 — frontend only, no migration, immediately verifiable.
2. Entity changes for items 2–6, then the single migration + probe check.
3. Item 2 — memo legal-affairs decision (backend then frontend).
4. Items 3–6 — award stage, backend then frontend. Item 4 depends on item 3's contractor being selectable, and item 6 depends on item 5's derived date.

## Testing

No automated test suite in this repo — the established pattern is `dotnet build` + `npx tsc --noEmit` + a live browser walkthrough. Verify:

- Memo picker shows only the selected year's projects; search narrows it; a checked project stays visible while filtering.
- Completing a memo without the legal-affairs decision is refused with the Arabic message; with it, the filename and upload date appear in the version list and survive a new version being added.
- A `توريدات` project's award page shows no advance-payment section and completes without one.
- A `مقاولات` project refuses completion until percentage, split, and proof are present; the split must balance to the computed total; over-drawing either funding pool is refused.
- Duration + `AtAward` handover produces the correct derived delivery date; `Pending` leaves it blank and does not block completion.
- Completing الترسية creates a `ProjectAssignment` with the chosen contractor; reopening and re-completing updates rather than duplicates it.
- Empty probe migration after the schema change.

Servers via `preview_start` (`.claude/launch.json`: `backend-api` 7250, `frontend-dev` 4200) — never Bash. Port 4200 is mandatory for CORS.
