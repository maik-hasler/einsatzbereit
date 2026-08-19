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
	/** `extra`'s actual language, when it may differ from the active UI
	 * language - e.g. a German-only opportunity title set as the nested-page
	 * title under the English UI (einsatzbereit#2057). Null when `extra` is
	 * always in the UI language, or when `extra` itself is null. */
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

/** Companion to `useOrgBreadcrumbExtra` - see `extraLang` above. */
export function useOrgBreadcrumbExtraLang() {
	return useOrgBreadcrumbCtx().extraLang;
}

// Lets a page nested under OrgAppLayout add a trailing breadcrumb segment
// beyond its tab (e.g. the specific opportunity being managed). `lang`
// carries the label's actual language when it may differ from the UI
// language (einsatzbereit#2057) - omit when it always matches.
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
