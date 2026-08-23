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

export function useQuickActions(actions: QuickAction[]) {
	const { setActions } = useQuickActionsCtx();
	useEffect(() => {
		setActions(actions);
		return () => setActions([]);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [actions]);
}
