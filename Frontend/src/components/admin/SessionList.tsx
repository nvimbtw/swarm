import type { CompanionPose, Session } from '../../data/fixtures'
import { cn } from '../../lib/cn'
import { Companion } from '../ui/Companion'

const POSE_LABEL: Record<CompanionPose, string> = {
  generating: 'generating',
  'request-sent': 'request sent',
  'request-received': 'request received',
  away: 'away',
  completed: 'completed',
  offline: 'offline',
}

const POSE_TONE: Record<CompanionPose, string> = {
  generating: 'text-progress',
  'request-sent': 'text-queued',
  'request-received': 'text-queued',
  away: 'text-ink-muted',
  completed: 'text-closed',
  offline: 'text-ink-muted',
}

export function SessionList({ sessions }: { sessions: Session[] }) {
  const connected = sessions.filter((session) => session.connected).length

  return (
    <section aria-label="Sessions">
      <div className="flex items-baseline justify-between gap-3">
        <h2 className="font-display text-lg font-medium text-ink">Sessions</h2>
        <span className="text-xs text-ink-muted">{connected} connected</span>
      </div>
      <ul className="mt-1 flex flex-col">
        {sessions.map((session) => (
          <li key={session.id} className="flex items-center gap-3 py-1.5">
            <Companion pose={session.pose} size={32} />
            <span className="min-w-0 flex-1">
              <span className="block text-xs text-ink">
                {session.owner} / {session.harness}
              </span>
              <span className="tag block text-2xs text-ink-muted">{session.machine}</span>
            </span>
            <span className={cn('shrink-0 text-2xs', POSE_TONE[session.pose])}>
              {POSE_LABEL[session.pose]}
            </span>
          </li>
        ))}
      </ul>
    </section>
  )
}
