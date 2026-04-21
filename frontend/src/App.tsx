import { Routes, Route } from 'react-router'
import { useAuth } from 'react-oidc-context'
import AppLayout from './layouts/AppLayout'
import ProtectedRoute from './layouts/ProtectedRoute'
import HomePage from './pages/HomePage'
import DatenschutzPage from './pages/DatenschutzPage'
import ImpressumPage from './pages/ImpressumPage'
import OrganizationSettingsPage from './pages/OrganizationSettingsPage'
import VolunteerOpportunityDetailPage from './pages/VolunteerOpportunityDetailPage'
import MyEngagementsPage from './pages/MyEngagementsPage'
import EngagementManagementPage from './pages/EngagementManagementPage'

function CallbackPage() {
  const auth = useAuth()
  if (auth.error) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <span className="text-red-600">Authentifizierungsfehler: {auth.error.message}</span>
      </div>
    )
  }
  return (
    <div className="flex min-h-screen items-center justify-center">
      <span className="text-gray-500">Anmeldung wird abgeschlossen…</span>
    </div>
  )
}

export default function App() {
  return (
    <Routes>
      <Route path="/callback" element={<CallbackPage />} />
      <Route element={<AppLayout />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/datenschutz" element={<DatenschutzPage />} />
        <Route path="/impressum" element={<ImpressumPage />} />
        <Route path="/volunteer-opportunities/:opportunityId" element={<VolunteerOpportunityDetailPage />} />
        <Route
          path="/my-engagements"
          element={
            <ProtectedRoute>
              <MyEngagementsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/volunteer-opportunities/:opportunityId/engagements"
          element={
            <ProtectedRoute>
              <EngagementManagementPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/organizations/:organizationId/settings"
          element={
            <ProtectedRoute>
              <OrganizationSettingsPage />
            </ProtectedRoute>
          }
        />
      </Route>
    </Routes>
  )
}
