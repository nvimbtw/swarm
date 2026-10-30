import type { ReactNode } from 'react'
import { SectionTexture } from '../shell/SectionTexture'

interface PageHeaderProps {
  title: string
  stats?: Array<{ value: string | number; label: string }>
  actions?: ReactNode
}

export function PageHeader({ title, stats = [], actions }: PageHeaderProps) {
  return (
    <header className="relative overflow-hidden">
      <SectionTexture />
      <div className="relative flex flex-col gap-5 py-7 sm:py-9 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="font-display text-3xl font-medium text-ink">{title}</h1>
          {stats.length > 0 ? (
            <p className="mt-3 flex flex-wrap items-baseline gap-x-7 gap-y-2">
              {stats.map((stat) => (
                <span key={stat.label} className="flex items-baseline gap-1.5">
                  <span className="figure text-md text-ink">{stat.value}</span>
                  <span className="text-xs text-ink-muted">{stat.label}</span>
                </span>
              ))}
            </p>
          ) : null}
        </div>
        {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
      </div>
    </header>
  )
}
