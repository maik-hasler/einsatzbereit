import {
	createContext,
	useCallback,
	useContext,
	useLayoutEffect,
	useMemo,
	useState,
} from "react";
import type { ReactNode } from "react";

interface HeaderOverlayValue {
	/** True while a page is rendering a dark band underneath the header. */
	isOverlaid: boolean;
	setOverlaid: (overlaid: boolean) => void;
}

const HeaderOverlayContext = createContext<HeaderOverlayValue>({
	isOverlaid: false,
	setOverlaid: () => undefined,
});

// Lets a page tell the header "I'm painting a dark band behind you, go
// transparent". Before #1755 the header hardcoded `location.pathname === "/"`
// to decide this, which meant any new page wanting the treatment had to edit
// the header - and the check silently stopped matching when the homepage hero
// became a boxed card that no longer runs under the header.
//
// The declaration lives with the thing that creates the condition
// (PageHeaderBand) rather than with the component reacting to it, so the two
// can't drift apart.
export function HeaderOverlayProvider({ children }: { children: ReactNode }) {
	const [overlayCount, setOverlayCount] = useState(0);

	// Counter, not a boolean: during a route change React mounts the incoming
	// page before unmounting the outgoing one, so a plain flag would have the
	// new band's "true" overwritten by the old band's cleanup "false", leaving
	// the header opaque on top of a dark band.
	const setOverlaid = useCallback((overlaid: boolean) => {
		setOverlayCount((count) => count + (overlaid ? 1 : -1));
	}, []);

	const value = useMemo(
		() => ({ isOverlaid: overlayCount > 0, setOverlaid }),
		[overlayCount, setOverlaid],
	);

	return (
		<HeaderOverlayContext.Provider value={value}>
			{children}
		</HeaderOverlayContext.Provider>
	);
}

/** Read by AppLayout to decide how to render the header. */
export function useHeaderOverlay(): boolean {
	return useContext(HeaderOverlayContext).isOverlaid;
}

/**
 * Declares that the calling component paints a dark band under the header for
 * as long as it is mounted. useLayoutEffect, not useEffect - on a plain effect
 * the header paints one opaque frame over the band before flipping, which
 * reads as a flash of a white bar on every navigation into such a page.
 */
export function useOverlaysHeader(): void {
	const { setOverlaid } = useContext(HeaderOverlayContext);
	useLayoutEffect(() => {
		setOverlaid(true);
		return () => setOverlaid(false);
	}, [setOverlaid]);
}
