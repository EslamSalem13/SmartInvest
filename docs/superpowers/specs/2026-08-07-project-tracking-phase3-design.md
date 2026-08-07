# Phase 3: متابعة المشروعات (Project Execution Tracking) — Design

## Goal

A new post-award execution-tracking module. Today the app tracks a sub-project through pre-award procurement (6 fixed stages ending in العقد والترسية) but has nothing after that — no visibility into how much has actually been spent, how physically complete the work is, or whether stages are running on schedule. This closes that gap, and adds an AI reporting layer (RAG for contractor/risk insight, agentic for portfolio-wide reports) on top of the resulting data.

Depends on Phase 1 item 3 (`SubProject.ProjectNature` constrained to `"توريدات"`/`"مقاولات"`) for the execution-order business rule below — implement Phase 1 first.

## Data model

**New entity `ExecutionStage`** (`Backend/src/SmartInvest.Domain/Entities`), one-to-many from `SubProject`, deliberately separate from the existing `Domain/Entities/Procurement/` stage model (that one is the fixed pre-award 6-stage workflow; this is a freeform, per-project, manager-named list that only starts once procurement's العقد والترسية stage is marked complete):

- `StageId`, `SubProjectId` (FK)
- `Name` (string, manager-entered — no fixed enum, "ينفع نضيف مرحلة جديدة")
- `Deadline` (DateTime)
- `SelfFundingSpent` / `BankFundingSpent` (decimal, each nullable/defaults 0)
- `SelfFundingProofFile` / `BankFundingProofFile` (only required if the corresponding spend field is non-zero — separate uploads per the explicit requirement)
- `PhysicalProgressPercent` (decimal)
- `PhysicalProgressProofFile` (photo/document)
- `Notes` (string, nullable)
- `PenaltyAmount` (decimal, nullable — filled when `Deadline` is missed; manually entered, no auto-calculation specified)
- `CreatedAt`/`CompletedAt` timestamps for the "on time" reporting later

**`SubProject` additions:**
- `OverrunPercentage` (decimal, nullable, project-level per explicit decision — not per-stage)

**Contractor additions** (`Contractor` entity / new related tables):
- `ContractorNote` — `Id`, `ContractorId`, `SubProjectId` (nullable — null means general note, set means project-specific), `Text`, `IsAiGenerated` (bool), `CreatedAt`
- `WillWorkAgain` (bool, nullable — yes/no flag per explicit decision, shown during contractor assignment)
- Fines: rolled up by querying `ExecutionStage.PenaltyAmount` across all of a contractor's `ProjectAssignment`s — no separate fines table needed, plus a `PenaltyPaid` (bool) flag added to `ExecutionStage` so each penalty can be marked paid/unpaid.

## Business rules

**Execution order** (branches on `SubProject.ProjectNature`):
- `مقاولات`: `SelfFundingSpent`/`BankFundingSpent` can be recorded as soon as a stage starts (advance payment to fund the work).
- `توريدات`: `PhysicalProgressPercent` must be recorded before spend is recorded for that stage (goods delivered first, payment follows) — validate spend fields stay at 0 until progress > 0 for that stage.

**Overrun validation:** sum of `SelfFundingSpent + BankFundingSpent` across all of a sub-project's stages cannot exceed `SubProject.TotalCost * (1 + OverrunPercentage/100)`. Enforced server-side on stage save.

**Stage-tracking start trigger:** first `ExecutionStage` can only be created once the sub-project's procurement workflow shows العقد والترسية stage completed (existing `ProcurementService`/`ProcurementStage` — reuse the existing completion check, don't duplicate it).

**Stalled projects still appear** in the متابعة المشروعات table (with a متعثر badge) — per explicit decision, since `IsApproved` stays true for stalled projects (Phase 1 item 4) and their spend/progress data remains real and relevant.

## New page: متابعة المشروعات

Sidebar item alongside المشروعات and الإدارة المالية. Filtered by financial year (required, like Projects/Financial pages) plus the same advanced filters already on the Projects page (main/sub program, level, agency, markaz, priority, funding).

Table columns (per the provided sketch): اسم المشروع الفرعي, اسم المقاول (from `ProjectAssignment`/latest contractor), % التنفيذ العيني (computed: latest/weighted stage `PhysicalProgressPercent`), % التنفيذ المالي (computed: total spent / `TotalCost`), ميعاد أقرب مرحلة قادمة (earliest incomplete stage's `Deadline`), زر "عرض المراحل" opening a detail view listing every `ExecutionStage` for that project (name, deadline, spent ذاتي/بنكي + proof files, progress % + proof, notes, penalty amount + paid flag) — mirrors the field list above, add-new-stage action included.

## AI layer — new dedicated التقارير page (not the Dashboard)

**RAG — contractor report:** reads all `ExecutionStage.Notes` (+ timeliness vs `Deadline`, + overrun history) across a contractor's assigned projects, synthesizes into an `IsAiGenerated=true` `ContractorNote`, and suggests a `WillWorkAgain` value for the manager to confirm/override.

**RAG — stalled-project precedent warning:** reads `SubProject.ApprovalCancellationReason` (Phase 1 item 4's stalled-reason field) across historically-stalled projects, and when a new sub-project is created that's similar (by name/description embedding or the existing deterministic-similarity approach already used for import matching), surfaces a warning referencing what went wrong on the similar past project.

**Agentic — portfolio reports:** on-demand + monthly/quarterly scheduled reports covering (a) which projects executed their stages on schedule (`CompletedAt` vs `Deadline` per stage), (b) per-markaz total spend comparison ("are some centers getting more funding than others"), synthesized into a written report, not just a data table.

## Testing

No automated test suite (established pattern). Verify via `dotnet build` + `ng build` + live walkthrough: create stages only unlocks after العقد والترسية completion, مقاولات vs توريدات ordering rule enforced correctly in both directions, overrun validation rejects a spend that pushes total past budget×(1+overrun%), متابعة المشروعات table shows correct % calculations and flags stalled projects, contractor profile shows rolled-up fines + notes + flag correctly during assignment, and the two RAG reports + one agentic report each produce sensible output against real seeded data (mirroring how every prior AI feature this session was verified against `suggested_real.xlsx`-style real data, not synthetic stubs).
