import i18n from "../i18n";

export function signinLocaleArgs(returnTo?: string): {
	ui_locales: string;
	state?: { returnTo: string };
} {
	return {
		ui_locales: i18n.language,
		state: returnTo !== undefined ? { returnTo } : undefined,
	};
}
