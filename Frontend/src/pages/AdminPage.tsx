import { useReducer } from 'react'
import {
  initiatives,
  nowRequest,
  requests,
  sessionById,
  sessions,
  workItems,
  history,
} from '../data/fixtures'
import {
  adminReducer,
  groupWorkItems,
  initialAdminState,
  waitingCount,
  workCounts,
} from '../lib/admin-state'
import { buttonClass } from '../components/ui/button'
import { ExternalIcon, PauseIcon, PlayIcon } from '../components/ui/icons'
import { PageHeader } from '../components/ui/PageHeader'
import { QueuePanel } from '../components/admin/QueuePanel'
import { SessionList } from '../components/admin/SessionList'
import { Workbench } from '../components/admin/Workbench'
import { usePageMeta } from '../lib/document-meta'

export function AdminPage() {
  // The preview opens with the waiting request selected so the queue-to-workbench
  // relationship (and the steel marker) is visible in the default picture.
  const [state, dispatch] = useReducer(adminReducer, {
    ...initialAdminState,
    selectedItemId: '409',
  })

  usePageMeta({
    title: 'Admin · Swarm',
    description: 'Queue, history, and the workbench for connected Swarm sessions.',
    path: '/admin',
  })

  const connected = sessions.filter((session) => session.connected).length
  const counts = workCounts(workItems)
  const waiting = waitingCount(requests)
  const groups = groupWorkItems(initiatives, workItems, state.collapsedGroupIds)
  const allCollapsed = state.collapsedGroupIds.length === initiatives.length

  return (
    <>
      <PageHeader
        title="Admin"
        stats={[
          { value: connected, label: 'connected' },
          { value: waiting, label: 'waiting' },
          { value: counts.active, label: 'active' },
        ]}
        actions={
          <>
            <a
              href="https://discord.com"
              target="_blank"
              rel="noreferrer"
              className={buttonClass('quiet')}
            >
              Open Discord
              <ExternalIcon />
            </a>
            <button
              type="button"
              onClick={() => dispatch({ type: 'toggle-queue' })}
              className={buttonClass('primary')}
              aria-pressed={state.queuePaused}
            >
              {state.queuePaused ? <PlayIcon /> : <PauseIcon />}
              {state.queuePaused ? 'Resume queue' : 'Pause queue'}
            </button>
          </>
        }
      />

      <div className="grid gap-10 xl:grid-cols-[360px_minmax(0,1fr)] xl:gap-12">
        <div className="grid min-w-0 content-start gap-10 md:grid-cols-[minmax(0,1fr)_280px] xl:grid-cols-1 xl:gap-10">
          <QueuePanel
            tab={state.tab}
            onTabChange={(id) =>
              dispatch({ type: 'set-tab', tab: id === 'history' ? 'history' : 'queue' })
            }
            paused={state.queuePaused}
            nowRequest={nowRequest}
            requests={requests}
            history={history}
            sessionFor={(request) => sessionById(request.sessionId)}
            selectedItemId={state.selectedItemId}
            onSelectItem={(id) => dispatch({ type: 'select-item', id })}
          />
          <SessionList sessions={sessions} />
        </div>

        <Workbench
          groups={groups}
          itemCount={workItems.length}
          allCollapsed={allCollapsed}
          selectedItemId={state.selectedItemId}
          onSelectItem={(id) => dispatch({ type: 'select-item', id })}
          onToggleGroup={(id) => dispatch({ type: 'toggle-group', id })}
          onToggleAll={() =>
            allCollapsed
              ? dispatch({ type: 'expand-all-groups' })
              : dispatch({
                  type: 'set-groups-collapsed',
                  ids: initiatives.map((initiative) => initiative.id),
                })
          }
        />
      </div>
    </>
  )
}
