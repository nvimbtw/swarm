import type { Request, Session } from '../../data/fixtures'
import { cn } from '../../lib/cn'
import { Companion } from '../ui/Companion'
import { RequestStateWord } from '../ui/StateWord'

interface RequestRowProps {
  request: Request
  session: Session | undefined
  selected: boolean
  onSelect: () => void
}

export function RequestRow({ request, session, selected, onSelect }: RequestRowProps) {
  return (
    <button
      type="button"
      onClick={onSelect}
      aria-current={selected ? 'true' : undefined}
      className={cn(
        'relative flex w-full items-start gap-3 rounded-control px-3 py-3 text-left transition-colors duration-[var(--duration-fast)]',
        selected ? 'bg-selected' : 'hover:bg-hover',
      )}
    >
      {selected ? (
        <span
          aria-hidden="true"
          className="absolute left-0 top-1/2 h-5 w-[2px] -translate-y-1/2 rounded-marker bg-primary"
        />
      ) : null}
      {session ? (
        <Companion pose={session.pose} size={28} className="mt-0.5" />
      ) : null}
      <span className="min-w-0 flex-1">
        <span className="block text-sm leading-snug text-ink">{request.title}</span>
        <span className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-ink-muted">
          {session ? (
            <>
              <span>
                {session.owner} → {session.harness}
              </span>
              <span className="tag text-2xs">{session.machine}</span>
            </>
          ) : null}
          <span>{request.age}</span>
        </span>
      </span>
      <span className="flex shrink-0 flex-col items-end gap-1">
        <RequestStateWord state={request.state} />
        <span className="tag text-2xs text-ink-muted">{request.delivery}</span>
      </span>
    </button>
  )
}
