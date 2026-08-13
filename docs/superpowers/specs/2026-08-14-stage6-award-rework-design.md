# Stage 6 (Contract Award) Rework — Design

## Context

This is Cycle A of a larger backlog produced by auditing the user's full requirements notes against the actual codebase (see the audit conversation — no separate artifact committed, decisions were made live). Six of the seven items below were explicitly confirmed by the user; one (contract-type-from-memo mechanics) is a technical resolution of an ask that didn't specify implementation details, reasoned from an existing codebase pattern and stated here for the record rather than re-asked.

All changes are scoped to the Contract Award stage (مرحلة العقد والترسية، stage 6 of procurement) and its immediate dependencies: `ContractAward`, `ProjectAssignment`, `PresentationMemo`, `ProcurementService`, and the award panel in `procurement-workflow.html`/`.ts`.

**Verified against current code on 2026-08-14**, after merging two same-day upstream commits (`6e89ffa`, `c199dc0`) that touched `ProcurementService.cs` and `procurement-workflow.html` in unrelated areas (duration/permission enforcement, a delivery-stage rename). Confirmed none of that work overlaps the methods this spec changes (`EnsureHasPresentationMemoAsync`, `ValidateContractAwardForCompletionAsync`, `SetCompletionAsync`'s ContractAward branch, `UpsertAssignmentAsync`, `GetContractAwardDetailsAsync` — all read fresh post-merge, byte-identical to the pre-merge versions this design was drafted against).

## The 7 changes

### 1. Label rename

"إجمالي التكلفة" → "إجمالي المخطط" in the award budget summary.

File: `Frontend/src/app/features/financial/procurement-workflow.html:252` — the `<span>` text only. No backend change; `ContractAwardDetailsDto.TotalCost` keeps its name (it's a DTO/API contract, not user-facing text).

### 2. وفرة (savings) — computed, not stored

New field on `ContractAwardDetailsDto`: `Savings` (decimal?), computed as `TotalCost − ContractValue` when `ContractValue < TotalCost`, else `null`. Same treatment as the existing `ContractualDeliveryDate` field on the same DTO — computed in `GetContractAwardDetailsAsync`, never persisted as a column. Reasoning: it's fully derivable from two values that already live elsewhere (`SubProject.TotalCost` via `BankFunding + SelfFunding`, `ProjectAssignment.ContractValue`); a stored column would just be a cache that could drift.

Displayed in the award panel next to قيمة العقد, only when `ContractValue` is set and `Savings` is not null (i.e., don't show a "وفرة: 0" or a meaningless value before a contract value is even entered).

### 3. قيمة العقد validation — blocks completion (user's explicit decision)

In `ValidateContractAwardForCompletionAsync` (`ProcurementService.cs:854-940`), add a check after the existing duration/handover checks and before the مقاولات-only advance-payment block (so it applies to both project natures — this rule isn't advance-payment-specific):

```
allowedCeiling = TotalCost × (1 + (SubProject.OverrunPercentage ?? 0) / 100)
if ContractValue is null → require it (a contract with no value can't be validated or later measured against — this is implicitly already required for the workflow to make sense, now enforced explicitly)
if ContractValue > allowedCeiling → block, message states both the entered value and the ceiling
```

This reuses `SubProject.OverrunPercentage` — the exact same field and ceiling formula already enforced for execution-stage spending in `ExecutionStageService.GetAllowedCeilingAsync`. Same concept, same source field, now also gating the contract value itself at award time rather than only gating post-award spend.

### 4. تاريخ العقد replaces رقم العقد — in the award UI only, not the schema

**Schema:** add `ContractDate` (`DateTime?`) to `ProjectAssignment`. **Do not remove `ContractNumber`** — it's a live column consumed by `ReportsService.ExecutionReports.cs:37` and the separate Project Assignments ledger feature (`ProjectAssignmentService.cs`, `ProjectAssignmentDtos.cs`), neither of which this cycle touches.

**UI:** in the award panel only (`procurement-workflow.html:311-314`), replace the "رقم العقد" input with a "تاريخ العقد" date input. The award form stops reading/writing `ContractNumber` — that field becomes exclusively the Project Assignments ledger's concern going forward, unless a future cycle decides otherwise. `SetContractAwardDetailsDto`/`ContractAwardDetailsDto` gain `ContractDate`; `SetContractAwardDetailsDto.ContractNumber` is removed (nothing legitimate will ever populate it through this endpoint again, since the field is gone from the form) — but `ProjectAssignment.ContractNumber` itself is untouched, so existing data and the report survive unchanged.

### 5. Contract type — fixed from the memo, not independently chosen (user's explicit decision)

Remove the "نوع العقد" `<select>` from the award panel (`procurement-workflow.html:302-310`) and `aContractTypeId` from `procurement-workflow.ts`. Replace with a read-only display of the linked memo's `ContractingMethodLabel` (already fetched into `ProcurementOverviewDto.ActivePresentationMemo` — no new API call needed, just render what's already on screen at the top of the page, or repeat it here for context).

**Backend mechanics** (new, since `ContractingMethod` is a 7-value enum and `ProjectAssignment.ContractTypeId` is a required FK into the free-form `ContractType` lookup table — two different shapes that need reconciling): on save, find-or-create a `ContractType` row whose `Name` equals the memo's `ContractingMethodLabel` (e.g. "مناقصة عامة"), and assign its Id. This mirrors the existing auto-create-missing-lookup pattern already used by the Excel import pipeline (auto-creating missing `ProjectLevel`/`ComponentType`/`AccountingUnit` rows rather than falling back to "غير محدد"). `SetContractAwardDetailsDto.ContractTypeId` is removed — the caller no longer supplies it; `UpsertAssignmentAsync` derives it internally from the sub-project's active memo instead of trusting an input value. If a sub-project somehow has no active memo at this point, this is unreachable in practice (see #6 — the whole stage is gated on a completed memo before any stage can even start), but the code should still throw a clear `BusinessRuleException` rather than silently null-referencing, for defensiveness.

### 6. Memo gate: attached → completed (user's explicit decision)

`EnsureHasPresentationMemoAsync` (`ProcurementService.cs:597-606`) changes from an existence check to also requiring `PresentationMemo.IsCompleted == true` on the active (most recent) memo. Reuse the same "most recent by CreatedAt then Id" ordering already established in `GetOverviewAsync` (`ProcurementService.cs:125-128`) for consistency — "the active memo" must mean the same thing everywhere in this file.

Update the doc comment (it currently explicitly says completion is NOT required, with a stated rationale — that rationale is being overridden by this change, the comment must say so, not just go stale).

Error message changes from "لا يمكن بدء مراحل الطرح قبل إرفاق مذكرة عرض للمشروع" to something like "لا يمكن بدء مراحل الطرح قبل اكتمال مذكرة العرض المرتبطة بالمشروع" (must be *completed*, not just attached).

### 7. توريدات skips land handover entirely (user's explicit decision, matches an earlier draft note)

**Validation** (`ValidateContractAwardForCompletionAsync`): wrap the entire `SiteHandoverMode`/`SiteHandoverDate`/`SiteHandoverProofFile` block (`ProcurementService.cs:879-895`) in `if (IsContractingProject(project.ProjectNature))` — توريدات skips it completely, no handover mode/date/proof required at all.

**Completion** (`SetCompletionAsync`, the `stage == ProcurementStage.ContractAward` branch at `ProcurementService.cs:281-284`): for توريدات projects, before calling `SyncFinalDeliveryStageAsync`, auto-set `doc.SiteHandoverDate = DateTime.UtcNow` (only if not already set) and save. This is the one deliberate reuse in this spec: rather than inventing a parallel "delivery clock" concept for supply projects, توريدات projects get their `SiteHandoverDate` populated automatically and silently, so the existing `ComputeContractualDeliveryDate`/`SyncFinalDeliveryStageAsync` machinery (which already reads `SiteHandoverDate + ExecutionDurationMonths/Days` to compute the delivery deadline) works completely unchanged for both project natures. `SiteHandoverMode` and `SiteHandoverProofFile` stay `null` for توريدات — nothing reads them as a precondition anymore per the validation change above.

**UI:** the "مدة التنفيذ وتسليم الأرضية" section's handover-mode/date/proof sub-fields (`procurement-workflow.html`, the block following what's shown above) render only when `aw.projectNature === 'مقاولات'`. توريدات still needs the execution-duration months/days inputs (those feed the same deadline math) — only the handover-specific fields are hidden.

## Out of scope for this cycle

- No change to `ContractNumber`'s existence, the Project Assignments ledger feature, or `ReportsService.ExecutionReports.cs`.
- No change to advance-payment logic (#16/#17 of the original notes) — already built, already confirmed correct by audit.
- No UI change to the memo creation form itself — item #6 only changes what the procurement *stage* checks, not how a memo gets marked complete (that's the existing legal-decision-attachment flow, untouched).

## Testing

Backend: `Backend/tests/SmartInvest.Tests/` already has a `ProcurementDurationAndAuthorizationTests.cs` from the just-merged upstream work as a pattern reference for testing this service. New/changed behavior needing coverage: the ceiling-validation block (#3), the memo-completion gate (#6), and توريدات skipping handover both in validation and in the auto-set-date behavior (#7).

No frontend test coverage exists for this component today (consistent with the rest of the app) — verification is manual click-through per this session's established convention, covering: a مقاولات project through full award completion (handover fields present, contract type read-only from memo, وفرة shown when contract value < planned, blocked when contract value exceeds ceiling), and a توريدات project through the same flow (no handover fields, timer starts automatically, follow-up's final-delivery row gets a real date immediately on completion).
