import { buttonClass } from '../ui/button'
import { MoonIcon, SunIcon } from '../ui/icons'

export function ThemeToggle({
  theme,
  onToggle,
  className,
}: {
  theme: 'dark' | 'light'
  onToggle: () => void
  className?: string
}) {
  const label = theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      onClick={onToggle}
      className={buttonClass('icon', className)}
    >
      {theme === 'dark' ? <SunIcon /> : <MoonIcon />}
    </button>
  )
}
