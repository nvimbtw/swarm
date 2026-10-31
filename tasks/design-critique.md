# Design critique — Swarm Admin preview

Reviewed against the approved brief in `design.md` and `appearance.md`, using the
1440 dark, 1440 light, 768 dark, and 390 dark captures in `preview/`.

## Scores

| Axis | Score | Evidence |
| --- | --- | --- |
| Hierarchy | 4/5 | Page title → Workbench → group → row reads in one pass; the now band is the live focal point, the selected row the locator. |
| Spacing rhythm | 4/5 | 4px scale throughout; 40px between pane sections, 10px row padding, 16px group gap. The stacked 768 rail now packs to the top instead of stretching. |
| Alignment | 4/5 | Title, stats, tabs, rows and group headings share left edges; state and owner columns hold a common right edge. |
| Typography | 4/5 | Newsreader display, Public Sans UI, JetBrains Mono identifiers. 13px operational rows, 11px metadata, never a second decorative face. |
| Colour discipline | 4/5 | Kanagawa Dragon roles only; steel is the single action/selection accent, ochre/amber/sage carry state, warm clay is reserved for the empty field. |
| Imagery / icons | 4/5 | One 1.5px icon family; the crab is the only raster character. Six poses read at 32px and 64px; 20px avatars were removed because they smudged. |
| Motion | 4/5 | Only the trellis throbber moves; 2.8s breathe, 120ms colour transitions, frozen under `prefers-reduced-motion`. |
| State coverage | 4/5 | Default, selected, paused, History, empty Documentation group, light/dark, 390/768/1440, long-content stress. Queue-empty and permission-denied copy is authored but not reachable from the fixture set. |
| Template smell | 4/5 | No card grid, no repeated rules, no all-caps eyebrow on every block, no middle-dot meta strings, no arrow-appended links. The ledger is specific to the brief. |

## Required findings — fixed

1. **Muted metadata failed on the selected surface.** `text-muted` measured 4.22:1
   on `bg-selected`. Dark muted moved from `#8a8880` to `#938f87`; the audit now
   checks muted on selected and hover, and every state word on selected. Lighthouse
   accessibility is 100.
2. **The active Admin destination was off-screen at 390px.** The mobile bar kept
   sidebar padding and icons, so the fourth item overflowed. The bar variant now
   hides icons below `sm`, uses compact padding, and marks the active item with a
   2px bottom rule. All four destinations fit at 390.
3. **The 768px rail stretched the queue to 736px and pushed Sessions into a 170px
   gap.** The left column is now a two-column queue/Sessions grid from `md` to
   `xl`, and `content-start` stops the grid stretching to the workbench height.
4. **Middle-dot history metadata read as a template tell.** Replaced with an
   em dash (`#409 — aleks / opencode`).
5. **Companion images were 320px for 32px displays.** Generated 64w/128w variants,
   wired `srcSet`/`sizes`, and dropped the originals. The responsive-image audit
   passes and the initial image payload is roughly half.

## Optional findings — not changed

- At ≥640px, `Open #docs` sits at the far edge of the empty band. The band is
  composed as a ledger row, so the far-edge action is deliberate.
- At 390px the header actions wrap below the stats. The stacked header is clearer
  than compressing the primary button beside the counts.
- The light theme's selected surface is a cool steel tint against warm cream. It
  is the accent doing its job, not a second palette.
- Queue-empty and permission-denied states are written into the spec and the
  queue panel, but the fixture set never empties the queue. A future preview mode
  could expose them without changing the fixture content.

## Verdict

**Designed, not generated.** The crab companion, trellis throbber, Newsreader
ledger, and Kanagawa roles give the screen an identity that survives the
screenshot. The remaining risk is not visual: the queue is a static fixture, so
the preview should not be mistaken for a working admin surface.

## Stress check

A 1440 and 390 pass with a long request title, a long work-item title, and a long
session hostname. Titles wrap to 2–4 lines; the state column keeps its edge; no
overlap, clipping, or horizontal scroll.
