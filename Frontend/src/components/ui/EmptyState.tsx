import type { ReactNode } from 'react'
import { SectionTexture } from '../shell/SectionTexture'
import { Companion } from './Companion'

interface EmptyStateProps {
  title: string
  body: string
  action?: ReactNode
  pose?: 'offline' | 'away'
  size?: number
}

export function EmptyState({ title, body, action, pose = 'offline', size = 64 }: EmptyStateProps) {
  return (
    <div
      className="relative overflow-hidden rounded-sheet px-4 py-4"
      style={{ backgroundColor: 'color-mix(in oklab, var(--texture-warm) 7%, transparent)' }}
    >
      <SectionTexture tone="warm" />
      <div className="relative flex flex-col gap-3 sm:flex-row sm:items-center">
        <Companion pose={pose} size={size} />
        <div className="min-w-0 flex-1">
          <p className="font-display text-md leading-tight text-ink">{title}</p>
          <p className="mt-0.5 text-xs text-ink-secondary">{body}</p>
        </div>
        {action ? <div className="shrink-0">{action}</div> : null}
      </div>
    </div>
  )
}
