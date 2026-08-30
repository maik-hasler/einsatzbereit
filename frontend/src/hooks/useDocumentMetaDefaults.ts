import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { setDocumentMetaDefaults } from "../lib/documentMeta";

// index.html's meta is German, because German is the default language and a
// static file cannot be anything else. Without this, every route that sets no
// description of its own kept serving that German prose under
// `<html lang="en">`, and og:title/twitter:title kept the German marketing
// line on every route (#2328).
export function useDocumentMetaDefaults() {
	const { t, i18n } = useTranslation();

	useEffect(() => {
		setDocumentMetaDefaults({
			socialTitle: t("meta.socialTitle"),
			description: t("meta.description"),
		});
	}, [t, i18n.language]);
}
