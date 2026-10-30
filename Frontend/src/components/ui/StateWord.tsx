import type { RequestState, WorkState } from '../../lib/admin-state'
import { cn } from '../../lib/cn'

const WORK_TONE: Record<WorkState, string> = {
  open: 'text-ink-muted',
  'in-progress': 'text-progress',
  verification: 'text-verification',
  closed: 'text-closed',
}

const REQUEST_TONE: Record<RequestState, string> = {
  queued: 'text-queued',
  waiting: 'text-verification',
  'in-progress': 'text-progress',
}

const REQUEST_LABEL: Record<RequestState, string> = {
  queued: 'queued',
  waiting: 'waiting',
  'in-progress': 'in progress',
}

export function StateWord({ state, className }: { state: WorkState; className?: string }) {
  return (
    <span className={cn('whitespace-nowrap text-xs', WORK_TONE[state], className)}>
      {state.replace('-', ' ')}
    </span>
  )
}

export function RequestStateWord({
  state,
  className,
}: {
  state: RequestState
  className?: string
}) {
  return (
    <span className={cn('whitespace-nowrap text-xs', REQUEST_TONE[state], className)}>
      {REQUEST_LABEL[state]}
    </span>
  )
}
