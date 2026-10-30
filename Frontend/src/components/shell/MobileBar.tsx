import { Link } from 'react-router-dom'
import { NavList } from './NavList'
import { ThemeToggle } from './ThemeToggle'

interface MobileBarProps {
  theme: 'dark' | 'light'
  onToggleTheme: () => void
}

export function MobileBar({ theme, onToggleTheme }: MobileBarProps) {
  return (
    <header className="sticky top-0 z-30 border-b border-line bg-surface/95 backdrop-blur lg:hidden">
      <div className="flex items-center justify-between px-4 py-3">
        <Link to="/admin" className="font-display text-lg font-medium text-ink">
          Swarm
        </Link>
        <ThemeToggle theme={theme} onToggle={onToggleTheme} />
      </div>
      <nav
        aria-label="Primary"
        className="flex gap-1 overflow-x-auto px-4 pb-1 sm:gap-4 [scrollbar-width:none]"
      >
        <NavList variant="bar" />
      </nav>
    </header>
  )
}
