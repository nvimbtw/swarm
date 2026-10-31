import type { WorkGroup, WorkItem } from '../../lib/admin-state'
import { cn } from '../../lib/cn'
import { buttonClass } from '../ui/button'
import { EmptyState } from '../ui/EmptyState'
import { ChevronRightIcon, ExternalIcon } from '../ui/icons'
import { StateWord } from '../ui/StateWord'

interface WorkItemRowProps {
  item: WorkItem
  selected: boolean
  onSelect: () => void
}

function WorkItemRow({ item, selected, onSelect }: WorkItemRowProps) {
  return (
    <button
      type="button"
      onClick={onSelect}
      aria-current={selected ? 'true' : undefined}
      className={cn(
        'relative flex w-full items-start gap-3 rounded-control px-3 py-2.5 text-left transition-colors duration-[var(--duration-fast)]',
        selected ? 'bg-selected' : 'hover:bg-hover',
      )}
    >
      {selected ? (
        <span
          aria-hidden="true"
          className="absolute left-0 top-1/2 h-5 w-[2px] -translate-y-1/2 rounded-marker bg-primary"
        />
      ) : null}
      <span className="tag w-9 shrink-0 pt-0.5 text-2xs text-ink-muted">#{item.id}</span>
      <span className="min-w-0 flex-1 text-sm leading-snug text-ink">{item.title}</span>
      <StateWord state={item.state} className="w-[86px] shrink-0 pt-0.5" />
      <span className="hidden w-[76px] shrink-0 pt-0.5 text-xs text-ink-muted md:block">
        {item.owner}
      </span>
    </button>
  )
}

interface WorkbenchProps {
  groups: WorkGroup[]
  itemCount: number
  allCollapsed: boolean
  selectedItemId: string | null
  onSelectItem: (id: string) => void
  onToggleGroup: (id: string) => void
  onToggleAll: () => void
}

export function Workbench({
  groups,
  itemCount,
  allCollapsed,
  selectedItemId,
  onSelectItem,
  onToggleGroup,
  onToggleAll,
}: WorkbenchProps) {
  return (
    <section aria-label="Workbench" className="min-w-0">
      <div className="flex items-baseline justify-between gap-4">
        <div className="flex items-baseline gap-3">
          <h2 className="font-display text-xl font-medium text-ink">Workbench</h2>
          <span className="text-xs text-ink-muted">{itemCount} items</span>
        </div>
        <button type="button" onClick={onToggleAll} className={buttonClass('quiet')}>
          {allCollapsed ? 'Expand all' : 'Collapse all'}
        </button>
      </div>

      <div className="mt-2">
        {groups.map((group) => {
          const panelId = `workbench-group-${group.id}`
          return (
            <section key={group.id} className="mt-4 first:mt-1">
              <h3>
                <button
                  type="button"
                  aria-expanded={!group.collapsed}
                  aria-controls={panelId}
                  onClick={() => onToggleGroup(group.id)}
                  className="flex w-full items-baseline gap-2 py-2 text-left"
                >
                  <ChevronRightIcon
                    className={cn(
                      'mt-0.5 shrink-0 text-ink-muted transition-transform duration-[var(--duration-fast)]',
                      !group.collapsed && 'rotate-90',
                    )}
                  />
                  <span className="font-display text-lg font-medium text-ink">{group.name}</span>
                  <span className="text-xs text-ink-muted">{group.count}</span>
                </button>
              </h3>

              <div id={panelId} hidden={group.collapsed}>
                {group.items.length > 0 ? (
                  <ul className="flex flex-col">
                    {group.items.map((item) => (
                      <li key={item.id}>
                        <WorkItemRow
                          item={item}
                          selected={selectedItemId === item.id}
                          onSelect={() => onSelectItem(item.id)}
                        />
                      </li>
                    ))}
                  </ul>
                ) : (
                  <EmptyState
                    title="No documentation items yet"
                    body="Create one from a #docs thread."
                    action={
                      <a
                        href="https://discord.com"
                        target="_blank"
                        rel="noreferrer"
                        className={buttonClass('quiet')}
                      >
                        Open #docs
                        <ExternalIcon />
                      </a>
                    }
                  />
                )}
              </div>
            </section>
          )
        })}
      </div>
    </section>
  )
}
