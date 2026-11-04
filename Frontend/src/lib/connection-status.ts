import type { DiagnosticsState } from './use-diagnostics'

export type ConnectionTone = 'pending' | 'live' | 'stale' | 'offline'

export interface ConnectionStatus {
  tone: ConnectionTone
  /** Open WebSocket connections on the server; null until the first snapshot. */
  count: number | null
  /** "live connection(s)", agreeing with the count. */
  noun: string
  /** Why the count is not live, or null when it is. */
  note: string | null
}

export function connectionNoun(count: number | null) {
  return count === 1 ? 'live connection' : 'live connections'
}

export function connectionStatus({ snapshot, error }: Pick<DiagnosticsState, 'snapshot' | 'error'>): ConnectionStatus {
  const count = snapshot ? snapshot.activeConnectionCount : null
  const noun = connectionNoun(count)

  if (error && snapshot) return { tone: 'stale', count, noun, note: 'Server unreachable. Showing last count.' }
  if (error) return { tone: 'offline', count, noun, note: 'Server unreachable. Retrying.' }
  if (!snapshot) return { tone: 'pending', count, noun, note: 'Connecting to server' }
  return { tone: 'live', count, noun, note: null }
}
