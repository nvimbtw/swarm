# Swarm dashboard design system

A quiet monitoring surface for people watching agents and connections. It should read like a
well-kept instrument panel: warm, low-glare, dense where the data is dense, and honest about
what is live and what is demo data.

The source of truth is code: `src/styles/tokens.css` (palette and roles) and `src/styles/index.css`
(Tailwind theme, type scale, component classes). This file explains the rules; if they disagree,
fix one of them.

## Colour

Three layers. Components use only layer 3 (Tailwind names such as `bg-surface`, `text-ink-muted`).

1. **Palette:** raw hexes, Kanagawa lineage: `dragon` (dark neutrals), `lotus` (light neutrals),
   `steel` (action blue), plus ochre, sage, amber, red, mauve, aqua and clay accents.
2. **Theme roles:** `--bg-*`, `--text-*`, `--border-*`, `--action-*`, `--status-*`, `--state-*`.
   Light resolves on `:root, .theme-light`; dark on `.dark`.
3. **Tailwind colours:** `canvas`, `surface`, `raised`, `sunken`, `hover`, `selected`, `ink`,
   `ink-secondary`, `ink-muted`, `ink-faint`, `line`, `line-strong`, `primary`, `quiet`,
   `success`, `warning`, `danger`, `info`, work states (`queued`, `progress`, `verification`,
   `closed`).

| Role | Dark | Light | Use |
| --- | --- | --- | --- |
| Canvas | `#181616` | `#f4efe4` | Page background |
| Surface | `#1d1c19` | `#fbf7ee` | Sidebar, sheets |
| Ink | `#c5c9c5` | `#4f4f58` | Primary text |
| Line | `#4a4843` | `#bfb29a` | 1px borders |
| Primary | `#7fa8be` | `#3e6478` | Actions, current page |
| Success | `#8a9a7b` | `#4f6b45` | Live, connected |
| Warning | `#d7a657` | `#8a5a12` | Stale, unreachable |

No gradients, glows or glass. Status colour always comes with a word (WCAG 1.4.1).

## Type

| Family | Role |
| --- | --- |
| Newsreader (400, 500, italic) | Page titles, section headings, `.figure` numerals |
| Public Sans (400–600) | Body, labels, controls |
| JetBrains Mono (400, 500) | `.tag`: IDs, hostnames, durations |

The scale runs from `text-2xs` (11px) to `text-5xl` (61px), with line height and tracking set per
step in `index.css`. Numbers use `.figure`, which sets tabular lining numerals. `.eyebrow` is the
only uppercase style and is kept for stat-tile terms.

## Space, shape, motion

- **Spacing:** 4px base (`--spacing: 0.25rem`). Sheet padding `1.5rem`, section gap `2rem`.
- **Radius:** sheet 6px, control 4px, small control 3px, marker 2px. Inner radius never exceeds
  outer radius.
- **Elevation:** a 1px-by-2px offset ink shadow (`shadow-sheet`, `shadow-overlay`). No blurred drop
  shadows.
- **Motion:** `--duration-fast` 120ms, `--duration-base` 200ms, `--duration-slow` 360ms, with
  `--ease-standard`. All durations collapse under `prefers-reduced-motion`. Polling updates never
  animate.

## Layout

The app shell is a fixed 240px sidebar at `lg` and up, and a sticky two-row top bar below it.
Main content is capped at 1180px. Page grids use `grid-cols-1` (`minmax(0,1fr)`) so wide tables
scroll inside their own region instead of widening the page.

## Live connection count

`src/components/shell/ConnectionCount.tsx` shows the server's open WebSocket count
(`activeConnectionCount` from `GET /diagnostics`) on every page. It sits in the sidebar footer,
and in the top bar on small screens. This covers raw `/ws` clients and local connectors on
`/ws/sessions`. A connector holds one connection for all of its sessions, so this number counts
connections, not sessions.

| State | Dot | Number | Text |
| --- | --- | --- | --- |
| Connecting | `ink-faint` | — | "Connecting to server" (bar: "connecting") |
| Live | `success` | `ink` | "N live connection(s)" |
| Stale (refresh failed after data) | `warning` | last count, `ink-secondary` | "Server unreachable. Showing last count." (bar: "last known") |
| Unreachable (never answered) | `warning` | — | "Server unreachable. Retrying." (bar: "unreachable") |

Rules:

- Never show `0` before data arrives. A dash means unknown, and zero is a real reading.
- The whole indicator links to Overview, which lists each connection.
- One shared polling loop (`DiagnosticsProvider`) feeds the shell and Overview. It refreshes
  once a second after the previous request finishes, and pauses while the tab is hidden.
- The link has an explicit accessible name (for example "3 live connections"). Without it,
  adjacent spans would read as "3live connections". The shell is not a live region; Overview
  owns the `role="status"`.
- Touch target is at least 44px in the top bar.

## Intentional exceptions

- **The count appears twice on Overview** (shell and the "Active connections" tile). The shell
  indicator is wayfinding and must stay in the same place on every page; the tile is the page's
  own figure.
- **The Admin page keeps fixture session counts.** It is a labelled design preview ("This
  workspace uses demo data"), and its sessions are agent sessions, a different quantity. The
  shell's "live" wording separates the two.
