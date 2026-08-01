# AI-Assisted Excel Import Design

Supersedes parts of `docs/superpowers/specs/2026-07-30-excel-project-import-design.md` and `docs/superpowers/specs/2026-08-01-excel-import-suggested-approved-design.md`: the base parsing/reconciliation/commit flow for main and sub projects is **unchanged**. This spec adds two AI-assisted capabilities on top of it and a review step for the new one that touches financial data.

## 1. Background

Real-world approved-plan files have two problems the current deterministic importer doesn't handle:

1. **Header typos.** Confirmed live: a file using "المكون العيني" (no diacritic) instead of the reference "المكوّن العيني" (with shadda) failed outright with "تعذّر التعرف على أعمدة الملف" — already fixed for the diacritic case specifically (commit `e0ff3bc`), but any other typo/variant in a header cell still hard-rejects the whole file.
2. **Sub-project names encode measurable quantities that go unrecorded.** e.g. `"تطوير منظومة النظافة بمراكز ومدن المحافظة (مشترك محافظة) سيارة 30 طن وسيارة 50 طن و2 سيارة 6 طن لمدرية الطرق"` describes three separate vehicle counts. Today nothing captures this — it stays as inert text in the sub-project name.

Also confirmed and fixed in this same effort (commit `87652f9`): approved-mode's create-new-subproject path was hardcoding funding to 0 and project-level/component-type/accounting-unit/executive-agency to defaults instead of reading the row's real values.

## 2. AI Gateway

An ITI-provided gateway, tested and confirmed working:

- **Endpoint:** `http://apiaccess.iti.net.eg/api/v1/student/chat` (plain HTTP, not HTTPS — provider-side, not something we control)
- **Auth:** `Authorization: Bearer <key>` — key stored via `dotnet user-secrets` / environment variable (`SBG_API_KEY`), never committed, per this repo's existing `.gitignore` convention for AI keys.
- **Request:** `{ "model_id": "anthropic.claude-sonnet-4-6", "messages": [{"role":"user","content":"..."}], "system_prompt": "...", "max_tokens": N }`
- **Response:** `{ "output_text": "...", "usage": {...}, "status": "active", ... }` — `output_text` is plain text; when we need structured output we instruct the model to emit JSON and parse `output_text` ourselves (the gateway has no native JSON-mode/tool-calling parameter).
- **Budget:** $20 total on this key, shared across whatever else it's used for. Both AI paths below are designed to minimize call count (batch header-fix into 1 call/file, batch measurement extraction into ~1 call per 15 rows) rather than 1 call per row.
- This is a course-issued key ("just for testing" per explicit confirmation) — fine to build and use for this feature now; not a dependency to assume permanent.

New: `IAiGatewayClient` (Infrastructure layer) wrapping this HTTP call — single-purpose, both features below call through it with their own prompts.

## 3. Header typo tolerance

**Unchanged:** the existing deterministic scan (`ExcelImportParser.FindHeaderRow`) stays as the fast path — exact match after diacritic-stripping, scanning the first 10 rows, zero cost, handles the common case instantly.

**New fallback**, triggered whenever that scan fails to find a row where *all 13* headers match (i.e. on **any** single unmapped column, not just total failure):

1. Among the first 10 rows, pick the one with the most already-recognized headers (ties broken by earliest row). This is the header-row candidate.
2. If that row matches **fewer than half** of the 13 expected headers, it's not a typo case — it's the wrong file. Skip the AI call, throw the existing `"تعذّر التعرف على أعمدة الملف"` error as today.
3. Otherwise, call the AI gateway once with: the candidate row's *unmatched* cell texts, and the list of *still-unmapped* canonical headers. Ask it to return a JSON mapping `{ "<cell text>": "<canonical header or null>" }`.
4. Merge the AI-resolved mappings into `columnIndexByHeader` alongside the deterministic ones. If the AI leaves any canonical header unmapped, fail with the same existing error (never silently proceed with missing columns).

One call per file, only when needed, small prompt (just the leftover cells + leftover headers, not all 13 every time).

## 4. Measurement extraction

**When:** during **preview**, not commit — extracted measurements must be visible to staff before anything is written to the database, since this is inherently fuzzy AI judgment touching real funding/quantity data.

**Granularity:** batch ~15 sub-project names per AI call (not 1 call per row). For an 88-row file that's ~6 calls instead of 88.

**Extraction semantics** (confirmed via user examples): a single sub-project name can contain multiple distinct measurable mentions, each becoming its own `(name, value, unit)` triple. The model must use contextual judgment — it is not a fixed "always extract count" rule. Worked example:

> `"...سيارة 30 طن وسيارة 50 طن و2 سيارة 6 طن..."` → three rows:
> - (`عدد`, `1`, `سيارة 30 طن`)
> - (`عدد`, `1`, `سيارة 50 طن`)
> - (`عدد`, `2`, `سيارة 6 طن`)
>
> i.e. when a phrase names a specific type/spec of item with an implicit or explicit count, the **count** is the value, `عدد` is the measurement name, and the full descriptive phrase (including its spec, e.g. "طن" capacity) is the unit — not just the bare noun. Other phrasings (e.g. a road length in meters) would use a different measurement name (`طول`) and a different value/unit pairing entirely. The model decides per phrase; there is no fixed vocabulary of measurement names.

A sub-project can extract to zero, one, or several measurements. Prompt: batch of `{rowIndex, subProjectName}`, response: JSON array of `{rowIndex, measurements: [{name, value, unit}]}` (rows with nothing extracted simply have an empty or absent array).

**Preview response shape (new):** regardless of suggested/approved mode, add `List<RowMeasurementPreviewDto> { int RowIndex; string SubProjectName; List<ExtractedMeasurementDto> Measurements }` alongside the existing per-mode preview data — every row gets its extraction result shown, whether the row will create, match, or approve a sub-project.

**Review UI (new):** the confirm step gains a section listing every row's extracted measurements, editable per row: change name/value/unit inline, remove a measurement, or add one manually. This is what staff actually approve before commit — the AI's output is a draft, never auto-applied.

## 5. Commit-time creation

For each sub-project (newly created or matched) that ends up with one or more approved measurement entries after review:

1. For each `(name, value, unit)`: resolve the Measurement definition by exact name **scoped to that sub-project's MainProject → SubProgram** (per the existing domain model — `Measurement` is many-to-many with `SubProgram` via `MeasurementSubProgram`). If missing, create it (`IMeasurementService.CreateAsync`) linked to that SubProgram.
2. Resolve the Unit by exact name (global lookup). If missing, create it.
3. Ensure the Measurement's allowed-units set includes this Unit (add via the same create/update path if not already linked) — `SetValuesForSubProjectAsync` rejects a `UnitId` that isn't in the Measurement's `MeasurementUnits`.
4. Call `IMeasurementService.SetValuesForSubProjectAsync(subProjectId, { Values: [{MeasurementId, UnitId, Value}, ...] })` once per sub-project with all its approved measurements.

Failures here (e.g. a genuinely malformed AI value) are per-sub-project best-effort, same pattern as the rest of this importer: report in the existing `Failed` list, don't roll back the sub-project itself.

## 6. Error handling / degraded mode

- AI gateway unreachable or errors on the header-fallback call: treat as "still unresolved," surface the existing `"تعذّر التعرف على أعمدة الملف"` error — no worse than today.
- AI gateway unreachable or errors on a measurement-extraction batch: that batch's rows get zero extracted measurements (not a hard failure) — sub-project creation/matching proceeds normally, staff just see nothing to review for those rows and can add measurements manually afterward via the existing sub-project form.

## 7. Out of scope

- No change to main-project or sub-project matching/creation logic itself (already correct, now also carries full field data per commit `87652f9`).
- No retry/fallback to a different model on gateway failure — single model, single attempt per call, per §6 above.
- No persistence of the AI's raw responses/audit trail beyond normal application logging.
