import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { DiagnosticsProvider } from './components/DiagnosticsProvider'
import { AppShell } from './components/shell/AppShell'
import { AdminPage } from './pages/AdminPage'
import { DisplayPage } from './pages/DisplayPage'
import { OverviewPage } from './pages/OverviewPage'
import { AccessibilityPage, ImprintPage, PrivacyPage } from './pages/LegalPages'
import { NotFoundPage, PreviewNotice } from './pages/PreviewNotice'

export function App() {
  return (
    <BrowserRouter basename={import.meta.env.BASE_URL}>
      <DiagnosticsProvider>
        <Routes>
          <Route path="/display" element={<DisplayPage />} />
          <Route
            element={
              <AppShell>
                <Outlet />
              </AppShell>
            }
          >
            <Route path="/" element={<Navigate to="/overview" replace />} />
            <Route path="/admin" element={<AdminPage />} />
            <Route path="/overview" element={<OverviewPage />} />
            <Route path="/work" element={<PreviewNotice title="Work" />} />
            <Route path="/sessions" element={<PreviewNotice title="Sessions" />} />
            <Route path="/privacy" element={<PrivacyPage />} />
            <Route path="/imprint" element={<ImprintPage />} />
            <Route path="/accessibility" element={<AccessibilityPage />} />
            <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Routes>
      </DiagnosticsProvider>
    </BrowserRouter>
  )
}
