import type { ReactNode } from 'react'
import { DiagnosticsContext, useDiagnosticsPolling } from '../lib/use-diagnostics'

/** Polls /diagnostics once for the whole app: the shell count and Overview share it. */
export function DiagnosticsProvider({ children }: { children: ReactNode }) {
  const state = useDiagnosticsPolling()
  return <DiagnosticsContext.Provider value={state}>{children}</DiagnosticsContext.Provider>
}
