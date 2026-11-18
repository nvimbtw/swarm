import { useEffect, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { nowRequest, requests, sessionById, sessions, workItems } from '../data/fixtures'
import { waitingCount, type WorkItem, type WorkState } from '../lib/admin-state'
import { cn } from '../lib/cn'
import { connectionStatus } from '../lib/connection-status'
import { usePageMeta } from '../lib/document-meta'
import { useDiagnostics } from '../lib/use-diagnostics'
import { POSE_LABEL, POSE_TONE } from '../components/ui/pose'
import { buttonClass } from '../components/ui/button'
import { Companion } from '../components/ui/Companion'
import { RequestStateWord, StateWord } from '../components/ui/StateWord'
import { Throbber } from '../components/ui/Throbber'

const COLUMNS: WorkState[] = ['open', 'in-progress', 'verification', 'closed']
const allRequests = [nowRequest, ...requests]
const owners = [...new Set(sessions.map((session) => session.owner))]
const EVERYONE = 'Everyone'
const EXIT_VISIBLE_MS = 5000

// Touch targets on the Pi screen need at least 44px.
const touch = 'h-11 px-4'
const tile =
  'flex min-h-0 flex-col items-center justify-center gap-1 rounded-sheet border border-line bg-surface p-3 text-center transition-colors duration-[var(--duration-fast)] hover:bg-hover active:bg-selected'

function personData(owner: string) {
  const own = sessions.filter((session) => session.owner === owner)
  return {
    // The busiest pose stands for the person on the menu.
    lead: own.find((session) => session.pose === 'generating') ?? own.find((s) => s.connected) ?? own[0],
    current: allRequests.find((request) => sessionById(request.sessionId)?.owner === owner),
    items: workItems.filter((item) => item.owner === owner),
  }
}

function Line({ label, children }: { label: string; children: ReactNode }) {
  return (
    <span className="flex w-full items-baseline justify-center gap-1.5 text-xs">
      <span className="shrink-0 text-ink-muted">{label}:</span>
      <span className="min-w-0 truncate">{children}</span>
    </span>
  )
}

function PersonTile({ owner, onOpen }: { owner: string; onOpen: () => void }) {
  const { lead, current } = personData(owner)
  return (
    <button type="button" onClick={onOpen} className={tile}>
      {lead ? <Companion pose={lead.pose} size={88} /> : null}
      <span className="mb-1 font-display text-xl font-medium text-ink">{owner}</span>
      {lead ? (
        <Line label="State">
          <span className={POSE_TONE[lead.pose]}>{POSE_LABEL[lead.pose]}</span>
        </Line>
      ) : null}
      <Line label="Now">
        {current ? <span className="text-ink-secondary">{current.title}</span> : <span className="text-ink-muted">idle</span>}
      </Line>
    </button>
  )
}

function Stat({ value, label }: { value: ReactNode; label: string }) {
  return (
    <div className="flex items-baseline gap-1.5">
      <span className="figure text-md text-ink">{value}</span>
      <span className="text-2xs text-ink-muted">{label}</span>
    </div>
  )
}

function Menu({ onOpen }: { onOpen: (owner: string) => void }) {
  const { snapshot } = useDiagnostics()
  const active = workItems.filter((item) => item.state !== 'closed').length

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-3">
      <div className="grid min-h-0 flex-1 grid-cols-4 gap-3">
        {owners.map((owner) => (
          <PersonTile key={owner} owner={owner} onOpen={() => onOpen(owner)} />
        ))}
        <button type="button" onClick={() => onOpen(EVERYONE)} className={tile}>
          <span className="figure flex size-[88px] items-center justify-center text-3xl text-ink">
            {workItems.length}
          </span>
          <span className="mb-1 font-display text-xl font-medium text-ink">{EVERYONE}</span>
          <Line label="Board">
            <span className="text-ink-secondary">all items</span>
          </Line>
        </button>
      </div>

      <div className="flex shrink-0 flex-wrap justify-center gap-x-8 gap-y-1">
        <Stat value={snapshot?.activeConnectionCount ?? '—'} label="live connections" />
        <Stat value={snapshot?.uptime ?? '—'} label="server uptime" />
        <Stat value={active} label="active items" />
        <Stat value={waitingCount(allRequests)} label="need a reply" />
      </div>
    </div>
  )
}

function BoardCard({ item, showOwner }: { item: WorkItem; showOwner: boolean }) {
  return (
    <li className="shrink-0 rounded-control border border-line bg-surface px-2.5 py-2">
      <div className="flex items-baseline justify-between gap-2">
        <span className="tag text-2xs text-ink-muted">#{item.id}</span>
        {showOwner ? <span className="text-2xs text-ink-secondary">{item.owner}</span> : null}
      </div>
      <p className="mt-0.5 line-clamp-2 text-xs leading-snug text-ink">{item.title}</p>
    </li>
  )
}

function Board({ owner, onBack }: { owner: string; onBack: () => void }) {
  const team = owner === EVERYONE
  const person = team ? undefined : personData(owner)
  const items = person ? person.items : workItems
  const current = person?.current

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-3">
      <header className="flex shrink-0 items-center gap-3">
        {person?.lead ? <Companion pose={person.lead.pose} size={44} /> : null}
        <div className="min-w-0 flex-1">
          <h1 className="font-display text-xl font-medium leading-tight text-ink">{owner}</h1>
          {current ? (
            <p className="flex items-center gap-2 text-xs">
              {current.state === 'in-progress' ? <Throbber /> : null}
              <span className="text-ink-muted">Now:</span>
              <span className="truncate text-ink-secondary">{current.title}</span>
              <RequestStateWord state={current.state} className="shrink-0" />
            </p>
          ) : null}
        </div>
        <button type="button" onClick={onBack} className={buttonClass('primary', touch)}>
          Back
        </button>
      </header>

      <div className="grid min-h-0 flex-1 grid-cols-4 gap-3">
        {COLUMNS.map((state) => {
          const column = items.filter((item) => item.state === state)
          return (
            <section key={state} aria-label={state.replace('-', ' ')} className="flex min-h-0 flex-col">
              <h2 className="flex shrink-0 items-baseline gap-2 border-b border-line pb-1.5">
                <StateWord state={state} className="text-sm font-medium" />
                <span className="text-xs text-ink-muted">{column.length}</span>
              </h2>
              <ul className="mt-2 flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto overscroll-contain">
                {column.map((item) => (
                  <BoardCard key={item.id} item={item} showOwner={team} />
                ))}
              </ul>
            </section>
          )
        })}
      </div>
    </div>
  )
}

/** The status line along the bottom. Tapping it reveals Exit for a few seconds. */
function StatusBar() {
  const status = connectionStatus(useDiagnostics())
  const [showExit, setShowExit] = useState(false)

  useEffect(() => {
    if (!showExit) return
    const timer = window.setTimeout(() => setShowExit(false), EXIT_VISIBLE_MS)
    return () => window.clearTimeout(timer)
  }, [showExit])

  return (
    <footer className="flex h-11 shrink-0 items-center justify-between gap-3">
      <button
        type="button"
        onClick={() => setShowExit((shown) => !shown)}
        aria-expanded={showExit}
        aria-label="Show display options"
        className="flex h-full min-w-0 flex-1 items-center gap-2 text-left text-xs"
      >
        <span
          aria-hidden="true"
          className={cn('size-2 shrink-0 rounded-full', status.tone === 'live' ? 'bg-success' : 'bg-warning')}
        />
        <span className="truncate text-ink-secondary">
          {status.tone === 'live' ? 'Connected to Swarm' : status.note}
          {status.count !== null ? ` · ${status.count} ${status.noun}` : ''}
          {' · people and work are demo data'}
        </span>
      </button>
      {showExit ? (
        <Link to="/admin" className={buttonClass('quiet', touch)}>
          Exit display
        </Link>
      ) : null}
    </footer>
  )
}

/**
 * Full-screen kiosk view sized for the Raspberry Pi 7" touchscreen (800×480).
 * The page never scrolls; only board columns scroll inside themselves.
 */
export function DisplayPage() {
  const [owner, setOwner] = useState<string | null>(null)

  usePageMeta({
    title: 'Display · Swarm',
    description: 'What everyone on the Swarm is working on right now.',
    path: '/display',
  })

  return (
    <div className="flex h-svh flex-col gap-2 overflow-hidden bg-canvas px-3 pt-3">
      <main className="flex min-h-0 flex-1 flex-col">
        {owner ? <Board owner={owner} onBack={() => setOwner(null)} /> : <Menu onOpen={setOwner} />}
      </main>
      <StatusBar />
    </div>
  )
}
