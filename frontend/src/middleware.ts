import type { MiddlewareHandler } from "astro";

export const onRequest: MiddlewareHandler = async ({ cookies, locals }, next) => {
  const session = cookies.get("session");

  if (!session) {
    locals.user = null;
    return next();
  }

  try {
    const tokenSet = JSON.parse(session.value);

    if (tokenSet.id_token) {
      // Decode JWT payload without re-verifying — the token was verified by
      // openid-client during the OIDC exchange
      const [, payload] = tokenSet.id_token.split(".");
      locals.user = JSON.parse(atob(payload));
    } else {
      locals.user = null;
    }
  } catch {
    locals.user = null;
  }

  return next();
};
