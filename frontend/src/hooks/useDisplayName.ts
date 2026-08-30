import { useSyncExternalStore } from "react";
import {
	getDisplayNameOverride,
	subscribeDisplayName,
} from "../lib/displayName";

/**
 * The display name to show for `sub`: the one saved on /profile if there is
 * one, otherwise `fallback` from the id_token (see lib/displayName.ts).
 */
export function useDisplayName(
	sub: string | undefined,
	fallback: string,
): string {
	const override = useSyncExternalStore(
		subscribeDisplayName,
		() => getDisplayNameOverride(sub),
		() => null,
	);
	return override ?? fallback;
}
