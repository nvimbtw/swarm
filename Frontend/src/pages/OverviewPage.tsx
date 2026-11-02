import type { ConnectionSnapshot } from '../lib/diagnostics'
import { cn } from '../lib/cn'
import { useDiagnostics } from '../lib/use-diagnostics'
import { usePageMeta } from '../lib/document-meta'
import { EmptyState } from '../components/ui/EmptyState'
import { PageHeader } from '../components/ui/PageHeader'

function milliseconds(value: number | null) {
  if (value === null) return '—'
  if (value > 0 && value < 0.01) return '< 0.01 ms'
  return `${value.toFixed(2)} ms`
}

function Timestamp({ value }: { value: string }) {
  return (
    <time dateTime={value} title={new Date(value).toLocaleString()}>
      {new Date(value).toLocaleTimeString()}
    </time>
  )
}

function Connections({
  title,
  connections,
  closed = false,
}: {
  title: string
  connections: ConnectionSnapshot[]
  closed?: boolean
}) {
  return (
    <section aria-label={title}>
      <div className="mb-3 flex items-baseline gap-3">
        <h2 className="font-display text-xl font-medium text-ink">{title}</h2>
        <span className="text-xs text-ink-muted">{connections.length}</span>
      </div>

      {connections.length === 0 ? (
        <EmptyState
          title={closed ? 'No recent disconnections' : 'No active connections'}
          body={
            closed
              ? 'Closed connections appear here with their final duration and message counts.'
              : 'Connect a client to Swarm to see its duration and message counts here.'
          }
        />
      ) : (
        <div
          className="max-h-80 overflow-auto rounded-sheet border border-line"
          role="region"
          aria-label={`${title} table`}
          tabIndex={0}
        >
          <table className="w-full whitespace-nowrap text-left text-sm">
            <caption className="sr-only">{title}; message counts are from the server.</caption>
            <thead className="sticky top-0 bg-surface text-xs text-ink-muted">
              <tr>
                <th scope="col" className="px-4 py-3 font-medium">Connection</th>
                <th scope="col" className="px-4 py-3 font-medium">Connected</th>
                {closed ? <th scope="col" className="px-4 py-3 font-medium">Disconnected</th> : null}
                <th scope="col" className="px-4 py-3 font-medium">Duration</th>
                <th scope="col" className="px-4 py-3 text-right font-medium">Sent</th>
                <th scope="col" className="px-4 py-3 text-right font-medium">Received</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {connections.map((connection) => (
                <tr key={connection.id}>
                  <th scope="row" className="px-4 py-3 text-left font-normal">
                    <span className="tag text-xs text-ink" title={connection.id}>
                      {connection.id.slice(0, 8)}
                    </span>
                    <span className={cn('ml-3 text-xs', closed ? 'text-ink-muted' : 'text-success')}>
                      {closed ? 'closed' : 'connected'}
                    </span>
                  </th>
                  <td className="px-4 py-3 text-ink-secondary"><Timestamp value={connection.connectedAt} /></td>
                  {closed ? (
                    <td className="px-4 py-3 text-ink-secondary">
                      {connection.disconnectedAt ? <Timestamp value={connection.disconnectedAt} /> : '—'}
                    </td>
                  ) : null}
                  <td className="tag px-4 py-3 text-xs text-ink">{connection.duration}</td>
                  <td className="px-4 py-3 text-right tabular-nums text-ink">{connection.messagesSent}</td>
                  <td className="px-4 py-3 text-right tabular-nums text-ink">{connection.messagesReceived}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

export function OverviewPage() {
  const { snapshot, updatedAt, error } = useDiagnostics()

  usePageMeta({
    title: 'Overview · Swarm',
    description: 'Live connection diagnostics, message counts, and send timings from the Swarm server.',
    path: '/overview',
  })

  const status = error ? 'Server unavailable' : snapshot ? 'Live diagnostics' : 'Connecting to server'

  return (
    <>
      <PageHeader
        title="Overview"
        stats={snapshot ? [{ value: snapshot.uptime, label: 'server uptime' }] : []}
        actions={
          <span
            role="status"
            className={cn('flex items-center gap-2 text-xs', error ? 'text-warning' : 'text-ink-secondary')}
          >
            <span
              aria-hidden="true"
              className={cn('size-2 rounded-full', error ? 'bg-warning' : snapshot ? 'bg-success' : 'bg-ink-muted')}
            />
            {status}
          </span>
        }
      />

      <div className="mb-6 flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 text-xs text-ink-muted">
        <p>Connection diagnostics · refreshes automatically every second</p>
        {updatedAt ? <p>Last updated <Timestamp value={updatedAt.toISOString()} /></p> : null}
      </div>

      {error ? (
        <div className="mb-6 rounded-sheet border border-line bg-surface p-4 text-sm" role="alert">
          <p className="text-warning">Unable to refresh diagnostics. Retrying automatically.</p>
          <p className="mt-1 text-xs text-ink-secondary">{error}</p>
          {snapshot ? <p className="mt-1 text-xs text-ink-muted">The values below are the last known data.</p> : null}
        </div>
      ) : null}

      {snapshot ? (
        <div className="grid gap-9">
          <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            {[
              { label: 'Active connections', value: snapshot.activeConnectionCount },
              { label: 'Total connections', value: snapshot.totalConnections },
              { label: 'Messages sent', value: snapshot.messagesSent },
              { label: 'Messages received', value: snapshot.messagesReceived },
            ].map((stat) => (
              <div key={stat.label} className="rounded-sheet border border-line bg-surface p-4">
                <dt className="eyebrow text-ink-muted">{stat.label}</dt>
                <dd className="figure mt-2 text-3xl text-ink">{stat.value.toLocaleString()}</dd>
              </div>
            ))}
          </dl>

          <Connections title="Active connections" connections={snapshot.activeConnections} />

          <section aria-label="Send duration">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <h2 className="font-display text-xl font-medium text-ink">Send duration</h2>
              <p className="text-xs text-ink-muted">
                {snapshot.sendDuration.sampleCount} of {snapshot.sendDuration.sampleLimit} recent samples
              </p>
            </div>
            <dl className="mt-3 grid gap-4 rounded-sheet border border-line bg-surface p-4 sm:grid-cols-3">
              <div>
                <dt className="eyebrow text-ink-muted">Average</dt>
                <dd className="figure mt-1 text-xl text-ink">{milliseconds(snapshot.sendDuration.averageMilliseconds)}</dd>
              </div>
              <div>
                <dt className="eyebrow text-ink-muted">Maximum</dt>
                <dd className="figure mt-1 text-xl text-ink">{milliseconds(snapshot.sendDuration.maximumMilliseconds)}</dd>
              </div>
              <div>
                <dt className="eyebrow text-ink-muted">Above {snapshot.sendDuration.slowThresholdMilliseconds} ms</dt>
                <dd className="figure mt-1 text-xl text-ink">{snapshot.sendDuration.countAboveThreshold}</dd>
              </div>
            </dl>
            <p className="mt-2 text-xs text-ink-muted">
              Server write time. Round-trip latency is not measured.
              {snapshot.sendDuration.sampleCount === 0 ? ' Timing values appear after the first successful send.' : ''}
            </p>
          </section>

          <Connections
            title="Recent disconnections"
            connections={[...snapshot.recentDisconnections].reverse()}
            closed
          />

          <p className="text-xs text-ink-muted">
            Counts are from the server’s perspective and reset when it restarts. Connection history keeps the latest 20 disconnections.
          </p>
        </div>
      ) : !error ? (
        <p className="py-8 text-sm text-ink-secondary">Waiting for the first diagnostics snapshot…</p>
      ) : null}
    </>
  )
}
