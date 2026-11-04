import { useEffect, useState, type ReactNode } from 'react'
import { MobileBar } from './MobileBar'
import { Sidebar } from './Sidebar'

export function AppShell({ children }: { children: ReactNode }) {
  const [theme, setTheme] = useState<'dark' | 'light'>('dark')

  useEffect(() => {
    document.documentElement.classList.toggle('dark', theme === 'dark')
  }, [theme])

  const toggleTheme = () => setTheme((current) => (current === 'dark' ? 'light' : 'dark'))

  return (
    <div className="min-h-svh bg-canvas">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded-control focus:bg-primary focus:px-3 focus:py-2 focus:text-primary-fg"
      >
        Skip to content
      </a>

      <Sidebar theme={theme} onToggleTheme={toggleTheme} />
      <MobileBar theme={theme} onToggleTheme={toggleTheme} />

      <div className="lg:pl-60">
        <main
          id="main"
          tabIndex={-1}
          className="mx-auto w-full max-w-[1180px] px-4 pb-20 outline-none sm:px-6 lg:px-8"
        >
          {children}
        </main>
      </div>
    </div>
  )
}
