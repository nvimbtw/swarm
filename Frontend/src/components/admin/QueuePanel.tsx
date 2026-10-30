import type { AdminTab, RequestState } from '../../lib/admin-state'
import type { HistoryEvent, Request, Session } from '../../data/fixtures'
import { cn } from '../../lib/cn'
import { EmptyState } from '../ui/EmptyState'
import { PauseIcon } from '../ui/icons'
import { Tabs } from '../ui/Tabs'
import { NowBand } from './NowBand'
import { RequestRow } from './RequestRow'

interface QueuePanelProps {
  tab: AdminTab
  onTabChange: (id: string) => void
  paused: boolean
  nowRequest: Request
  requests: Request[]
  history: HistoryEvent[]
  sessionFor: (request: Request) => Session | undefined
  selectedItemId: string | null
  onSelectItem: (id: string) => void
}

function countByState(requests: Request[], state: RequestState) {
  return requests.filter((request) => request.state === state).length
}

export function QueuePanel({
  tab,
  onTabChange,
  paused,
  nowRequest,
  requests,
  history,
  sessionFor,
  selectedItemId,
  onSelectItem,
}: QueuePanelProps) {
  const queueCount = requests.length + 1
  const waiting = countByState(requests, 'waiting')

  return (
    <section aria-label="Queue and history" className="min-w-0">
      <div className="flex items-baseline justify-between gap-4">
        <Tabs
          label="Queue views"
          idPrefix="queue"
          value={tab}
          onChange={onTabChange}
          tabs={[
            { id: 'queue', label: 'Queue', count: queueCount },
            { id: 'history', label: 'History', count: history.length },
          ]}
        />
        <span className="text-2xs text-ink-muted">
          {paused ? 'Paused' : waiting > 0 ? `${waiting} need a reply` : 'No replies needed'}
        </span>
      </div>

      <div
        id="queue-panel-queue"
        role="tabpanel"
        aria-labelledby="queue-tab-queue"
        hidden={tab !== 'queue'}
      >
        {paused ? (
          <p className="mt-3 flex items-start gap-2 text-xs">
            <PauseIcon className="mt-0.5 shrink-0 text-warning" />
            <span>
              <span className="font-medium text-warning">Queue paused.</span>{' '}
              <span className="text-ink-muted">New requests wait until you resume.</span>
            </span>
          </p>
        ) : null}

        <NowBand
          request={nowRequest}
          session={sessionFor(nowRequest)}
          selected={selectedItemId === nowRequest.itemId}
          onSelect={() => onSelectItem(nowRequest.itemId)}
        />

        {requests.length > 0 ? (
          <ul className="mt-2 flex flex-col">
            {requests.map((request) => (
              <li key={request.id}>
                <RequestRow
                  request={request}
                  session={sessionFor(request)}
                  selected={selectedItemId === request.itemId}
                  onSelect={() => onSelectItem(request.itemId)}
                />
              </li>
            ))}
          </ul>
        ) : (
          <div className="mt-4">
            <EmptyState
              title="Nothing waiting"
              body="Requests appear here when someone sends one to a session."
            />
          </div>
        )}
      </div>

      <div
        id="queue-panel-history"
        role="tabpanel"
        aria-labelledby="queue-tab-history"
        hidden={tab !== 'history'}
      >
        <ol className="mt-4 flex flex-col">
          {history.map((event) => (
            <li key={event.id} className="flex items-baseline gap-3 py-2.5">
              <span className="tag w-8 shrink-0 text-2xs text-ink-muted">{event.age}</span>
              <span className="min-w-0">
                <span
                  className={cn(
                    'block text-sm',
                    event.kind === 'permission' ? 'text-danger' : 'text-ink',
                  )}
                >
                  {event.text}
                </span>
                <span className="block text-xs text-ink-muted">{event.detail}</span>
              </span>
            </li>
          ))}
        </ol>
      </div>
    </section>
  )
}
