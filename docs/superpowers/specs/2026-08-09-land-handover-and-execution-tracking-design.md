# تسليم الأرضية وربط التعاقدات بمتابعة المشروعات — Design

**Date:** 2026-08-09
**Status:** Approved, ready for implementation planning

## Problem

Three connected gaps in the flow between الإدارة المالية (procurement) and متابعة المشروعات (execution tracking), plus the UX bug that surfaced them.

### The reported bug

Completing step 6 (العقد والترسية) for a sub-project "just refreshes" with no confirmation and no visible assignment.

Root-caused against live data — sub-project 1770:

```
ProjectAssignmentId=4  ExecutionDurationMonths=2  ExecutionDurationDays=15
SiteHandoverMode=2  PenaltyAmount=1000  CurrentVersionNumber=0  IsCompleted=0
```

The save **worked**. Two separate causes produced the impression that it didn't:

1. `saveAward()` calls `reload()` on success and nothing else
   (`Frontend/src/app/features/financial/procurement-workflow.ts:185`). There is **no
   snackbar/toast mechanism anywhere in the application** — 23 `alert()` calls across 10
   files are the entire feedback vocabulary, and success paths have none at all.
2. Saving award details is not the same action as completing the stage. Completion is a
   separate button and was correctly blocked: `CurrentVersionNumber=0` means neither
   أمر الإسناد nor العقد (both `required: true`) had been uploaded.

### The three feature gaps

1. **No proof of land handover.** `ContractAward` already models
   `SiteHandoverMode` (`AtAward` / `Pending`), `SiteHandoverDate`, and a computed
   `ContractualDeliveryDate`. But there is no evidence file for the handover, and
   `SetSiteHandoverAsync` — the endpoint that records a later handover — is **called by no
   UI at all**, so a `Pending` project can never record its handover today.
2. **متابعة المشروعات shows unassigned projects.** `GetFollowUpListAsync` filters on
   `s.IsApproved` only. Execution tracking is meaningless before a contractor exists;
   the list should require a completed award.
3. **No contractual delivery deadline in the stage list.** `ContractualDeliveryDate` is
   computed but never surfaced. Staff have no view of the date the contract is actually
   measured against.

## Scope

**In scope:** the three gaps above, plus an app-wide toast service (the direct cause of the
reported bug).

**Explicitly out of scope, its own spec/plan cycle later:** the Excel-import wizard's
per-row "show and edit all project data" button. It is an independent subsystem requiring
new preview DTOs (the preview currently returns aggregate counts and unresolved-lookup
lists only — zero per-row field data) and commit-time row overrides.

## Decisions

| Question | Decision | Reasoning |
|---|---|---|
| Where is the handover proof uploaded? | Both places, one field each | `AtAward` records it during step 6; `Pending` records it later from متابعة المشروعات |
| Is the proof mandatory? | Yes | Every started execution clock must have evidence behind it |
| How does the final deadline exist? | A real auto-created `ExecutionStage` row | Matches "the stage should be there" |
| Can staff edit/delete that row? | Locked, system-owned | Prevents the row contradicting the signed contract |
| What happens on step-6 reopen? | Row survives; deadline recomputes on re-completion | Preserves any spend/progress already recorded against it |
| Existing 6/6 projects? | Backfill in the migration | Old and new data behave identically from day one |
| Stage deadline past contract date? | Warn, do not block | Reality sometimes runs late; the system should record it |
| Who keeps the final stage in sync? | `SyncFinalDeliveryStageAsync` on `ExecutionStageService` | Single owner; three callers |
| `Deadline` for a pending handover? | Make the column nullable | `null` means genuinely unknown, and is excluded from overdue KPIs automatically |

## Architecture

### Storage: one location, two entry points

The handover proof is stored **once**, as an owned `StoredFile` on `ContractAward`.

The rejected alternative was a 4th file slot on `ContractAwardVersion` for the `AtAward`
case plus a separate field on `ContractAward` for the `Pending` case. That splits one
concept across two tables and forces every read to check both places. Instead a handover
is always the pair `{date, proof}`, written through a single service path, reachable from
either screen.

### The final stage is a projection, never a source of truth

`ContractAward.ContractualDeliveryDate` stays `[NotMapped]` and computed. The existing code
comment states the reason: *"محسوب وغير مخزَّن، حتى لا يتعارض مع تصحيح تاريخ تسليم الأرضية بعد
إدخاله"* (computed and not stored, so it cannot conflict with correcting the handover date
after entry).

The final stage's stored `Deadline` is therefore a **projection** of that computed value,
refreshed by `SyncFinalDeliveryStageAsync`. Correcting a handover date can never strand a
stale contract date, because the authoritative value is still derived on every read.

### Sync ownership

`ExecutionStageService.SyncFinalDeliveryStageAsync(subProjectId)` is the single owner of
"what the final stage should look like". It is idempotent and has exactly three callers:

```
ProcurementService.SetCompletionAsync(ContractAward, true) ──┐
ProcurementService.SetSiteHandoverAsync(...)  ───────────────┼──> SyncFinalDeliveryStageAsync
Backfill migration ─────────────────────────────────────────┘
```

Rejected: having `ProcurementService` write the row directly (duplicates the rule across
three call sites, drifts on first edit), and domain events (this codebase has no event
infrastructure; building it for one event is unjustified).

`ProcurementService` gains an `IExecutionStageService` dependency. Both are Infrastructure
services, so this introduces no layering violation.

## Data model

Single migration:

| Change | Detail |
|---|---|
| `ExecutionStage.Deadline` | `DateTime` → `DateTime?` |
| `ExecutionStage.IsFinalDelivery` | new `bool`, default `false` |
| `ContractAward.SiteHandoverProofFile` | new owned `StoredFile` via the existing `StoredFileConfigurationExtensions.OwnsStoredFile` pattern |
| Data backfill | Create one final-delivery stage per already-completed `ContractAward` |

Existing stage rows are unaffected by the nullability change — they keep their dates.
User-created stages still require a deadline; only the system-owned row may have `null`.

## Backend behavior

### `SetSiteHandoverAsync(subProjectId, date, proofFile)`

Becomes multipart to carry the proof. Rules:

- Requires a `ContractAward` row with `SiteHandoverMode` set.
- Mode `AtAward`: allowed **before** the award is completed, so it can be recorded inline
  during step 6.
- Mode `Pending`: still requires `IsCompleted == true` — recording a handover before the
  award exists is meaningless.
- Rejects a missing proof file with an Arabic `BusinessRuleException`.
- Calls `SyncFinalDeliveryStageAsync` after saving.

### `ValidateContractAwardForCompletionAsync`

Gains one rule, after the existing `SiteHandoverMode == null` check: when mode is
`AtAward`, both `SiteHandoverDate` and `SiteHandoverProofFile` must be present, otherwise
step 6 cannot complete.

### `SyncFinalDeliveryStageAsync(subProjectId)`

- No-ops unless the award exists and `IsCompleted`.
- Finds the row where `IsFinalDelivery == true`, creates it if absent — never duplicates.
- Name: `التسليم النهائي`.
- `Deadline = SiteHandoverDate + ExecutionDurationMonths + ExecutionDurationDays`,
  or `null` when `SiteHandoverDate` is null.
- Preserves everything already recorded on the row (spend, progress, penalty, completion).

### `CreateAsync` (user-created stages)

- Refuses to create a row with `IsFinalDelivery = true`.
- The stage DTO gains `ExceedsContractualDeadline` (bool) so the UI can warn without the
  API blocking the save.

### `GetFollowUpListAsync`

Filter becomes `IsApproved` **and** the sub-project's `ContractAward.IsCompleted`. Because
completing step 6 already requires steps 1–5 (`EnsurePreviousStageCompletedAsync`),
"award completed" is equivalent to 6/6.

Verified against live data: only sub-projects 1 and 1770 have awards or stages. 1 is
award-complete with a handover date and 5 stages (it gains a backfilled final row); 1770 is
neither approved nor complete, so it is absent from the list today regardless. The filter
change loses no currently-visible data.

## Frontend

### Toast service

`Frontend/src/app/core/services/toast.service.ts` — signal-backed queue exposing
`success()` and `error()`, with auto-dismiss. A single `ToastHost` component is mounted
beside `<router-outlet>` in `Frontend/src/app/layout/main-layout/main-layout.html:82`.

**Migration scope:** only the 4 `alert()` calls in `procurement-workflow.ts` are converted.
The other 19 across 9 files stay as they are; the service is app-wide so they can migrate
as those pages are next worked on. Converting all 23 now would turn this into an unrelated
refactor.

### Step 6 — الإدارة المالية

- When mode is `مُسلَّمة للمقاول` (`AtAward`), the form shows a handover date field and a
  proof-file field; completion is blocked until both are filled.
- Award save, stage complete, stage reopen, and handover recording all raise success
  toasts. This is the direct fix for the reported bug.

### متابعة المشروعات

- The final-delivery row renders visually distinct and locked: no delete, deadline not
  editable. Staff **can** mark it complete — that is what records the project as delivered.
  **Penalty remains editable** (manager-gated, as today) — a late-delivery fine belongs on
  precisely that row.
- With no handover recorded, the deadline cell reads `بانتظار تسليم الأرضية` instead of a date.
- Projects in `Pending` mode gain a `تسجيل تسليم الأرضية` action taking date + proof.
- Any user stage whose deadline runs past the contractual delivery date shows a warning
  marker; the save is not blocked.

### Models and services

`follow-up.models.ts` and `financial.models.ts` gain the new fields
(`isFinalDelivery`, nullable `deadline`, `exceedsContractualDeadline`,
`siteHandoverProofFileName`); `FollowUpService` and `FinancialService` gain the
handover-recording call.

## Error handling

- All new failures raise `BusinessRuleException` with Arabic messages, surfaced through the
  toast service — matching the existing convention. FluentValidation remains unwired
  codebase-wide and is not introduced here.
- `SyncFinalDeliveryStageAsync` is idempotent, so repeated award completion/reopen cycles
  cannot produce duplicate rows.
- Reopening step 6 preserves the final row and any spend recorded against it; the deadline
  refreshes on re-completion.

## Verification

No automated test suite exists in this project. The bar is a clean build plus live
verification against real data.

1. `dotnet build` and `npx ng build` both clean.
2. Migration applies; backfill creates exactly one final row for sub-project 1 and zero for
   1770.
3. Sub-project 1770 is absent from متابعة المشروعات until its award completes, then appears.
4. `AtAward` path: completing step 6 with date + proof produces a final stage carrying a
   real computed date.
5. `Pending` path: final stage shows `بانتظار تسليم الأرضية`; recording the handover fills in
   the date.
6. A success toast actually appears on award save.
7. Calling `SyncFinalDeliveryStageAsync` twice in a row creates no duplicate row.
