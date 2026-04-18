import { EinsatzbereitApi } from './api-client'

export function createApiClient(accessToken?: string): EinsatzbereitApi {
  return new EinsatzbereitApi(import.meta.env.VITE_API_URL, {
    fetch: (url: RequestInfo, init?: RequestInit) =>
      globalThis.fetch(url, {
        ...init,
        headers: {
          ...init?.headers,
          ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
        },
      }),
  })
}
