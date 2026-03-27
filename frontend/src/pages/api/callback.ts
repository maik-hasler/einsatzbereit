import type { APIRoute } from "astro";
import { discovery, allowInsecureRequests, authorizationCodeGrant } from "openid-client";
import { KEYCLOAK_AUTHORITY_URL } from "astro:env/server";

export const GET: APIRoute = async ({ request, cookies, redirect }) => {
  const codeVerifier = cookies.get("code_verifier")?.value;

  if (!codeVerifier) {
    return redirect("/api/login");
  }

  try {
    const config = await discovery(
      new URL(KEYCLOAK_AUTHORITY_URL),
      "frontend",
      undefined,
      undefined,
      { execute: [allowInsecureRequests] },
    );

    const tokenSet = await authorizationCodeGrant(
      config,
      new URL(request.url),
      { pkceCodeVerifier: codeVerifier }
    );

    cookies.delete("code_verifier", { path: "/" });

    cookies.set("session", JSON.stringify(tokenSet), {
      httpOnly: true,
      secure: process.env.NODE_ENV === "production",
      path: "/",
      sameSite: "lax",
    });

    return redirect("/");
  } catch (err) {
    console.error("OIDC callback error:", err);
    return redirect("/auth-error");
  }
};
