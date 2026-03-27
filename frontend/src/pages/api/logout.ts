import type { APIRoute } from "astro";
import { discovery, allowInsecureRequests } from "openid-client";
import { KEYCLOAK_AUTHORITY_URL } from "astro:env/server";

export const GET: APIRoute = async ({ cookies, redirect, url }) => {
  const sessionCookie = cookies.get("session")?.value;
  let idToken: string | undefined;

  if (sessionCookie) {
    try {
      const tokenSet = JSON.parse(sessionCookie);
      idToken = tokenSet.id_token;
    } catch {
      // corrupted session, just clear it
    }
  }

  cookies.delete("session", { path: "/" });
  cookies.delete("code_verifier", { path: "/" });

  if (idToken) {
    const config = await discovery(
      new URL(KEYCLOAK_AUTHORITY_URL),
      "frontend",
      undefined,
      undefined,
      { execute: [allowInsecureRequests] },
    );

    const endSessionUrl = new URL(config.serverMetadata().end_session_endpoint as string);
    endSessionUrl.searchParams.set("id_token_hint", idToken);
    endSessionUrl.searchParams.set("post_logout_redirect_uri", url.origin);

    return redirect(endSessionUrl.toString());
  }

  return redirect("/");
};
