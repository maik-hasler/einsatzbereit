import {
	createContext,
	useCallback,
	useContext,
	useEffect,
	useState,
	type ReactNode,
} from "react";

export type BreadcrumbItem = {
	label: string;
	href?: string;
};

type ToolbarConfig = {
	breadcrumbs: BreadcrumbItem[];
};

type ToolbarContextValue = {
	config: ToolbarConfig | null;
	setConfig: (config: ToolbarConfig | null) => void;
};

const ToolbarContext = createContext<ToolbarContextValue | null>(null);

export function ToolbarProvider({ children }: { children: ReactNode }) {
	const [config, setConfigState] = useState<ToolbarConfig | null>(null);
	const setConfig = useCallback((c: ToolbarConfig | null) => {
		setConfigState(c);
	}, []);
	return (
		<ToolbarContext.Provider value={{ config, setConfig }}>
			{children}
		</ToolbarContext.Provider>
	);
}

function useToolbarCtx() {
	const ctx = useContext(ToolbarContext);
	if (!ctx)
		throw new Error("usePageToolbar must be used within ToolbarProvider");
	return ctx;
}

export function useToolbarConfig() {
	return useToolbarCtx().config;
}

export function usePageToolbar(breadcrumbs: BreadcrumbItem[]) {
	const { setConfig } = useToolbarCtx();
	const key = JSON.stringify(breadcrumbs);
	useEffect(() => {
		setConfig({ breadcrumbs: JSON.parse(key) as BreadcrumbItem[] });
		return () => setConfig(null);
	}, [key, setConfig]);
}
