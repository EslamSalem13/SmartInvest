# App-Wide Motion & Page Transitions — Design

## Context

SmartInvest today has scattered, inconsistent micro-transitions (73 occurrences of `transition` across 14 feature CSS files, each with its own hand-picked duration) and **zero** page-to-page transition — route changes are an instant, jarring swap. The app already has a small design-token system in `Frontend/src/styles.css` (colors, radii, text sizes, one shared easing curve `--ease: cubic-bezier(.22, .8, .3, 1)`) and already respects `prefers-reduced-motion` globally (`styles.css:639-645`).

Goal: make every click and every page switch feel smooth and consistent, without a visual redesign — this is a motion pass on top of the existing look, not a restyle.

## Scope

**In scope:**
1. Page-to-page transition (cross-fade) on every route change.
2. A small shared set of duration tokens, reusing the existing `--ease` curve.
3. Standardized micro-interactions app-wide: button press feedback, card hover-lift, table-row hover, modal/dropdown entrance (already exists — keep, just standardize the timing token used).
4. Reduced-motion compliance for the new page transition (the existing rule already covers everything else).

**Out of scope (explicitly):**
- Any color, spacing, or layout change.
- Per-page custom/bespoke transitions (e.g. a special animation just for the dashboard).
- Directional/slide transitions — cross-fade only (see Architecture).
- Animating third-party widgets (ECharts, Leaflet) beyond what they already do.
- `@angular/animations` package — not introduced.

## Architecture

### Page transitions — native View Transitions API

Enable via Angular Router's built-in `withViewTransitions()` feature in `Frontend/src/app/app.config.ts`:

```typescript
provideRouter(
  routes,
  withInMemoryScrolling({ scrollPositionRestoration: 'top' }),
  withComponentInputBinding(),
  withViewTransitions(),
),
```

This wraps every navigation in `document.startViewTransition()`. The browser snapshots the outgoing view and the incoming view as pseudo-elements (`::view-transition-old(root)` / `::view-transition-new(root)`) and cross-fades between them **by default** — no custom CSS is strictly required to get a working cross-fade. We add a small CSS override only to align the duration/easing with our own tokens instead of the browser default (~0.25s ease):

```css
::view-transition-old(root),
::view-transition-new(root) {
  animation-duration: var(--dur-page);
  animation-timing-function: var(--ease);
}
```

**Why cross-fade specifically (not a directional slide):** it sidesteps RTL slide-direction complexity entirely (a fade has no direction to get wrong in an RTL layout) and reads as understated/professional, matching the government-system tone. This was chosen explicitly over slide/rise alternatives during design review.

**Browser support / fallback:** unsupported browsers (older Firefox) simply don't animate — `startViewTransition` is feature-detected internally by Angular's router feature; navigation still happens instantly, nothing breaks. No fallback code needed.

**Reduced motion:** the View Transitions API does **not** automatically respect `prefers-reduced-motion` — this needs an explicit guard, added alongside the existing sitewide reduced-motion block:

```css
@media (prefers-reduced-motion: reduce) {
  ::view-transition-group(*),
  ::view-transition-old(*),
  ::view-transition-new(*) {
    animation-duration: .001ms !important;
  }
}
```

(`::view-transition-*` pseudo-elements live outside the normal flat tree, so the existing `*, *::before, *::after` reduced-motion rule does not reach them — this new rule is additive, not a replacement.)

### Motion tokens

Add to the `:root` token block in `styles.css`, next to the existing `--ease`:

```css
--dur-fast: .15s;   /* hover/press feedback — buttons, table rows */
--dur-base: .22s;   /* existing card/modal/theme-swap pattern (already this value today, just named now) */
--dur-page: .32s;   /* page cross-fade — slightly slower, it's a bigger visual change than a hover */
```

All existing hardcoded duration values (`.15s`, `.18s`, `.2s`, `.22s`, `.25s`) that match one of these get replaced with the token during implementation, so every transition in the app pulls from these 3 values instead of inventing new ones. `.18s` (icon rotate) and `.2s`/`.25s` (dropdown pop, card shadow) round to the nearest token (`--dur-fast` or `--dur-base`) — a few-ms difference is imperceptible and not worth a 4th token.

### Micro-interaction coverage

Standardize (not redesign) these interaction patterns as shared rules in `styles.css`, replacing the current per-feature-file duplicates where a feature file re-declares the same pattern:

| Element | Current state | Target |
|---|---|---|
| `.si-btn` (buttons) | has hover transition (`transform`, `box-shadow`, `background`, `border-color` @ `.15s`) | keep, retarget to `--dur-fast`; add `:active` micro-scale (`transform: scale(.97)`) for press feedback — currently missing |
| `.si-card` | has hover transition (`box-shadow`, `transform`, `border-color` @ `.25s`) | keep, retarget to `--dur-base`; this is the hover-lift, already present, just gets the shared token |
| Table rows (`tr`, `td`) | covered only by the theme-swap color rule (`.22s`, `styles.css:674-688`) | add a `background-color` hover transition using `--dur-fast` (currently rows change color on hover with no transition — instant snap) |
| Modals/dropdowns (`si-pop` keyframe) | has entrance pop (`.2s`) | keep, retarget to `--dur-fast` |
| Inputs/selects/textarea | covered by theme-swap rule already | no change needed |
| Theme (dark/light) swap | `.22s` on `.content`/`.si-card`/`.si-modal`/`.si-btn`/inputs/table | retarget to `--dur-base` (same value, now a named token) |

This is a **find-and-retarget** pass across the 14 feature CSS files (swap hardcoded values for `var(--dur-fast)` / `var(--dur-base)`) plus the 2 additions called out above (button press-scale, table-row hover transition) — not a rewrite of each file's styling.

## Testing / verification

No automated test suite covers CSS/visual behavior in this codebase (Vitest covers component logic). Verification is manual, in-browser, after implementation:
- Navigate between at least 4 different route types (list page, detail page, settings sub-page, a lazy-loaded feature) and visually confirm a smooth cross-fade with no flash-of-blank or layout jump.
- Toggle OS-level "reduce motion" and confirm page transitions and all micro-interactions become instant.
- Click through buttons/cards/table rows/a modal in both light and dark mode to confirm smooth, consistent timing.
- Confirm no visual/layout regression on at least one RTL-heavy screen (e.g. الإدارة المالية).

## Global constraint carried into the plan

Every commit must build on its own (`dotnet build` unaffected — this is frontend-only; `npm run build` must stay green after each task).
