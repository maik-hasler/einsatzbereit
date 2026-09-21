/**
 * Minimal typed publish/subscribe channel.
 *
 * The three buses in this folder (toast, avatar, session expiry) exist for one
 * reason: their producer is plain TypeScript with no access to React context.
 * `client/api-instance.ts` has to raise a toast on a 5xx and announce an
 * expired session on a 401, and `lib/unhandledRejection.ts` runs before any
 * component is mounted at all. A bus is the seam between those and the React
 * tree that renders the result.
 *
 * What was worth removing is not the buses but the three hand-copied
 * implementations of the same fifteen lines. Everything below is deliberate:
 *
 * - A `Set` rather than the `indexOf`/`splice` array the copies used: an
 *   O(1) unsubscribe, and no way for a bookkeeping slip to leave a listener
 *   registered twice.
 * - `publish` iterates over a snapshot. A listener that unsubscribes (or
 *   subscribes) while it is being called used to mutate the array mid-`forEach`
 *   and silently skip the next listener in line.
 */

type Listener<TPayload> = (payload: TPayload) => void;

export interface EventBus<TPayload> {
	/** Registers `listener` and returns the function that removes it again. */
	subscribe: (listener: Listener<TPayload>) => () => void;
	/** Calls every current listener. Payload-less buses call this with no argument. */
	publish: (payload: TPayload) => void;
}

export function createEventBus<TPayload = void>(): EventBus<TPayload> {
	const listeners = new Set<Listener<TPayload>>();

	function subscribe(listener: Listener<TPayload>): () => void {
		listeners.add(listener);
		return () => {
			listeners.delete(listener);
		};
	}

	function publish(payload: TPayload): void {
		for (const listener of [...listeners]) listener(payload);
	}

	return { subscribe, publish };
}
