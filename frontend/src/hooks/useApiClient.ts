import { useAuth } from 'react-oidc-context'
import { useMemo } from 'react'
import { createApiClient } from '../client/api-instance'

export function useApiClient() {
  const { user } = useAuth()
  return useMemo(() => createApiClient(user?.access_token), [user?.access_token])
}
