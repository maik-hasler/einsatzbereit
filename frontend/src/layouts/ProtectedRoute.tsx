import { useAuth } from 'react-oidc-context'
import type { ReactNode } from 'react'

interface Props {
  children: ReactNode
}

export default function ProtectedRoute({ children }: Props) {
  const auth = useAuth()

  if (auth.isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <span className="text-gray-500">Wird geladen…</span>
      </div>
    )
  }

  if (!auth.isAuthenticated) {
    auth.signinRedirect()
    return null
  }

  return <>{children}</>
}
