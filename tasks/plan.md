# Implementation Plan: Swarm Admin design preview

## Overview

A single Admin screen, built as a real React page against the approved Slate/Kanagawa
token layer, fed by fixtures, captured as a picture at three widths in dark and one in
light. The queue and workbench are vertical slices: each gets its state logic tested
first, then its composition, then a capture against the live dev server.

## Architecture Decisions

- **`Frontend/` owns the app.** The repo already reserves this directory for the
  dashboard; the backend stays untouched.
- **One page, real shell.** Admin is the only complete page. Other nav destinations
  render an honest preview notice rather than dead links or fake pages.
- **Pure state in `src/lib/admin-state.ts`.** Pause, tab, selection, and collapse are a
  reducer with no React inside, so they can be tested without rendering layout.
- **Tokens before components.** `tokens.css` is written and audited before the first
  component reads a variable.
- **Fixture data, marked as such.** No backend calls. The sidebar footer says `Demo data`.
- **Crab as state, not sticker.** Six poses map to six session states; the sprite is
  cropped from the supplied image and never animated.
- **Deploy-ready but not deployed.** Base path from `BASE_PATH`, 404 SPA fallback,
  router basename, Pages workflow, `vercel.json`, `.nojekyll`. No push.

## Task List

### Phase 1: Foundation

- [x] **Task 1 — Scaffold, tokens, contrast audit**
  - Acceptance: Vite + React + TS + Tailwind v4 runs; `tokens.css` has palette/theme/
    active layers; Newsreader, Public Sans, JetBrains Mono load locally; `npm run lint`
    and `npm run build` pass; `audit-contrast.mjs` reports every required pair passing.
  - Verify: `npm run lint && npm run build && node scripts/audit-contrast.mjs`
  - Files: `Frontend/package.json`, `vite.config.ts`, `tsconfig*`, `eslint.config.js`,
    `src/styles/*`, `scripts/audit-contrast.mjs`, `src/main.tsx`, `index.html`
  - Scope: M

- [x] **Task 2 — Shell, routes, meta, legal stubs**
  - Acceptance: `/admin` renders inside the shell; nav marks Admin active; `/`,
    `/overview`, `/work`, `/sessions`, `/privacy`, `/imprint`, `/accessibility` resolve;
    each page sets a unique title; no 404s; no console errors.
  - Verify: `npm test`, build, manual route check in browser.
  - Files: `src/App.tsx`, `src/components/shell/*`, `src/pages/*`, `src/lib/document-meta.ts`
  - Scope: M (no logic test beyond route resolution)

### Checkpoint: Foundation
- [x] Lint, build, tests, contrast audit pass.

### Phase 2: Admin slices

- [x] **Task 3 — Admin state reducer (TDD)**
  - Acceptance: tests cover pause/resume, tab switch, group collapse, row selection,
    and derived counts. Failing tests written first.
  - Verify: `npm test`
  - Files: `src/lib/admin-state.ts`, `src/lib/admin-state.test.ts`
  - Scope: S

- [x] **Task 4 — Queue rail, now band, throbber, crab**
  - Acceptance: tabs work; now band shows throbber + active request; rows show owner,
    harness, elapsed, state word; paused state shows the banner and disables sending;
    empty state uses the bounded warm-clay crab field.
  - Verify: tests from Task 3, build, visual check.
  - Files: `src/components/admin/QueuePanel.tsx`, `NowBand.tsx`, `RequestRow.tsx`,
    `src/components/ui/Throbber.tsx`, `Companion.tsx`, `EmptyState.tsx`
  - Scope: M

- [x] **Task 5 — Workbench grouping and collapse (TDD)**
  - Acceptance: items group by initiative; collapsed groups hide rows but keep counts;
    selection is single; empty group is reported.
  - Verify: `npm test`
  - Files: `src/lib/admin-state.ts`, `src/lib/admin-state.test.ts`, `src/data/fixtures.ts`
  - Scope: S

- [x] **Task 6 — Workbench UI**
  - Acceptance: serif group headings with counts and chevrons; open rows, no cards or
    separators; 2px steel marker on selected row; state words; Documentation empty
    group uses the crab; long titles wrap without breaking alignment.
  - Verify: build, visual check, keyboard check.
  - Files: `src/components/admin/Workbench.tsx`, `WorkItemRow.tsx`,
    `src/components/ui/StateWord.tsx`
  - Scope: M

### Checkpoint: Admin works
- [x] Lint, build, tests pass; queue and workbench interactions work end to end.

### Phase 3: Polish and proof

- [x] **Task 7 — Themes and responsive**
  - Acceptance: dark default + working light toggle; 390px stacks panes, 768px reflows,
    1440px holds the two-pane ledger; no horizontal scroll; focus rings visible.
  - Verify: captures at 1440 / 768 / 390 dark, 1440 light.
  - Files: shell and admin components, `src/styles/index.css`
  - Scope: M

- [x] **Task 8 — Browser verification**
  - Acceptance: zero console errors/warnings, no failed requests, snapshot shows real
    content, primary actions click through.
  - Verify: chrome-devtools MCP.
  - Files: none (fixes as found)
  - Scope: XS

- [x] **Task 9 — Design critique and fixes**
  - Acceptance: nine rubric axes walked; every Blocking/Required finding fixed and
    re-captured; verdict recorded.
  - Verify: screenshots + re-critique.
  - Files: as findings require
  - Scope: M

- [x] **Task 10 — Performance, SEO, legal, ship locally**
  - Acceptance: Lighthouse desktop/mobile recorded; meta/OG/JSON-LD/favicon;
    robots + sitemap; self-hosted fonts; privacy/imprint/accessibility stubs with
    visible placeholders; `dist/` base and `404.html` verified; final commit.
  - Verify: Lighthouse, `grep -o 'src="[^"]*"' Frontend/dist/index.html`,
    `ls Frontend/dist/404.html`, `npm run lint && npm run build && npm test`.
  - Files: `index.html`, `public/*`, legal pages, `vercel.json`, workflow
  - Scope: M

### Checkpoint: Complete
- [x] All acceptance criteria met; captures delivered; branch committed, not pushed.

## Risks and Mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| `design.md` describes a larger LLM app | High | Use its visual grammar and admin composition only; no model/training pages |
| Crab source is one raster PNG on charcoal | Med | Flood-fill background transparent from edges, keep interior screen blacks, trim each pose |
| Throbber contract is elaborate | Med | Author a fixed 28×25 lattice figure with a seeded tree; freeze under reduced motion; state word carries meaning |
| Admin features are not in the README | High | Label pause/permission as assumptions in the gap list; keep them plausible and reversible |
| Fonts block render | Low | `@fontsource` self-host + `font-display: swap`; only needed weights |

## Open Questions

- Is `Pause queue` real, or should it be replaced before the next pass?
- Owner-only Admin, or all project members?
- Fourth Workbench group: `Documentation` or `PRs`?
