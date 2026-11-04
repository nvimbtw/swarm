import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import type { DiagnosticsSnapshot } from '../../lib/diagnostics'
import { connectionNoun, connectionStatus } from '../../lib/connection-status'
import { DiagnosticsContext, type DiagnosticsState } from '../../lib/use-diagnostics'
import { ConnectionCount } from './ConnectionCount'

function snapshot(activeConnectionCount: number): DiagnosticsSnapshot {
  return {
    startedAt: '2026-11-04T12:00:00Z',
    uptimeSeconds: 60,
    uptime: '0h 1m 0s',
    activeConnectionCount,
    totalConnections: activeConnectionCount + 2,
    messagesSent: 0,
    messagesReceived: 0,
    sendDuration: {
      sampleCount: 0,
      sampleLimit: 100,
      averageMilliseconds: null,
      maximumMilliseconds: null,
      slowThresholdMilliseconds: 20,
      countAboveThreshold: 0,
    },
    activeConnections: [],
    recentDisconnections: [],
  }
}

function renderWith(state: DiagnosticsState, variant?: 'sidebar' | 'bar') {
  return render(
    <MemoryRouter>
      <DiagnosticsContext.Provider value={state}>
        <ConnectionCount variant={variant} />
      </DiagnosticsContext.Provider>
    </MemoryRouter>,
  )
}

describe('connectionStatus', () => {
  it('is pending with no count before the first snapshot', () => {
    expect(connectionStatus({ snapshot: null, error: null })).toMatchObject({ tone: 'pending', count: null })
  })

  it('is live with the server count', () => {
    expect(connectionStatus({ snapshot: snapshot(3), error: null })).toEqual({
      tone: 'live',
      count: 3,
      noun: 'live connections',
      note: null,
    })
  })

  it('keeps the last count but marks it stale after a failed refresh', () => {
    expect(connectionStatus({ snapshot: snapshot(2), error: 'timed out' })).toMatchObject({ tone: 'stale', count: 2 })
  })

  it('is offline when the server never answered', () => {
    expect(connectionStatus({ snapshot: null, error: 'HTTP 502' })).toMatchObject({ tone: 'offline', count: null })
  })

  it('agrees in number', () => {
    expect(connectionNoun(1)).toBe('live connection')
    expect(connectionNoun(0)).toBe('live connections')
  })
})

describe('ConnectionCount', () => {
  it('shows a dash, not zero, while connecting', () => {
    renderWith({ snapshot: null, updatedAt: null, error: null })
    const link = screen.getByRole('link')
    expect(link).toHaveTextContent('—')
    expect(link).toHaveTextContent('Connecting to server')
    expect(link).not.toHaveTextContent('0')
    expect(link).toHaveAccessibleName('Live connections unknown. Connecting to server')
  })

  it('links to Overview with the live count', () => {
    renderWith({ snapshot: snapshot(1), updatedAt: new Date(), error: null })
    const link = screen.getByRole('link', { name: /1 live connection/ })
    expect(link).toHaveAttribute('href', '/overview')
  })

  it('explains a stale count in the accessible name on the compact bar', () => {
    renderWith({ snapshot: snapshot(4), updatedAt: new Date(), error: 'timed out' }, 'bar')
    const link = screen.getByRole('link')
    expect(link).toHaveAccessibleName('4 live connections. Server unreachable. Showing last count.')
    // Visible text, not only the amber dot, says the count is not current.
    expect(link).toHaveTextContent('last known')
  })
})
