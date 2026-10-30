import { NavLink } from 'react-router-dom'
import { cn } from '../../lib/cn'
import { NAV_ITEMS } from './nav-items'

interface NavListProps {
  onNavigate?: () => void
  variant?: 'sidebar' | 'bar'
}

export function NavList({ onNavigate, variant = 'sidebar' }: NavListProps) {
  const isBar = variant === 'bar'

  return (
    <>
      {NAV_ITEMS.map((item) => {
        const Icon = item.icon
        return (
          <NavLink
            key={item.to}
            to={item.to}
            onClick={onNavigate}
            className={({ isActive }) =>
              cn(
                'relative flex items-center rounded-control text-sm transition-colors duration-[var(--duration-fast)]',
                isBar ? 'gap-1.5 px-1.5 py-2 sm:gap-2.5 sm:px-3' : 'gap-3 py-2 pl-4 pr-3',
                isActive
                  ? 'font-medium text-primary'
                  : 'text-ink-muted hover:bg-hover hover:text-ink',
              )
            }
          >
            {({ isActive }) => (
              <>
                {isActive ? (
                  <span
                    aria-hidden="true"
                    className={cn(
                      'absolute rounded-marker bg-primary',
                      isBar
                        ? 'inset-x-0 bottom-0 h-[2px]'
                        : 'left-0 top-1/2 h-4 w-[2px] -translate-y-1/2',
                    )}
                  />
                ) : null}
                <Icon
                  className={cn(
                    isBar && 'hidden sm:block',
                    isActive ? 'text-primary' : 'text-ink-faint',
                  )}
                />
                <span>{item.label}</span>
                {isActive ? <span className="sr-only">(current page)</span> : null}
              </>
            )}
          </NavLink>
        )
      })}
    </>
  )
}
