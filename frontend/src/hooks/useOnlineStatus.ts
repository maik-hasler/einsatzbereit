import { useSyncExternalStore } from "react";
import { getOnlineStatus, subscribeOnlineStatus } from "../lib/onlineStatus";

export function useOnlineStatus(): boolean {
	return useSyncExternalStore(subscribeOnlineStatus, getOnlineStatus);
}
