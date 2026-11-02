// Matches the JSON records returned by the ASP.NET Core diagnostics endpoint.
export interface ConnectionSnapshot {
  id: string
  connectedAt: string
  disconnectedAt: string | null
  durationSeconds: number
  duration: string
  messagesSent: number
  messagesReceived: number
}

export interface DiagnosticsSnapshot {
  startedAt: string
  uptimeSeconds: number
  uptime: string
  activeConnectionCount: number
  totalConnections: number
  messagesSent: number
  messagesReceived: number
  sendDuration: {
    sampleCount: number
    sampleLimit: number
    averageMilliseconds: number | null
    maximumMilliseconds: number | null
    slowThresholdMilliseconds: number
    countAboveThreshold: number
  }
  activeConnections: ConnectionSnapshot[]
  recentDisconnections: ConnectionSnapshot[]
}

// A same-origin request through Vite locally, or the host's reverse proxy in production.
export const DIAGNOSTICS_URL = '/api/diagnostics'
