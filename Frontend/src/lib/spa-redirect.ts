const KEY = '__spa_redirect'

/**
 * GitHub Pages serves 404.html for unknown paths. That file stashes the
 * requested path and redirects to the app root; restore it before the router
 * mounts so a hard refresh or shared link lands on the intended route.
 */
export function restoreSpaRedirect() {
  if (typeof window === 'undefined') return
  const base = import.meta.env.BASE_URL
  const stored = sessionStorage.getItem(KEY)
  if (!stored) return
  sessionStorage.removeItem(KEY)
  window.history.replaceState(null, '', base + stored.replace(/^\//, ''))
}
