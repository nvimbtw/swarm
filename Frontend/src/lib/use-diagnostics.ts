import { useEffect, useState } from 'react'
import { DIAGNOSTICS_URL, type DiagnosticsSnapshot } from './diagnostics'

interface DiagnosticsState {
  snapshot: DiagnosticsSnapshot | null
  updatedAt: Date | null
  error: string | null
}

export function useDiagnostics(): DiagnosticsState {
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
      const controller = new AbortController()
      activeRequest = controller
      const timeout = window.setTimeout(() => controller.abort(), 5000)

      try {
        const response = await fetch(DIAGNOSTICS_URL, {
          signal: controller.signal,
          cache: 'no-store',
        })

        if (!response.ok) {
          throw new Error(`The server returned HTTP ${response.status}.`)
        }

        const snapshot: DiagnosticsSnapshot = await response.json()
        if (
          !snapshot ||
          typeof snapshot.uptime !== 'string' ||
          !snapshot.sendDuration ||
          !Array.isArray(snapshot.activeConnections) ||
          !Array.isArray(snapshot.recentDisconnections)
        ) {
          throw new Error('The server returned an unexpected diagnostics snapshot.')
        }

        if (!disposed) {
          setState({ snapshot, updatedAt: new Date(), error: null })
        }
      } catch (error) {
        if (!disposed) {
          const message = controller.signal.aborted
            ? 'The diagnostics request timed out.'
            : error instanceof Error
              ? error.message
              : 'The diagnostics request failed.'

          // Preserve the last successful snapshot, but show it as stale in the UI.
          setState((current) => ({ ...current, error: message }))
        }
      } finally {
        window.clearTimeout(timeout)
        if (!disposed) {
          // Schedule after completion so slow requests never overlap.
          refreshTimer = window.setTimeout(() => void refresh(), 1000)
        }
      }
    }

    void refresh()
    return () => {
      disposed = true
      window.clearTimeout(refreshTimer)
      activeRequest?.abort()
    }
  }, [])

  return state
}
