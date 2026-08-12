import { useSyncExternalStore } from "react";
import { getOnlineStatus, subscribeOnlineStatus } from "../lib/onlineStatus";

/**
 * Whether the browser currently believes it has a network connection, kept
 * live via the window `online`/`offline` events (#1774).
 *
 * useSyncExternalStore rather than a useState/useEffect pair: the initial
 * value has to come from `navigator.onLine` at render time, not from an
 * effect that runs a tick later - a page loaded while already offline would
 * otherwise flash the wrong state on its very first paint, which is the one
 * paint that matters here.
 */
export function useOnlineStatus(): boolean {
	return useSyncExternalStore(subscribeOnlineStatus, getOnlineStatus);
}
