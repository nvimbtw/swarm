import { createContext, useContext, useEffect, useState } from 'react'
import { DIAGNOSTICS_URL, type DiagnosticsSnapshot } from './diagnostics'

export interface DiagnosticsState {
  snapshot: DiagnosticsSnapshot | null
  updatedAt: Date | null
  error: string | null
}

const REFRESH_MS = 1000
const TIMEOUT_MS = 5000

function isSnapshot(value: unknown): value is DiagnosticsSnapshot {
  const snapshot = value as DiagnosticsSnapshot | null
  return (
    !!snapshot &&
    typeof snapshot.uptime === 'string' &&
    Number.isFinite(snapshot.activeConnectionCount) &&
    Number.isFinite(snapshot.totalConnections) &&
    !!snapshot.sendDuration &&
    Array.isArray(snapshot.activeConnections) &&
    Array.isArray(snapshot.recentDisconnections)
  )
}

/**
 * The single polling loop for the diagnostics snapshot. Mounted once by
 * DiagnosticsProvider; everything else reads the result with useDiagnostics().
 */
export function useDiagnosticsPolling(): DiagnosticsState {
  const [state, setState] = useState<DiagnosticsState>({
    snapshot: null,
    updatedAt: null,
    error: null,
  })

  useEffect(() => {
    let disposed = false
    let refreshTimer: number | undefined
    let activeRequest: AbortController | undefined

    async function refresh() {
      refreshTimer = undefined
      const controller = new AbortController()
      activeRequest = controller
      const timeout = window.setTimeout(() => controller.abort(), TIMEOUT_MS)

      try {
        const response = await fetch(DIAGNOSTICS_URL, {
          signal: controller.signal,
          cache: 'no-store',
          credentials: 'same-origin',
        })

        if (!response.ok) {
          throw new Error(`The server returned HTTP ${response.status}.`)
        }

        const snapshot: unknown = await response.json()
        if (!isSnapshot(snapshot)) {
          throw new Error('The server returned an unexpected diagnostics snapshot.')
        }

        if (!disposed) {
          setState({ snapshot, updatedAt: new Date(), error: null })
        }
      } catch (error) {
        if (!disposed) {
          const message = controller.signal.aborted
            ? 'The diagnostics request timed out.'
            : error instanceof SyntaxError
              ? 'The server returned a response that is not JSON.'
              : error instanceof Error
                ? error.message
                : 'The diagnostics request failed.'

          // Preserve the last successful snapshot, but show it as stale in the UI.
          setState((current) => ({ ...current, error: message }))
        }
      } finally {
        window.clearTimeout(timeout)
        activeRequest = undefined
        schedule()
      }
    }

    // Schedule after completion so slow requests never overlap. A hidden tab
    // stops polling and refreshes as soon as it becomes visible again.
    function schedule() {
      if (disposed || refreshTimer !== undefined || activeRequest) return
      if (document.visibilityState === 'hidden') return
      refreshTimer = window.setTimeout(() => void refresh(), REFRESH_MS)
    }

    function onVisibilityChange() {
      if (document.visibilityState !== 'visible' || activeRequest) return
      window.clearTimeout(refreshTimer)
      void refresh()
    }

    document.addEventListener('visibilitychange', onVisibilityChange)
    void refresh()

    return () => {
      disposed = true
      document.removeEventListener('visibilitychange', onVisibilityChange)
      window.clearTimeout(refreshTimer)
      activeRequest?.abort()
    }
  }, [])

  return state
}

export const DiagnosticsContext = createContext<DiagnosticsState | null>(null)

export function useDiagnostics(): DiagnosticsState {
  const state = useContext(DiagnosticsContext)
  if (!state) throw new Error('useDiagnostics must be used inside <DiagnosticsProvider>.')
  return state
}
