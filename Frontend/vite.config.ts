import { writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// GitHub Pages project sites serve from /repo/, Vercel from /. Never hardcode.
function normalizeBase(path: string | undefined) {
  if (!path) return '/'
  return path.endsWith('/') ? path : `${path}/`
}

const base = normalizeBase(process.env.BASE_PATH)

// Keep browser requests on the frontend origin; no backend CORS changes are needed.
const apiProxy = {
  '/api': {
    target: process.env.SWARM_API_TARGET || 'http://127.0.0.1:5142',
    changeOrigin: true,
    rewrite: (path: string) => path.replace(/^\/api/, ''),
  },
}

// GitHub Pages serves files from disk, so an unknown path returns 404.html.
// Emit one that keeps the requested path and bounces back into the SPA.
function githubPagesSpaFallback() {
  return {
    name: 'github-pages-spa-fallback',
    apply: 'build' as const,
    closeBundle() {
      const out = fileURLToPath(new URL('./dist/404.html', import.meta.url))
      writeFileSync(
        out,
        `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <title>Swarm</title>
    <script>
      (function () {
        var base = ${JSON.stringify(base)};
        var l = window.location;
        var path = l.pathname.indexOf(base) === 0 ? l.pathname.slice(base.length) : l.pathname;
        if (path) sessionStorage.setItem('__spa_redirect', path + l.search + l.hash);
        l.replace(base);
      })();
    </script>
  </head>
  <body><p>Redirecting&hellip;</p></body>
</html>
`,
      )
    },
  }
}

export default defineConfig({
  base,
  plugins: [react(), tailwindcss(), githubPagesSpaFallback()],
  server: { proxy: apiProxy },
  preview: { proxy: apiProxy },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})
