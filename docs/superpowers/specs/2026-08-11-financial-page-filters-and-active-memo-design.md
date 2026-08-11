# صفحة المالية: Filters, Stage Names, and the Active Presentation Memo — Design

## Context

Two rounds of feedback on الإدارة المالية — مراحل الطرح:

1. The page's KPI cards are inert. The team wants them to filter — by نوع التعاقد (the `ContractingMethod` just added to مذكرة العرض) and by which of the 6 procurement stages a project is currently sitting in, with a visible count per bucket. The existing `0/6 … 6/6` chips should read as stage names instead. A has/hasn't-memo filter is also wanted.

2. A hard business rule that isn't enforced today: **no procurement stage may be started for a sub-project that has no مذكرة عرض attached.**

3. A data-model correction: a sub-project currently links to any number of memos and all of them render as chips (the second screenshot shows two on one project). Only one should be **active** — the newest — and only that one should appear on the مراحل الطرح page.

## Current state

- `ProcurementSubProjectListItemDto` carries `CompletedStages`, `TotalStages`, `HasPresentationMemo` — no memo identity, no contracting method.
- `financial-list.ts:33` has `stageCountFilter: Set<number>` over `stageCountOptions = [0..6]`, filtering on `completedStages`, plus a search box and client-side pagination. No counts shown.
- `ProcurementOverviewDto.PresentationMemos` is a `List<PresentationMemoBriefDto>`, built at `ProcurementService.cs:78`, and `procurement-workflow.html:35` loops it into chips.
- `EnsureSubProjectExistsAsync` (`ProcurementService.cs:369`) already gates every procurement operation on `IsApproved` and throws a Arabic `BusinessRuleException`. The memo rule belongs in the same place.
- `UploadVersionAsync` (`:131`) is the only way to *start* a stage — it calls `EnsureSubProjectExistsAsync` then `EnsurePreviousStageCompletedAsync`.

## The "active memo" rule

Active = the most recently created memo linked to the sub-project. Ordered by `PresentationMemo.CreatedAt` descending, tie-broken by `Id` descending so the result is deterministic — matching the tie-break convention already used for execution stages (`358ab80`).

The M:N table `PresentationMemoSubProject` stays. Nothing is deleted; older memos simply stop being surfaced on the مراحل الطرح page. This keeps history intact and avoids a destructive migration.

**No schema change is required for any of this.**

### Query shape

`GetSubProjectsAsync` must not regress the perf work in `b5efd9e` ("avoid cartesian-join queries on project lists"). Joining memo links into the main projection would multiply rows. Instead:

1. Existing query for the project rows (unchanged).
2. One follow-up query fetching the active memo for the returned ids.
3. Stitch in memory.

Two queries, no row multiplication.

## Changes

### Backend

`ProcurementSubProjectListItemDto` gains:
- `ActiveMemoId` (int?), `ActiveMemoTitle` (string?)
- `ContractingMethod` (int?), `ContractingMethodLabel` (string?) — read from the active memo

`ProcurementOverviewDto.PresentationMemos` → `ActivePresentationMemo` (`PresentationMemoBriefDto?`).

New guard in `ProcurementService`, called from `UploadVersionAsync`:

```
EnsureHasPresentationMemoAsync(subProjectId)
  → BusinessRuleException("لا يمكن بدء مراحل الطرح قبل إرفاق مذكرة عرض للمشروع")
```

Placed alongside the existing `IsApproved` check so every entry point inherits it.

### Frontend — `financial-list`

Three filter groups above the table, each a chip row with a live count, matching the existing `.ftab`/chip styling already in `financial.css`:

| Group | Buckets |
|---|---|
| المرحلة الحالية | كراسة الشروط · الإعلان · فتح المظاريف · التقييم الفني · التقييم المالي · العقد والترسية · مكتمل |
| نوع التعاقد | the methods actually present in the data, each with a count |
| مذكرة العرض | الكل · لها مذكرة · بدون مذكرة |

The stage bucket is derived from `completedStages`: `0 → كراسة الشروط`, `1 → الإعلان`, … `5 → العقد والترسية`, `6 → مكتمل`. This reuses the existing `stageCountFilter` mechanics unchanged — only the labels and the added counts are new, which satisfies both "filter by the 6 stages with counts" and "replace 0/6 with stage names" in one change.

Counts are computed over the search-filtered set but *before* the group's own filter, so a chip always shows how many rows selecting it would yield.

The table gains a مذكرة العرض column showing the active memo title (linked) and نوع التعاقد.

### Frontend — `procurement-workflow`

Header renders the single active memo instead of looping all linked memos. When none, the existing "لا توجد — إنشاء مذكرة عرض" prompt stays, and the stage accordion is visibly disabled to reflect the new server rule rather than letting the user hit an error.

## Open decisions

Two behaviour forks that change what the code does — asked before implementing:

1. **Gate strength** — does starting a stage require the memo merely *attached*, or *completed* (i.e. with the legal-affairs decision)?
2. **Link enforcement** — should linking a sub-project that already belongs to another memo be blocked outright, or allowed with newest-wins?

## Verification

`dotnet build` + `npx ng build`, then a live walkthrough: chip counts match row counts, stage names line up with actual progress, memo filter partitions the list, the guard rejects an upload on a memo-less project with the Arabic message, and a project with two memos shows only the newest on both pages.
