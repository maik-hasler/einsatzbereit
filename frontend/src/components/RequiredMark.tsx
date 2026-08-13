import { useTranslation } from "react-i18next";

// text-red-600 (5:1 on white), not the text-red-400 the wizard used before
// this component was shared - the glyph is the only visual carrier of
// "required", so it has to clear the AA text floor in frontend/AGENTS.md the
// same way error copy does.
const markClass = "text-red-600";

/**
 * The one required-field marker in the product (issue #1797).
 *
 * Three conventions used to coexist: this asterisk (create-opportunity
 * wizard), an asterisk baked into the translation string with a literal
 * space ("Name *", org settings) and a spelled-out "(required)" suffix
 * (sign-up and feedback modals). A marker inside a translated string can't
 * be aria-hidden, so org settings announced its field as "Name star".
 *
 * The asterisk won over the spelled-out variant because the wizard's
 * floating labels sit in grid columns as narrow as 5rem ("PLZ",
 * "Hausnummer") - a "(Pflichtfeld)" suffix does not fit there. It does need
 * a legend, so every form that renders a mark also renders exactly one
 * `RequiredFieldsLegend` above its fields.
 *
 * Keep it aria-hidden: the accessible half of "this field is required" is
 * the control's own `required`/`aria-required`, so announcing the marker
 * would only say it twice.
 */
export function RequiredMark() {
	return (
		<span className={`ml-0.5 ${markClass}`} aria-hidden="true">
			*
		</span>
	);
}

/** Explains the asterisk. Render once per form, above the fields. */
export function RequiredFieldsLegend({
	className = "",
}: {
	className?: string;
}) {
	const { t } = useTranslation();
	return (
		// aria-hidden for the same reason the mark is: it explains a purely
		// visual convention, and every marked control already announces itself
		// as required on its own.
		<p aria-hidden="true" className={`text-xs text-gray-500 ${className}`}>
			<span className={markClass}>*</span> {t("common.requiredField")}
		</p>
	);
}
