# Tasks — Swarm Admin preview

Branch: `dashboard-preview` · Deliverable: Admin screen + picture captures

## Phase 1 — Foundation

- [x] **1. Scaffold, tokens, contrast audit**
  - [x] Vite + React + TS + Tailwind v4 runs
  - [x] Three-layer `tokens.css` with dark default and light swap
  - [x] Self-hosted Newsreader / Public Sans / JetBrains Mono
  - [x] `node scripts/audit-contrast.mjs` passes
  - [x] `npm run lint` and `npm run build` pass
  - [x] Commit: `add dashboard scaffold and Kanagawa token layer`

- [x] **2. Shell, routes, meta, legal stubs**
  - [x] Sidebar, page frame, preview notice, legal stubs
  - [x] Unique title per route
  - [x] Commit: `add dashboard shell and preview routes`

## Checkpoint — Foundation
- [x] Lint, build, tests, contrast all pass.

## Phase 2 — Admin

- [x] **3. Admin state reducer (test first)**
  - [x] Pause/resume, tabs, collapse, selection, counts covered
  - [x] Commit: `add tested admin queue state`

- [x] **4. Queue rail, now band, throbber, crab**
  - [x] Queue/History, now band, rows, paused + empty states
  - [x] Commit: `build admin queue rail and live now band`

- [x] **5. Workbench grouping (test first)**
  - [x] Grouping, collapse, selection, empty group covered
  - [x] Commit: `add tested workbench grouping`

- [x] **6. Workbench UI**
  - [x] Serif groups, open rows, steel marker, state words, crab empty group
  - [x] Commit: `build grouped workbench with work states`

## Checkpoint — Admin works
- [x] Lint, build, tests pass; queue and workbench interactions work.

## Phase 3 — Polish and proof

- [x] **7. Themes and responsive**
  - [x] 1440 / 768 / 390 dark captures, 1440 light capture
  - [x] Commit: `make admin preview responsive in both themes`

- [x] **8. Browser verification**
  - [x] Zero console errors/warnings, no failed requests
  - [x] Queue/History, collapse, selection, pause all click through

- [x] **9. Design critique**
  - [x] Nine axes walked; Blocking/Required fixed; verdict recorded
  - [x] Commit: `apply design critique fixes`

- [x] **10. Performance, SEO, legal, ship**
  - [x] Lighthouse desktop + mobile recorded
  - [x] Meta, OG, JSON-LD, favicon, robots, sitemap
  - [x] Legal stubs with visible placeholders
  - [x] `dist/404.html` and base-path check
  - [x] `npm run lint && npm run build && npm test`
  - [x] Commit: `finish admin preview, SEO, and deploy configuration`
