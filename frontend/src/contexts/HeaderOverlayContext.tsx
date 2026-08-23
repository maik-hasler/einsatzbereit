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
	isOverlaid: boolean;
	setOverlaid: (overlaid: boolean) => void;
}

const HeaderOverlayContext = createContext<HeaderOverlayValue>({
	isOverlaid: false,
	setOverlaid: () => undefined,
});

export function HeaderOverlayProvider({ children }: { children: ReactNode }) {
	const [overlayCount, setOverlayCount] = useState(0);

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

export function useHeaderOverlay(): boolean {
	return useContext(HeaderOverlayContext).isOverlaid;
}

export function useOverlaysHeader(): void {
	const { setOverlaid } = useContext(HeaderOverlayContext);
	useLayoutEffect(() => {
		setOverlaid(true);
		return () => setOverlaid(false);
	}, [setOverlaid]);
}
