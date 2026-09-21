/**
 * The bearer token the API client sends, held outside React.
 *
 * `react-oidc-context` owns the token and hands it out through `useAuth()`,
 * which only a component can call. The API client is a module: it has to be
 * able to read the current token from a plain function at request time.
 *
 * `hooks/useApiClient.ts` is the single writer, and it writes during render -
 * before the effect that fires the request runs, and before any handler the
 * rendered tree installs. An effect would be too late on the first render
 * after a sign-in: child effects run before their parents', so a page's own
 * fetch would go out with the previous token.
 *
 * Deliberately a plain module variable rather than a store with subscribers:
 * nothing needs to react to a token change, only to read the latest value at
 * the moment a request is built.
 */
let accessToken: string | undefined;

export function setAccessToken(token: string | undefined): void {
	accessToken = token;
}

export function getAccessToken(): string | undefined {
	return accessToken;
}
