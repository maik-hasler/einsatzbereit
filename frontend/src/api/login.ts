import type { APIRoute } from "astro";
import {
  discovery,
  buildAuthorizationUrl,
  randomPKCECodeVerifier,
  calculatePKCECodeChallenge
} from "openid-client";
import { KEYCLOAK_AUTHORITY_URL, REDIRECT_URI } from "astro:env/server";

export const GET: APIRoute = async ({ cookies, redirect }) => {
  const config = await discovery(
    new URL(KEYCLOAK_AUTHORITY_URL),
    "frontend"
  );

  const codeVerifier = randomPKCECodeVerifier();
  const codeChallenge = await calculatePKCECodeChallenge(codeVerifier);

  cookies.set("code_verifier", codeVerifier, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    path: "/",
    sameSite: "lax",
  });

  const url = buildAuthorizationUrl(config, {
    redirect_uri: REDIRECT_URI,
    scope: "openid profile email",
    code_challenge: codeChallenge,
    code_challenge_method: "S256",
  });

  return redirect(url.toString());
};
