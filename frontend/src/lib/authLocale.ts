import i18n from "../i18n";

export function signinLocaleArgs(): { ui_locales: string } {
	return { ui_locales: i18n.language };
}
