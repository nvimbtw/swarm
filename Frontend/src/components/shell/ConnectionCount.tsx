import { Link } from 'react-router-dom'
import { cn } from '../../lib/cn'
import { connectionStatus, type ConnectionTone } from '../../lib/connection-status'
import { useDiagnostics } from '../../lib/use-diagnostics'

// The compact bar has no room for the note, so a non-live state gets a short
// visible word instead of relying on the dot colour alone (WCAG 1.4.1).
const BAR_WORD: Record<Exclude<ConnectionTone, 'live'>, string> = {
  pending: 'connecting',
  stale: 'last known',
  offline: 'unreachable',
}

const DOT: Record<ConnectionTone, string> = {
  pending: 'bg-ink-faint',
  live: 'bg-success',
  stale: 'bg-warning',
  offline: 'bg-warning',
}

/**
 * The server's open WebSocket count, on every page. Links to Overview, which
 * lists each connection. The shell does not announce every refresh to screen
 * readers; Overview carries the live status region.
 */
export function ConnectionCount({ variant = 'sidebar' }: { variant?: 'sidebar' | 'bar' }) {
  const status = connectionStatus(useDiagnostics())
  const isBar = variant === 'bar'
  const value = status.count === null ? '—' : status.count.toLocaleString()
  // Adjacent spans would otherwise be read as "3live connections".
  const name =
    (status.count === null ? 'Live connections unknown' : `${value} ${status.noun}`) +
    (status.note ? `. ${status.note}` : '')

  return (
    <Link
      to="/overview"
      aria-label={name}
      title="Open WebSocket connections to the Swarm server, including local connectors"
      className={cn(
        'group flex min-w-0 rounded-control transition-colors duration-[var(--duration-fast)] hover:bg-hover',
        isBar ? 'min-h-11 items-center gap-2 px-2' : '-mx-2 flex-col px-2 py-1.5',
      )}
    >
      <span className="flex items-center gap-2">
        <span aria-hidden="true" className={cn('size-2 shrink-0 rounded-full', DOT[status.tone])} />
        <span
          className={cn(
            'figure leading-none',
            isBar ? 'text-md' : 'text-lg',
            status.tone === 'live' ? 'text-ink' : 'text-ink-secondary',
          )}
        >
          {value}
        </span>
        {isBar && status.tone !== 'live' ? (
          <span aria-hidden="true" className={cn('text-xs', status.tone === 'pending' ? 'text-ink-muted' : 'text-warning')}>
            {BAR_WORD[status.tone]}
          </span>
        ) : (
          <span className={cn('text-xs text-ink-muted group-hover:text-ink-secondary', isBar && 'max-[359px]:sr-only')}>
            {status.noun}
          </span>
        )}
      </span>
      {status.note ? (
        <span className={cn('text-2xs', status.tone === 'pending' ? 'text-ink-muted' : 'text-warning', isBar ? 'sr-only' : 'mt-1')}>
          {status.note}
        </span>
      ) : null}
    </Link>
  )
}
