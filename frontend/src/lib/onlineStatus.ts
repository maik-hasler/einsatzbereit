// Browser connectivity as a subscribable store, so React reads it through
// useSyncExternalStore (see hooks/useOnlineStatus.ts) instead of every
// component that cares wiring up its own pair of window listeners.
//
// Nothing in the app was offline-aware at all before #1774: reloading with no
// connection served the precached app shell (header, hero, filter chips,
// footer all came back) and then rendered "an unexpected error occurred" with
// a retry button that could not possibly succeed while the connection was
// down.

type Listener = () => void;

export function subscribeOnlineStatus(listener: Listener): () => void {
	window.addEventListener("online", listener);
	window.addEventListener("offline", listener);
	return () => {
		window.removeEventListener("online", listener);
		window.removeEventListener("offline", listener);
	};
}

/**
 * `navigator.onLine` is only ever trustworthy when it reads false: false means
 * there is definitely no network route, while true only means the browser has
 * an interface up - it can still be a captive portal or a dead uplink. That
 * asymmetry is exactly what the UI needs here, because this is only ever used
 * to downgrade an *already failed* request to an honest "you are offline",
 * never to predict that a request will succeed.
 */
export function getOnlineStatus(): boolean {
	return navigator.onLine;
}
