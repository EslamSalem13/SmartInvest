# Sub-Project Details Page Overhaul — Design

## Goal

The sub-project details page (`/app/projects/:id`, `SubProjectDetails` component) currently mixes main-project data into its header incorrectly, hides fields that are already available, uses a dead technical-specifications system instead of the real Measurement/Unit system, fakes its geo-location tab with a static placeholder, and wastes its fourth tab on a redundant "sibling sub-projects" list. This design fixes the header bug, expands the basic-info tab with real data, replaces the specs tab with the real measurements UI, adds a real interactive map, and replaces the siblings tab with a procurement/memo summary.

## Architecture

Backend: extend `SubProjectDetailDto` and `SubProjectMappingProfile` (`Backend/src/SmartInvest.Application`) with fields that already exist on related entities but aren't surfaced on the single sub-project detail endpoint (`GET /api/subprojects/{id}`). No new tables, no new services, no new endpoints — the procurement/memo data and the measurement data are already served by existing endpoints (`GET /api/subprojects/{id}/procurement`, `GET/PUT /api/subprojects/{id}/measurement-values`).

Frontend: rework `SubProjectDetails` (`Frontend/src/app/features/projects/details/sub-project-details.ts/.html/.css`) tab-by-tab. Tab 2 and Tab 4 get their underlying data source swapped (specs → measurements, siblings → procurement). Tab 3 gets a real Leaflet map. Tab 1 gets more fields rendered plus the header code-chip bug fixed.

## Global Constraints

- No automated test suite in this repo — verification is build + live browser check (dev server via Browser pane), matching how every prior feature in this session was verified.
- Batch commits into few large commits, not one per fix (standing user preference).
- Never push to origin without a fresh explicit "yes" each time.
- Arabic UI strings throughout (matches existing app).
- Leaflet + OpenStreetMap tiles for the map — no API key, no billing (explicit user choice over Google Maps).

## Part 1 — Header code-chip fix + `SubProjectDetailDto` extension

**Bug:** `sub-project-details.html` line ~22 renders `الكود الرئيسي: {{ project()!.mainProjectName }}` — labeled "main project code" but bound to the main project's *name*. `SubProjectDetailDto` has no `MainProjectCode` field at all (unlike `SubProjectListItemDto`, which already has one), so the frontend literally cannot bind the code today.

**Backend changes (`Backend/src/SmartInvest.Application/DTOs/SubProjectDtos.cs`, `SubProjectDetailDto`):**
Add:
```csharp
public string MainProjectCode { get; set; } = string.Empty;
public int SubProgramId { get; set; }
public string? ContractorName { get; set; }
public string? ContractTypeName { get; set; }
public string? ContractNumber { get; set; }
public decimal? ContractValue { get; set; }
public IReadOnlyList<SubProjectFinancialYearDto> FinancialYears { get; set; } = new List<SubProjectFinancialYearDto>();
```
`SubProjectFinancialYearDto` already exists (used elsewhere for the financial-year join) — reuse it, don't redefine.

**Mapping (`Backend/src/SmartInvest.Application/Common/Mappings/SubProjectMappingProfile.cs`, the `CreateMap<SubProject, SubProjectDetailDto>()` block):**
Add `ForMember` entries:
- `MainProjectCode` ← `src.MainProject.MainProjectCode`
- `SubProgramId` ← `src.MainProject.SubProgramId`
- `ContractorName`, `ContractTypeName`, `ContractNumber`, `ContractValue` ← a new private static helper `GetLatestAssignment(SubProject)` (same ordering as the existing `GetLatestContractorName` — `OrderByDescending(a => a.AssignmentDate).First()`), reading `.Contractor?.ContractorName`, `.ContractType?.Name`, `.ContractNumber`, `.ContractValue` off the single resolved assignment (avoid calling `.OrderByDescending().First()` four separate times)
- `FinancialYears` ← `src.FinancialYears` (AutoMapper resolves the nested `SubProjectFinancialYear → SubProjectFinancialYearDto` map already registered elsewhere)

**Frontend model (`Frontend/src/app/core/models/project.models.ts`, `SubProjectDetail`):** add the mirrored fields (`mainProjectCode: string`, `subProgramId: number`, `contractorName: string | null`, `contractTypeName: string | null`, `contractNumber: string | null`, `contractValue: number | null`, `financialYears: SubProjectFinancialYear[]`).

**Frontend template fix:** change the header chip to `الكود الرئيسي: {{ project()!.mainProjectCode || '—' }}`.

## Part 2 — Tab 1 (البيانات الأساسية): render hidden fields + new fields

These fields are already populated on `SubProjectDetail` today (AutoMapper convention mapping, same property names on the `SubProject` entity) but never rendered: `goal`, `socialImpact`, `economicImpact`, `environmentalImpact`, `greenInvestmentLink`, `projectNature`, `accountingUnitName`. No backend work for these seven — purely a template addition.

Add to the "نظرة عامة" info-grid: المستوى (already there), الوحدة الحسابية, طبيعة المشروع.
Add a new "الأثر والاستدامة" card: الهدف (goal), الأثر الاجتماعي, الأثر الاقتصادي, الأثر البيئي, and رابط الاستثمار الأخضر rendered as an `<a>` link (only if non-null).
Add a new "بيانات التعاقد" card (only rendered if `contractorName` is non-null, since many sub-projects have no assignment yet): المقاول, نوع العقد, رقم العقد, قيمة العقد (money-formatted).
Add "السنوات المالية" as a chip list under the funding card, one chip per `financialYears[]` entry showing `financialYearName`.

## Part 3 — Tab 2: المواصفات الفنية → القياسات (measurements)

Drop `SpecificationsService`/`ProjectSpecification` usage from this component entirely (it's the only remaining frontend consumer per the codebase check — backend `ProjectSpecificationService`/`ProjectSpecificationsController` are left untouched, out of scope, in case anything server-side still depends on them; only this page's frontend usage is removed).

New tab content mirrors the existing "القياسات المخصصة" step (Step 4) of `sub-project-form.ts`:
- On tab activation, load `MeasurementsService.getValuesForSubProject(subId)` (current recorded values) and `MeasurementsService.getApplicable(project()!.subProgramId)` (available measurement definitions, for the add-row autocomplete) — same two calls Step 4 already makes.
- Render current values as a table (اسم المقياس / القيمة / الوحدة / إجراءات), each row editable inline or via the same add/edit modal pattern already used for specs (reuse the existing modal shell, swap the fields to measurement name + value + unit, with `<datalist>` autocomplete sourced from `applicable`, matching Step 4's UX).
- Save calls `MeasurementsService.setValuesForSubProject(subId, values)` — same resolve/auto-create semantics Step 4 already relies on, no new backend logic.

Tab button label changes from "المواصفات الفنية" to "القياسات".

## Part 4 — Tab 3: real map (Leaflet + OpenStreetMap)

Add `leaflet` + `@types/leaflet` as npm dependencies (`Frontend/package.json`). No environment/API-key config needed (OSM tiles are keyless).

Replace the fake `.map`/`.pin` placeholder div with a real Leaflet map instance:
- Container div with fixed height (e.g. 420px), Leaflet initialized in `ngAfterViewInit`/on tab activation (must be visible before `L.map()` sizes it correctly — initialize lazily the first time `tab() === 'location'` is reached, not eagerly on component construction).
- Center: `project()!.latitude`/`longitude` if set, else fall back to a sensible default center for the governorate (or Egypt-wide default if nothing is set).
- A single draggable marker. Marker `dragend` event and map `click` event both update a local `pickedLat`/`pickedLng` signal and move the marker.
- Show the currently-picked coordinates as text (reuse existing `.coords` styling).
- A "حفظ الموقع" button, disabled until the picked coordinates differ from the loaded ones, calls `ProjectsService.updateSubProject(subId, dto)` with the full `UpdateSubProject` payload built from the already-loaded `project()` data plus the new `latitude`/`longitude` (the update endpoint requires the full DTO, not a partial patch — same constraint the edit-sub-project modal already works within). On success, reload `project()` and show a brief success indicator; on error, `alert(err?.error?.message ?? ...)` matching this component's existing error-handling style.
- Must call `map.remove()` / clean up the Leaflet instance on component destroy (`ngOnDestroy`) to avoid a leaked map instance if the user navigates away and Angular later reuses the DOM node.

Tab label stays "الموقع الجغرافي".

## Part 5 — Tab 4: المشاريع الفرعية → الطرح والعروض (procurement summary)

Drop the sibling-sub-projects loading (`loadSiblings`, `siblings` signal, `getMainProject` call) — this data is redundant (already visible from the Projects table itself) and the user confirmed it should be replaced, not kept as a second tab.

New tab content, backed by `FinancialService.getOverview(subId)` (existing endpoint, zero backend work):
- Stage checklist: one row per `stages[]` entry, ordered by `order`, showing `stageLabel` with a ✓/pending indicator from `isCompleted`.
- مذكرات العرض list: one row per `presentationMemos[]` entry, showing `title` and a ✓/pending indicator from `isCompleted`.
- A "فتح إدارة كاملة" button/link routing to `/app/financial/{subId}` (existing `ProcurementWorkflow` route) for full stage/memo management (file uploads, completion toggles, advance-payment confirmation) — this summary tab is read-only by design (per user's explicit choice), full interaction stays on the existing financial page.
- Empty state if `stages` is empty or the endpoint 404s (a sub-project with no procurement record yet) — show "لا توجد بيانات طرح مسجّلة بعد" rather than erroring.

Tab button label changes from "المشاريع الفرعية" to "الطرح والعروض".

## Error Handling

- All new API calls follow the existing component's pattern: `error.set(...)` for page-level load failures, `alert(err?.error?.message ?? 'رسالة افتراضية')` for action failures (save location, save measurement) — consistent with `saveSpec`/`deleteSpec` today.
- Procurement summary tab treats a missing/empty overview as an empty state, not an error (a sub-project may legitimately have no procurement workflow started yet).

## Testing

No automated test suite exists in this repo. Verification: `dotnet build` for backend changes, Angular build for frontend, then live browser walkthrough via the Browser pane covering: header code chip shows a real code, all seven previously-hidden fields render correctly for a project that has them populated, contractor/contract-type/financial-year card appears only when data exists, measurements tab loads current values and successfully adds a new measurement end-to-end, map tab renders a real map centered correctly, dragging/clicking the marker and saving location persists correctly across a page reload, procurement tab shows real stage/memo data matching what `/app/financial/{id}` shows for the same sub-project, and the "فتح إدارة كاملة" link navigates correctly.
