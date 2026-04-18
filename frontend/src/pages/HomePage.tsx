import { useAuth } from 'react-oidc-context'
import VolunteerOpportunitiesList from '../components/VolunteerOpportunitiesList'

export default function HomePage() {
  const auth = useAuth()
  const roles = (Array.isArray(auth.user?.profile?.roles) ? auth.user!.profile.roles : []) as string[]
  const isOrganisator = roles.includes('organisator')

  return (
    <>
      <h1 className="mb-4 text-4xl font-bold text-gray-900">Einsatzbereit</h1>
      <p className="mb-8 text-lg text-gray-600">
        Engagiere dich spontan. Finde dein Ehrenamt in der Nähe.
      </p>
      <VolunteerOpportunitiesList canCreateOpportunity={isOrganisator} />
    </>
  )
}
