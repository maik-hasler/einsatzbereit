import {
	createContext,
	useContext,
	useEffect,
	useMemo,
	useState,
	type ReactNode,
} from "react";

export type QuickAction = {
	key: string;
	label: string;
	icon: ReactNode;
	onClick: () => void;
	variant?: "primary" | "default";
	disabled?: boolean;
	// Surfaced as the button's `title` - mainly for a disabled action, where
	// the native `disabled` attribute alone gives no indication why (see
	// useEditModeQuickActions's editDisabledTitle).
	title?: string;
};

type QuickActionsContextValue = {
	actions: QuickAction[];
	setActions: (actions: QuickAction[]) => void;
};

const QuickActionsContext = createContext<QuickActionsContextValue | null>(
	null,
);

export function QuickActionsProvider({ children }: { children: ReactNode }) {
	const [actions, setActions] = useState<QuickAction[]>([]);
	// Memoized so consumers (useQuickActionsList/useQuickActions) only
	// re-render when the actions actually change, not on every Provider
	// render - a fresh `{ actions, setActions }` literal every render would
	// otherwise re-trigger useQuickActions's effect below even when nothing
	// changed, see its own comment for why that matters.
	const value = useMemo(() => ({ actions, setActions }), [actions]);
	return (
		<QuickActionsContext.Provider value={value}>
			{children}
		</QuickActionsContext.Provider>
	);
}

function useQuickActionsCtx() {
	const ctx = useContext(QuickActionsContext);
	if (!ctx)
		throw new Error("useQuickActions must be used within QuickActionsProvider");
	return ctx;
}

export function useQuickActionsList() {
	return useQuickActionsCtx().actions;
}

// Opt-in mechanism for a page nested under either AppLayout or OrgAppLayout
// to publish the action-bar quick actions rendered by PageHeaderBand/
// OrgPageHeader (both call useQuickActionsList) - the same shape as
// useSetOrgBreadcrumbExtra. Not calling it means no quick actions render for
// that page.
//
// `actions` MUST be a referentially stable array (useMemo, or a literal with
// no per-render-changing dependencies) - a fresh array/objects on every
// render make this effect re-fire every render, which calls setActions,
// which re-renders every consumer (including whichever page called this
// hook), which recreates `actions` again: an infinite render loop that pins
// a CPU core and starves other pending work (fetches, navigation) on the
// page indefinitely. See useEditModeQuickActions for the pattern (useMemo
// keyed on the primitive/visual deps, refs for the callbacks so they can't
// go stale without needing to be memo deps).
export function useQuickActions(actions: QuickAction[]) {
	const { setActions } = useQuickActionsCtx();
	useEffect(() => {
		setActions(actions);
		return () => setActions([]);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [actions]);
}
