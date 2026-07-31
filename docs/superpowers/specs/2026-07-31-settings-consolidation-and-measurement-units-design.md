# Settings Consolidation + Measurement Units Redesign — Design

> Date: 2026-07-31 | Branch: `main` | Status: Approved

## 1. Problem

Three follow-ups from the lookup-management/measurements work:

1. **Contractors, Executive Agencies, Users, and Measurements are top-level nav items today; they should live inside Settings** alongside the 11 lookup-management tabs, as one consolidated "administration" area.
2. **The measurement model conflates "unit" with "value"** — `Measurement` currently has a single fixed `Unit` string, but a real measurement (e.g. "الارتفاع") needs to support several units (متر، سنتيمتر، كيلومتر), with staff choosing *which unit* per sub-project when they record a value — not one unit baked into the measurement definition.
3. **The measurement modal's sub-program picker is a flat checkbox list** of every sub-program in the system, with no grouping — unusable once there are more than a handful. It should group by Main Program (collapsible), Sub Programs nested as checkboxes inside.

## 2. Settings Becomes a Routed Shell

`/app/settings` changes from one component with in-memory tab-switching (a `signal<TabKey>`) to a parent route with child routes — one per tab. This is required because Contractors/Agencies/Users/Measurements are full pages with their own rich forms, not simple `{id, name}` lists the existing generic `SettingsLookupTable` component can render.

- `Frontend/src/app/app.routes.ts`: `users`, `contractors`, `agencies`, `measurements` routes move from being direct children of `app` to children of `settings` (e.g. `/app/settings/contractors`). The `users` route keeps its existing `roleGuard([Roles.PlanningManager, Roles.SuperAdmin])`. The generic lookup tabs (main-programs, sub-programs, governorates, markaz, villages, priorities, statuses, component-types, project-levels, accounting-units, contract-types, units — see §3) each become their own child route too, each rendering a thin wrapper around the existing `SettingsLookupTable` component with that type's config (replacing today's single `Settings` component that internally switches on a `TabKey` signal).
- `Settings` becomes a shell component: a sidebar listing all 16 tabs (12 lookups + Contractors + Agencies + Users + Measurements) as `routerLink`s, plus a `<router-outlet>` for the active child. The `Users` sidebar entry is hidden for non-managers (`*ngIf`-equivalent on `isManager()`), matching its current top-level restriction; every other entry stays visible to all staff, matching this app's existing convention.
- `Frontend/src/app/layout/main-layout/main-layout.ts`: `allNav` loses the `المقاولون`/`الجهات التنفيذية`/`إدارة المستخدمين`/`القياسات` entries — only `الإعدادات` remains as the entry point for all of this.
- The `Contractors`/`Agencies`/`Users`/`Measurements` components themselves are **not modified** beyond being re-routed — same templates, same services, same behavior. Any internal link that hardcodes the old path (e.g. a "cancel and go back" button, if one exists) gets updated to the new nested path.
- Old direct routes (`/app/contractors`, `/app/agencies`, `/app/users`, `/app/measurements`) are removed, not redirected — this is an internal admin tool, no external bookmarks to preserve.

## 3. Measurement / Unit Redesign

### 3.1 New `Unit` lookup (distinct from `AccountingUnit` — explicitly discussed and confirmed different concepts)

A new global, flat `Unit { Id, Name }` lookup (e.g. "متر", "سنتيمتر", "كيلومتر", "عدد") — the 12th generic settings tab, built exactly like the other 8 flat lookups (own `CreateUnitAsync`/`UpdateUnitAsync`/`DeleteUnitAsync` in `LookupService`, own route/controller actions in `LookupsController`, own tab in the Settings shell reusing `SettingsLookupTable`).

`AccountingUnit` is untouched and stays a completely separate concept (a single administrative/budget classification per sub-project, sourced from the original Excel import) — deliberately not reused for measurement units per the explicit conversation confirming they're unrelated.

### 3.2 `Measurement` loses its single `Unit`; gains many-to-many with `Unit`

- `Measurement { Id, Name }` — `Unit` string property removed.
- New join entity `MeasurementUnit { Id, MeasurementId, UnitId }` — many-to-many, mirroring the existing `MeasurementSubProgram` join exactly.
- `SubProjectMeasurementValue` gains `UnitId` (FK to `Unit`, required) alongside its existing `MeasurementId`/`SubProjectId`/`Value` — a recorded value is now "this measurement, in this unit, this value" rather than implicitly inheriting one fixed unit from the measurement definition.
- Confirmed with the user: unit choice is **per sub-project**, not fixed once per measurement — the same measurement can be recorded in meters on one sub-project and centimeters on another.

### 3.3 Measurement management UI changes

The add/edit measurement modal (`Frontend/src/app/features/measurements/measurements.ts`/`.html`) gains a second multi-select — Units — alongside the existing sub-program picker, using the same checkbox-list pattern. The single "الوحدة" text input is removed.

The measurements table gains a "الوحدات" column (chips, same pattern as the existing "البرامج الفرعية المرتبطة" chips column).

### 3.4 Recording a value (sub-project form Step 4)

Each applicable measurement's row changes from a single labeled number input to three parts:
- The measurement's name (label, unchanged).
- A **required** unit `<select>`, populated only from that measurement's linked units (via `MeasurementDto.UnitIds`/`UnitNames`, the same shape as today's `SubProgramIds`/`SubProgramNames`).
- The value (number input, still optional — leaving it blank means "not recorded", matching today's behavior).

If a unit isn't selected but a value is entered, that's a validation error (can't record a value without knowing its unit). If a measurement has only one linked unit, it does not get auto-selected specially — staff still picks it from the (single-option) dropdown, keeping the UI uniform.

`SubProjectMeasurementValueDto`/`SetMeasurementValueDto` (backend) and their frontend counterparts gain `UnitId` (`SetMeasurementValueDto`, write side) and `UnitId`/`UnitName` (`SubProjectMeasurementValueDto`, read side).

## 4. Hierarchical Sub-Program Picker

The measurement modal's sub-program checkbox list (currently a flat list of every `SubProgramLookup` in the system) becomes grouped by Main Program: each Main Program renders as a collapsible header (closed by default, matching the row-expand accordion interaction already used on the Contractors/Agencies pages), with its Sub Programs as checkboxes nested inside. Expanding/collapsing is pure UI state (no extra API calls — the full sub-program list, including each one's `mainProgramId`, is already fetched up front).

## 5. Out of Scope

- No migration/backfill logic needed for `SubProjectMeasurementValue.UnitId` — no real (non-test) measurement values exist yet in any environment, since this feature only just shipped.
- No change to `AccountingUnit`, `ComponentType`, `ProjectLevel`, or any of the other 10 existing lookup types beyond their tabs moving under the new routed Settings shell.
- No change to Contractors/Agencies/Users/Measurements pages' internal behavior — purely a navigation/routing relocation.
- Aggregating or reporting on measurement values across units (e.g. converting cm to m for a total) — still just capture and display, per the original measurements design's stated scope.

## 6. Testing

Manual, via dev servers (no test suite in this repo, per established convention):
- `/app/settings` shows a sidebar with all 16 areas; each is independently navigable via direct URL (e.g. typing `/app/settings/contractors` works, not just clicking the sidebar).
- Confirm `Users` tab is hidden from a non-manager account's sidebar, all other 15 remain visible.
- Confirm the old top-level nav no longer shows المقاولون/الجهات التنفيذية/إدارة المستخدمين/القياسات, and the old direct routes (`/app/contractors` etc.) no longer resolve.
- Create a `Unit` (e.g. "متر") via its new settings tab.
- Create a measurement, link it to 2 units and 1 sub-program via the redesigned modal; confirm the sub-program picker groups by Main Program with expand/collapse, and the table shows both new chip columns.
- Add a sub-project under that sub-program; confirm Step 4 shows a required unit dropdown (only the 2 linked units) plus the value input; record a value with a unit, save, re-open to confirm both the unit and value are pre-filled.
- Confirm submitting a value without a unit selected is rejected with a clear message.
