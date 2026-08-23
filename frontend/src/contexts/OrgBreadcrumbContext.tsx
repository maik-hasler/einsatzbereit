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

	extraLang: string | null;
	setExtra: (label: string | null, lang?: string | null) => void;
};

const OrgBreadcrumbContext = createContext<OrgBreadcrumbContextValue | null>(
	null,
);

export function OrgBreadcrumbProvider({ children }: { children: ReactNode }) {
	const [extra, setExtraState] = useState<string | null>(null);
	const [extraLang, setExtraLangState] = useState<string | null>(null);
	const setExtra = useCallback((label: string | null, lang?: string | null) => {
		setExtraState(label);
		setExtraLangState(lang ?? null);
	}, []);
	return (
		<OrgBreadcrumbContext.Provider value={{ extra, extraLang, setExtra }}>
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

export function useOrgBreadcrumbExtraLang() {
	return useOrgBreadcrumbCtx().extraLang;
}

export function useSetOrgBreadcrumbExtra(
	label: string | null | undefined,
	lang?: string | null,
) {
	const { setExtra } = useOrgBreadcrumbCtx();
	useEffect(() => {
		setExtra(label ?? null, lang);
		return () => setExtra(null);
	}, [label, lang, setExtra]);
}
