# Spec: Swarm Admin — design preview

## Objective

Build a high-fidelity, static preview of Swarm's **Admin** screen so the team can judge
the visual direction before the rest of the dashboard exists. The preview is a real
React page using the real token layer, fixtures instead of the unfinished ASP.NET API,
and it is captured as a picture at desktop, tablet, and phone widths.

Success is: a teammate opens the picture and can tell, without explanation, which
agents are connected, what is queued, which work item each request belongs to, and
what state each item is in — in the visual language of `design.md` and `appearance.md`.

**Audience:** the internal Swarm team (Tadej, Jan, Aleks, invited collaborators).
**This pass is not:** the rest of the dashboard, a live Discord integration, auth, or a
production deployment.

## Tech Stack

- React 19 + TypeScript
- Vite 7
- Tailwind CSS v4 via `@tailwindcss/vite` (CSS-first `@theme`)
- React Router 7 (`BrowserRouter`, basename from `import.meta.env.BASE_URL`)
- Vitest + jsdom + Testing Library
- ESLint 9 (flat config) + typescript-eslint
- Fonts self-hosted through `@fontsource` — Newsreader, Public Sans, JetBrains Mono
- No chart, icon, or component library. Icons are inline SVG; the throbber is authored.

## Commands

All commands run inside `Frontend/`.

```sh
npm run dev       # Vite dev server
npm run build     # tsc -b && vite build
npm run preview   # serve dist/
npm run lint      # eslint .
npm test          # vitest run
npm test -- --watch
node scripts/audit-contrast.mjs   # token-level WCAG audit, both themes
```

## Project Structure

```text
Swarm/
├── SPEC.md
├── tasks/plan.md, tasks/todo.md
├── .github/workflows/deploy.yml
├── Frontend/
│   ├── index.html
│   ├── package.json, tsconfig*.json, vite.config.ts, eslint.config.js, vercel.json
│   ├── public/               # .nojekyll, robots.txt, sitemap.xml, favicon.svg, companion/
│   ├── scripts/              # audit-contrast.mjs, crop-companion.py
│   └── src/
│       ├── styles/           # tokens.css (palette → theme → active), index.css (@theme)
│       ├── data/             # fixtures.ts
│       ├── lib/              # state helpers, document meta
│       ├── components/
│       │   ├── shell/        # AppShell, Sidebar, SectionTexture
│       │   ├── ui/           # PageHeader, Tabs, StateWord, Companion, Throbber, EmptyState
│       │   └── admin/        # QueuePanel, Workbench, RequestRow, WorkItemRow, NowBand
│       ├── pages/            # AdminPage, PreviewNotice, PrivacyPage, ImprintPage, AccessibilityPage
│       └── test/             # setup.ts
```

## Code Style

Semantic tokens only in components. A raw hex belongs in `tokens.css` layer 1 and
nowhere else. Tailwind utilities resolve to CSS variables through `@theme`.

```tsx
// components/ui/StateWord.tsx
const tone = { queued: "text-kind-queued", verification: "text-status-warning" } as const;

export function StateWord({ state }: { state: WorkState }) {
  return <span className={cn("text-2xs tracking-wide", tone[state])}>{state}</span>;
}
```

- Components are function declarations, named exports, no default exports except pages.
- `cn` is a three-line local helper; no `clsx` dependency.
- Data shapes live in `src/data/fixtures.ts`; presentation never computes domain rules.
- Every interactive element has a visible `:focus-visible` ring from `--color-focus-ring`.

## Token System

Three layers in `Frontend/src/styles/tokens.css`, exactly as `appearance.md` requires:

1. **Palette** — `--palette-dragon-*`, `--palette-lotus-*`, `--palette-steel-*`,
   `--palette-ochre-*`, `--palette-sage-*`, `--palette-amber-*`, `--palette-red-*`,
   `--palette-mauve-*`, `--palette-aqua-*`, `--palette-clay-*`. Never read by a component.
2. **Theme** — one pair per role, light and dark side by side.
3. **Active mode** — `:root` resolves light, `.dark` resolves dark; components read only
   these. No component-level dark overrides.

Semantic roles: canvas, surface, raised, sunken, hover, selected, ink tiers
(primary/secondary/muted/faint), inverse, border subtle/strong/focus, action
primary bg/bg-hover/bg-active/fg, quiet action, focus ring, selection, status
success/warning/danger/info, work-state queued/in-progress/verification/closed,
domain kinds (model/tokenize/chat/rl/benchmark), destructive, texture ink/warm,
throbber base/spark.

Type: Newsreader (display, serif), Public Sans (body/UI), JetBrains Mono (identifiers
and timestamps only). Scale is 1.25 on a 16px base: `2xs 11`, `xs 12`, `sm 13`,
`base 15`, `md 16`, `lg 20`, `xl 25`, `2xl 31`, `3xl 39`, `4xl 49`, `5xl 61`; every
step carries its own line height and tracking. Spacing is the 4px unit, `--space-1` to
`--space-32`. Radius roles: sheet 6, control 4, control-sm 3, marker 2, composer 14,
bubble 12, full. Elevation is the crisp offset only: 1px right, 2px down, zero blur.
Motion: 120 / 200 / 360ms, one standard easing, all collapsed to 0.01ms under
`prefers-reduced-motion`.

Contrast floors from `appearance.md`: 4.5 for text, 3.0 for meaningful graphics, 1.8
for hairlines. `node scripts/audit-contrast.mjs` prints every pair in both modes and
fails the process if a floor is missed. The one intentional exception is
`--color-text-faint`, used only for non-essential decoration and never for copy.

## Pages and Sections

| Route | Page | In this preview |
| --- | --- | --- |
| `/admin` | Admin | Full screen, the deliverable |
| `/` | redirect | → `/admin` |
| `/overview`, `/work`, `/sessions` | Preview notice | Honest "not in this preview" state |
| `/privacy`, `/imprint`, `/accessibility` | Legal stubs | Clearly marked placeholders |

Admin composition, following `design.md`:

- **Sidebar** (240px): Swarm wordmark over a faint grey character field; nav
  Overview / Work / Sessions / Admin (active with a 2px steel marker); footer with
  connection count, sleeping crab, and legal links.
- **PageHeader**: display title `Admin`, functional counts (`4 connected · 2 waiting ·
  11 open`), primary action `Pause queue`, secondary `Open Discord`, faint grey
  `SectionTexture` field. No subtitle, no decorative rule.
- **Queue rail** (~340px): plain `Queue` / `History` tabs with muted counts; now band
  with the trellis throbber, active request, owner → harness, elapsed; compact request
  rows; empty and paused states.
- **Workbench**: grouped, collapsible work items. Groups are serif headings with counts
  and chevrons. Rows are open — no card, no separator — with a 2px steel marker on the
  selected row. State words carry meaning independently of colour. `Documentation` is
  shown as a real empty group using the bounded warm-clay crab field.

## Content Inventory

Voice: teammates mid-task. Plain, specific, present tense. No exclamation marks, no
motivational copy, no narrating background mechanics, no jargon the README does not use.

| Slot | Copy |
| --- | --- |
| Meta title | `Admin · Swarm` |
| Meta description | `Queue, history, and the workbench for connected Swarm sessions.` |
| Wordmark | `Swarm` |
| Nav | `Overview`, `Work`, `Sessions`, `Admin` |
| Page title | `Admin` |
| Header counts | `4 connected`, `2 waiting`, `11 open` |
| Primary action | `Pause queue` → `Resume queue` |
| Secondary action | `Open Discord` |
| Tabs | `Queue 3`, `History 12` |
| Now band | `Now`, `Review the connector brief`, `aleks → opencode`, `2m` |
| Queue row states | `queued`, `waiting`, `in progress` |
| Workbench heading | `Workbench`, filter `All groups` |
| Group names | `Swarm connector 6`, `Discord threads 4`, `Dashboard 3`, `Documentation 0` |
| Work states | `open`, `in progress`, `verification`, `closed` |
| Empty queue | `Nothing waiting` / `Requests appear here when someone sends one to a session.` |
| Empty history | `No history yet` / `Completed requests and session events are kept here.` |
| Empty workbench group | `No documentation items yet` / `Create one from a #docs thread.` |
| Paused queue | `Queue paused` / `New requests wait until you resume.` |
| Permission denied | `You do not have admin access` / `Ask a project owner to add you.` |
| Preview notice | `Not in this preview` / `This pass covers Admin only.` |
| Footer | `Privacy`, `Imprint`, `Accessibility`, `Demo data` |

## Content Gaps

- **Public URL** is assumed to be `https://nvimbtw.github.io/swarm/`; confirm before
  deploy. `robots.txt`, `sitemap.xml`, canonical, and OG URLs use it.
- **Legal operator** — company/person, address, contact email, jurisdiction. The
  Imprint and Privacy pages carry visible `[PLACEHOLDER]` markers, not invented details.
- **Discord invite URL** — the `Open Discord` action links to `https://discord.com`
  until a real server invite exists.
- **Machine/host names** in fixtures (`jan@framework`, `aleks@t14`) are illustrative.
- **Admin permission model** is not defined in the README. `Pause queue` and the
  permission-denied state are plausible admin operations, not confirmed product scope.
- **Logo** — no wordmark asset exists; the preview sets `Swarm` in Newsreader.
- **Crab sprite** — cropped from the supplied `image.png`; no vector source exists.
- **Privacy/analytics** — no analytics and no sign-ups, so no cookie banner is shipped.
  If that changes, the banner and processor list become required.

## Accessibility

- WCAG 2.2 AA target. Token-level contrast audit is part of the build.
- Semantic landmarks: `header`, `nav`, `main`, `aside`; one `h1` per page.
- Tabs follow the ARIA tabs pattern with arrow-key movement.
- Collapsible groups use `aria-expanded` and `aria-controls`; rows are links with a
  separate action target, never a button nested inside a link.
- Focus is visible on every interactive element in both themes.
- Status is never colour-only: every state has a word.
- `prefers-reduced-motion` freezes the throbber and removes transitions.
- The crab is decorative (`aria-hidden`) except where its state is already named in text.
- Touch targets ≥ 32px in compact rows, ≥ 40px for primary controls.

## Definition of Done

- `npm run lint`, `npm run build`, `npm test` pass.
- `node scripts/audit-contrast.mjs` passes every required pair.
- Admin renders with zero console errors or warnings and no failed network requests.
- Queue/History switch, group collapse, row selection, and pause/resume work.
- Captures exist at 1440, 768, and 390 in Dragon dark; 1440 in Lotus light.
- `dist/` proves deploy-readiness: assets rooted at the configured base, `404.html`
  present, `vercel.json` and the Pages workflow committed.
- Branch `dashboard-preview` holds the work. Nothing is pushed.

## Boundaries

- **Always:** semantic tokens; tests for state logic; both themes; keyboard paths;
  reduced motion; commit each task separately.
- **Ask first:** adding a dependency beyond the listed stack; changing the palette
  values; inventing product features not in the README or `design.md`.
- **Never:** commit secrets; call the unfinished backend; claim legal compliance;
  push or deploy; use lorem ipsum; put a raw hex in a component.

## Open Questions

1. Is `Pause queue` a real admin operation, or should the primary action be something
   the backend will actually expose?
2. Should Admin be reachable by every project member, or only owners? The permission
   copy assumes owners.
3. Is `Documentation` the right fourth Workbench group, or should it be `PRs`?
