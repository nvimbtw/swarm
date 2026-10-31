import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AppShell } from './components/shell/AppShell'
import { sessions } from './data/fixtures'
import { AdminPage } from './pages/AdminPage'
import { AccessibilityPage, ImprintPage, PrivacyPage } from './pages/LegalPages'
import { NotFoundPage, PreviewNotice } from './pages/PreviewNotice'

export function App() {
  const connected = sessions.filter((session) => session.connected).length

  return (
    <BrowserRouter basename={import.meta.env.BASE_URL}>
      <AppShell connected={connected}>
        <Routes>
          <Route path="/" element={<Navigate to="/admin" replace />} />
          <Route path="/admin" element={<AdminPage />} />
          <Route path="/overview" element={<PreviewNotice title="Overview" />} />
          <Route path="/work" element={<PreviewNotice title="Work" />} />
          <Route path="/sessions" element={<PreviewNotice title="Sessions" />} />
          <Route path="/privacy" element={<PrivacyPage />} />
          <Route path="/imprint" element={<ImprintPage />} />
          <Route path="/accessibility" element={<AccessibilityPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </AppShell>
    </BrowserRouter>
  )
}
