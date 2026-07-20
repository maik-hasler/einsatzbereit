import {
	createContext,
	useContext,
	useEffect,
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
	return (
		<QuickActionsContext.Provider value={{ actions, setActions }}>
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

// Opt-in mechanism for a page nested under OrgAppLayout to publish the
// action-bar quick actions rendered right of the breadcrumb (see Header.tsx's
// `breadcrumb.actions` and OrgAppLayout.tsx) - the same shape as
// useSetOrgBreadcrumbExtra/usePageToolbar. Not calling it means no quick
// actions render for that page.
export function useQuickActions(actions: QuickAction[]) {
	const { setActions } = useQuickActionsCtx();
	useEffect(() => {
		setActions(actions);
		return () => setActions([]);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [actions]);
}
