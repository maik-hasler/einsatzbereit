import { useTranslation } from "react-i18next";

const markClass = "text-red-600";

export function RequiredMark() {
	return (
		<span className={`ml-0.5 ${markClass}`} aria-hidden="true">
			*
		</span>
	);
}

export function RequiredFieldsLegend({
	className = "",
}: {
	className?: string;
}) {
	const { t } = useTranslation();
	return (
		<p aria-hidden="true" className={`text-xs text-gray-500 ${className}`}>
			<span className={markClass}>*</span> {t("common.requiredField")}
		</p>
	);
}
