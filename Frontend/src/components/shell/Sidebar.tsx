import { Link } from 'react-router-dom'
import { SectionTexture } from './SectionTexture'
import { NavList } from './NavList'
import { ThemeToggle } from './ThemeToggle'

interface SidebarProps {
  connected: number
  theme: 'dark' | 'light'
  onToggleTheme: () => void
}

export function Sidebar({ connected, theme, onToggleTheme }: SidebarProps) {
  return (
    <aside className="fixed inset-y-0 left-0 z-30 hidden w-60 flex-col border-r border-line bg-surface lg:flex">
      <div className="relative overflow-hidden px-5 py-5">
        <SectionTexture />
        <Link to="/admin" className="relative font-display text-xl font-medium text-ink">
          Swarm
        </Link>
      </div>

      <nav aria-label="Primary" className="flex flex-col gap-0.5 px-3 py-3">
        <NavList />
      </nav>

      <div className="mt-auto border-t border-line px-4 py-4">
        <div className="flex items-center justify-between gap-3">
          <p className="flex items-baseline gap-1.5">
            <span className="figure text-sm text-ink">{connected}</span>
            <span className="text-xs text-ink-muted">connected</span>
          </p>
          <ThemeToggle theme={theme} onToggle={onToggleTheme} />
        </div>
        <p className="mt-1 text-2xs text-ink-muted">Demo data</p>

        <nav aria-label="Legal" className="mt-4 flex flex-wrap gap-x-3 gap-y-1 text-2xs">
          <Link to="/privacy" className="text-ink-muted hover:text-ink">
            Privacy
          </Link>
          <Link to="/imprint" className="text-ink-muted hover:text-ink">
            Imprint
          </Link>
          <Link to="/accessibility" className="text-ink-muted hover:text-ink">
            Accessibility
          </Link>
        </nav>
      </div>
    </aside>
  )
}
