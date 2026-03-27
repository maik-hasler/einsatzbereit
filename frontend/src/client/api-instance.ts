import type { AstroCookies } from "astro";
import { API_URL } from "astro:env/server";
import { EinsatzbereitApi } from "./api-client";

export function createApiClient(cookies: AstroCookies): EinsatzbereitApi {
  const session = cookies.get("session");
  if (!session) {
    throw new Error("No session cookie — user is not authenticated");
  }

  const { access_token } = JSON.parse(session.value);

  return new EinsatzbereitApi(API_URL, {
    fetch: (url: RequestInfo, init?: RequestInit) =>
      globalThis.fetch(url, {
        ...init,
        headers: {
          ...init?.headers,
          Authorization: `Bearer ${access_token}`,
        },
      }),
  });
}
