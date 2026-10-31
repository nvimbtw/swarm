import { useRef } from 'react'
import { cn } from '../../lib/cn'

export interface TabItem {
  id: string
  label: string
  count?: number
}

interface TabsProps {
  label: string
  tabs: TabItem[]
  value: string
  onChange: (id: string) => void
  idPrefix: string
}

export function Tabs({ label, tabs, value, onChange, idPrefix }: TabsProps) {
  const refs = useRef<Array<HTMLButtonElement | null>>([])

  const move = (index: number) => {
    const next = tabs[index]
    if (!next) return
    onChange(next.id)
    refs.current[index]?.focus()
  }

  return (
    <div role="tablist" aria-label={label} className="flex items-center gap-6">
      {tabs.map((tab, index) => {
        const selected = tab.id === value
        return (
          <button
            key={tab.id}
            ref={(element) => {
              refs.current[index] = element
            }}
            type="button"
            role="tab"
            id={`${idPrefix}-tab-${tab.id}`}
            aria-selected={selected}
            aria-controls={`${idPrefix}-panel-${tab.id}`}
            tabIndex={selected ? 0 : -1}
            onClick={() => onChange(tab.id)}
            onKeyDown={(event) => {
              if (event.key === 'ArrowRight') {
                event.preventDefault()
                move((index + 1) % tabs.length)
              }
              if (event.key === 'ArrowLeft') {
                event.preventDefault()
                move((index - 1 + tabs.length) % tabs.length)
              }
              if (event.key === 'Home') {
                event.preventDefault()
                move(0)
              }
              if (event.key === 'End') {
                event.preventDefault()
                move(tabs.length - 1)
              }
            }}
            className={cn(
              'flex items-baseline gap-1.5 py-2 text-sm transition-colors duration-[var(--duration-fast)]',
              selected ? 'font-medium text-primary' : 'text-ink-muted hover:text-ink',
            )}
          >
            {tab.label}
            {typeof tab.count === 'number' ? (
              <span className={cn('text-2xs', selected ? 'text-primary' : 'text-ink-muted')}>
                {tab.count}
              </span>
            ) : null}
          </button>
        )
      })}
    </div>
  )
}
