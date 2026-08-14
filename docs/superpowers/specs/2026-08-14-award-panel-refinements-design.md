# Contract Award Panel Refinements — Design

## Context

Follow-up work on the same award panel reworked in Cycle A (`feature/stage6-award-rework`, merged to `main` as `605bffd`/`b147739`). Six targeted fixes to `procurement-workflow.html`/`.ts` (مرحلة العقد والترسية، الإدارة المالية → step 6) and its backend (`ProcurementService.cs`, `BankAvailabilityService.cs`), requested directly by the user after using the reworked panel. All six were confirmed against current code before this doc was written (2026-08-14, post-merge).

## The 6 changes

### 1. قيمة العقد / الدفعة المقدمة inputs — thousands entry

`aContractValue`, `aAdvanceSelf`, `aAdvanceBank` (`procurement-workflow.ts:70,73,74`) are plain `type="number"` EGP inputs today (`procurement-workflow.html:318,332,336`) — the only raw-EGP inputs left in this panel, inconsistent with every other money field in the app (budget entry, بنك availability amount), which all use the established `egpToThousands`/`thousandsToEgp` pattern (`Frontend/src/app/core/utils/budget.util.ts`): the signal stores the thousands value the user types, a computed derives the raw EGP for submission/calculation, `egpToThousands()` converts server values back to thousands on load.

Apply the same idiom to these three fields: `syncAwardForm()` (`procurement-workflow.ts:141-158`) converts server values via `egpToThousands()` when populating the signals; the save payload (`procurement-workflow.ts:169-178`) converts back via `thousandsToEgp()`; `advanceAmount()`/`advanceRemaining()` (lines 87-99) work in raw EGP internally, converting the thousands signals at the point of use. Backend DTOs (`ContractAwardDetailsDto`/`SetContractAwardDetailsDto`) are untouched — this is a frontend-only convenience, the wire format stays raw EGP decimal, exactly like budget entry and bank availability already work.

### 2. إثبات صرف الدفعة المقدمة — inline expand/collapse under the checkbox

The file slot already exists end-to-end (`advance-payment-proof` in `ContractAwardVersion.AdvancePaymentProof`, registered as a non-required `FileSlot` in `ProcurementService.cs:867`, enforced only at stage-completion time via the `extraCompletionCheck` at `ProcurementService.cs:1027-1030`) — today it's uploaded through a separate, generic "ملفات الإصدار" version-files section elsewhere on the page, with the checkbox (`procurement-workflow.html:344-348`) only showing a hint pointing there.

Move it: remove `advance-payment-proof` from the generic file-list section entirely (single location, no duplication). When `aAdvanceDone()` is checked, render a file input directly under the checkbox (matching the pattern already used for إثبات تسليم الأرضية at `procurement-workflow.html:409`); unchecking hides it and clears any staged-but-unsaved file. The backend requirement is unchanged — still only enforced at stage completion, not at checkbox-check time, matching today's relaxed behavior.

### 3. Advance self/bank ≤ planned self/bank

New validation, both layers:
- **Frontend:** inline red hint the moment `aAdvanceSelf()` (converted to raw EGP) exceeds `aw.selfFunding`, or `aAdvanceBank()` exceeds `aw.bankFunding` — immediate feedback while typing, same visual language as other `si-err` hints in this panel.
- **Backend:** `SetContractAwardDetailsAsync`/`UpsertAssignmentAsync` (`ProcurementService.cs`) gain a real check before saving — `AdvancePaymentSelfAmount > SubProject` planned self-funding, or bank equivalent, throws `BusinessRuleException` with a message stating both the entered value and the ceiling. Equal is allowed (≤, not <) — matches the ≤-ceiling convention already used by the overrun checks elsewhere in this same service.

### 4. الدفعة المقدمة base — قيمة العقد instead of الإجمالي المخطط

`advanceAmount()` (`procurement-workflow.ts:87-94`) currently computes `aAdvancePercentage() × award()?.totalCost`. Switch the base to the live `aContractValue()` input (post-thousands-conversion, raw EGP) instead of the saved `totalCost` — matches how the field already reacts live to `aAdvancePercentage()` as the user types, before any save. If `aContractValue()` is empty/null, `advanceAmount()` returns 0 (same empty-input handling as today).

### 5. المتاح becomes net of spend (advance payments + execution spend)

Today `BankAvailabilityListDto.TotalAvailable` (`BankAvailabilityService.cs:66,70`, displayed as "إجمالي المتاح" in `projects.html:278`) is a pure receipts sum — every إتاحة entry adds to it, nothing ever subtracts. This is the exact "net of spend" behavior decided earlier in this project's requirements audit but never implemented (it was scoped as its own follow-up cycle, never started) — the user is now asking for it directly, scoped as: advance payments paid **and** execution-stage spend, both across every sub-project linked to the financial year.

`GetForFinancialYearAsync` (`BankAvailabilityService.cs:36-75`) changes `TotalAvailable` from the raw `items.Sum(x => x.Amount)` to:

```
receipts = items.Sum(x => x.Amount)                                    // unchanged raw sum
advancesSpent = Σ ContractAward.AdvancePaymentBankAmount
                  where AdvancePaymentDone == true
                  and SubProject.FinancialYears.Any(fy => fy.FinancialYearId == financialYearId)
executionSpent = Σ ExecutionStage.BankFundingSpent
                  where SubProjectFinancialYear.FinancialYearId == financialYearId
TotalAvailable = receipts - advancesSpent - executionSpent
```

Both sums are scoped via the same "sub-projects linked to this financial year" join `GetTotalBankFundingAsync` (`BankAvailabilityService.cs:345-350`) already uses for `TotalBankFunding`, so all three headline figures on the الإتاحات البنكية modal stay mutually consistent (same year-scoping logic, one join pattern, no drift between them).

`RemainingAvailable` (`= TotalBankFunding − TotalAvailable`, "المتبقي الممكن إتاحته") is *not* touched — it keeps meaning "how much more bank funding still needs to be deposited to reach the plan," a different question than "how much of what's already been deposited is still unspent." Only `TotalAvailable`'s definition changes.

The existing deposit cap-check in `AddAsync`/`UpdateAsync` (`BankAvailabilityService.cs:127-137,247-257`, blocks adding an إتاحة entry that would push total receipts above `TotalBankFunding`) is **not** touched — it already computes its own raw receipts sum locally for that specific check, which is about capping deposits against the plan, not about spendable balance. No migration needed — pure aggregation over existing columns, no new schema.

### 6. أرضية المشروع — upload without a forced save-first step

Today, choosing "مُسلَّمة للمقاول" in أرضية المشروع only reveals a real upload form once the award has already been saved once (`aw.siteHandoverMode !== 1` gate at `procurement-workflow.html:400-401`, backed by a separate `SaveHandover` endpoint/flow distinct from the main award save). This applies to both first-time creation and later edits.

Merge the two: `SetContractAwardDetailsAsync` (and its DTO, `SetContractAwardDetailsDto`) accepts the handover date and proof file directly alongside the rest of the award fields, in one request. Frontend: when `aHandoverMode()` is set to 1 (مُسلَّمة), the date + file inputs render immediately as part of the main award form — no more gate, no more separate save action for this specific case. The award's own existing save button becomes the only save action needed to set `SiteHandoverMode`, `SiteHandoverDate`, and the proof file together, whether this is the first save or a later edit.

## Out of scope

- No change to `ContractAwardVersion`'s other file slots (`award-order`, `contract`) — only `advance-payment-proof` moves.
- No change to `RemainingAvailable`'s formula (item 5 only touches `TotalAvailable`).
- No change to the deposit cap-check in `AddAsync`/`UpdateAsync`.
- No change to `ExecutionStage.BankFundingSpent`'s own recording/validation logic — item 5 only reads it, doesn't change how it's written.
- إثبات تسليم الأرضية's own required-ness at stage completion (`ProcurementService.cs:964`) is unchanged — item 6 only removes the forced-save-first gate, not the underlying completion requirement.

## Testing

Backend: new/changed coverage needed for item 3 (self/bank-exceeds-planned block, both fields, boundary at exactly equal), item 5 (`TotalAvailable` net-of-spend arithmetic — receipts minus done-advances minus execution spend, scoped correctly to one financial year and not leaking sub-projects from other years), item 6 (single save request setting handover fields together, both create and edit paths).

Frontend: no automated test coverage exists for this component today (consistent with the rest of the app) — manual click-through covering: thousands entry round-trips correctly on reload (item 1), proof upload appears/disappears with the checkbox and the generic file list no longer shows it (item 2), the self/bank hint appears and the save blocks at the boundary (item 3), advance amount recalculates when قيمة العقد changes (item 4), إجمالي المتاح drops after marking an advance payment done and after recording execution spend (item 5), and أرضية المشروع can be set and uploaded in one save on a brand-new award (item 6).
