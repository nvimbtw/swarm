# Performance and deploy checks

All checks run against the production build served from `Frontend/dist` at
`http://localhost:4173/swarm/admin` with `BASE_PATH=/swarm/`.

## Lighthouse

| Run | Performance | Accessibility | Best practices | SEO |
| --- | --- | --- | --- | --- |
| Desktop | 99 | 100 | 100 | 63 |
| Mobile | 75 | 100 | 100 | 63 |

Desktop metrics: FCP 0.5s, LCP 0.6s, TBT 110ms, CLS 0, TTI 0.7s.
Mobile metrics: FCP 2.4s, LCP 2.6s, TBT 790ms, CLS 0, TTI 3.4s.

The mobile lab run was taken while the host was under load (load average ~3.9,
with a terminal process at ~77% CPU). An earlier mobile run on the same build
family under lighter load scored 88 with TBT 250ms. The app ships 92 KiB of
JS and 22 KiB of CSS gzipped; the variance is host contention, not a new
render path. The LCP element in both runs is the now-band request title.

**SEO 63 is intentional.** This is an internal design preview:
`<meta name="robots" content="noindex, nofollow">` and `robots.txt` disallow
crawling. The score is capped by that decision. All other SEO audits pass.

## What is in the payload

- 92.2 KiB gzipped JS, 21.8 KiB gzipped CSS.
- Six companion poses as 64w/128w WebP variants selected with `srcSet`; the
  browser picks 64w for the 32px rows at DPR 1 and 128w for the 64px empty state
  at DPR 2.
- Six self-hosted WOFF2 faces (Newsreader 400/500/italic, Public Sans 400/500/600,
  JetBrains Mono 400/500 as needed). No third-party script or font request.
- No analytics, cookies, auth, or backend calls.

## Base path and SPA fallback

`BASE_PATH=/swarm/ npm run build` produces:

- `dist/index.html` with `/swarm/favicon.svg`, `/swarm/assets/...`, `/swarm/og.png`.
- `dist/404.html` with the `/swarm/` base baked into its redirect script; it stashes
  the requested path in `sessionStorage` and returns to the base, where the router
  restores it.
- Companion requests under `/swarm/companion/...`.
- `.nojekyll` in the artifact root.

Verified in Chrome:

- `/swarm/` and `/swarm/admin` return 200 and hydrate.
- `/swarm/privacy` loads directly and sets `Privacy · Swarm` plus the canonical
  `https://nvimbtw.github.io/swarm/privacy`.
- All 14 network requests for the admin route return 200; zero console errors or
  warnings.
- `npm run preview` must be started with the same `BASE_PATH` used for the build,
  otherwise the asset URLs and the preview server disagree.

## Interaction checks

- Queue/History tabs: click and ArrowRight; tabpanel visibility follows.
- Pause/Resume: button label and `aria-pressed` update; paused banner appears.
- Collapse all/Expand all: four groups toggle `aria-expanded`; counts stay visible.
- Row selection: steel marker on both the queue request and its workbench item.
- Theme toggle: dark ↔ light, no reload, token roles swap.
- Reduced motion: throbber `animation-name: none`, opacity 0.8.
- 390/768/1440 and long-title stress: no clipping, overlap, or horizontal scroll.

## Deploy configuration

- `.github/workflows/deploy.yml`: Node 22, `npm ci`, lint, test, build with
  `BASE_PATH=/${{ github.event.repository.name }}/`, upload `Frontend/dist`,
  deploy with `actions/deploy-pages`. Triggers on `main` and manual dispatch.
- `vercel.json`: installs/builds in `Frontend/`, publishes `Frontend/dist`,
  rewrites deep links to `/index.html`, and sets basic security headers.
- Neither target has been pushed or deployed from this branch.
