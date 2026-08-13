import {
	createContext,
	useCallback,
	useContext,
	useEffect,
	useState,
	type ReactNode,
} from "react";

type OrgBreadcrumbContextValue = {
	extra: string | null;
	setExtra: (label: string | null) => void;
};

const OrgBreadcrumbContext = createContext<OrgBreadcrumbContextValue | null>(
	null,
);

export function OrgBreadcrumbProvider({ children }: { children: ReactNode }) {
	const [extra, setExtraState] = useState<string | null>(null);
	const setExtra = useCallback((label: string | null) => {
		setExtraState(label);
	}, []);
	return (
		<OrgBreadcrumbContext.Provider value={{ extra, setExtra }}>
			{children}
		</OrgBreadcrumbContext.Provider>
	);
}

function useOrgBreadcrumbCtx() {
	const ctx = useContext(OrgBreadcrumbContext);
	if (!ctx)
		throw new Error(
			"useOrgBreadcrumbExtra/useSetOrgBreadcrumbExtra must be used within OrgBreadcrumbProvider",
		);
	return ctx;
}

export function useOrgBreadcrumbExtra() {
	return useOrgBreadcrumbCtx().extra;
}

// Lets a page nested under OrgAppLayout add a trailing breadcrumb segment
// beyond its tab (e.g. the specific opportunity being managed).
export function useSetOrgBreadcrumbExtra(label: string | null | undefined) {
	const { setExtra } = useOrgBreadcrumbCtx();
	useEffect(() => {
		setExtra(label ?? null);
		return () => setExtra(null);
	}, [label, setExtra]);
}
