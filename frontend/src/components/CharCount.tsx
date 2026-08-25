import { useTranslation } from "react-i18next";

interface Props {
	current: number;
	max: number;
}

export default function CharCount({ current, max }: Props) {
	const { t } = useTranslation();
	return (
		<p className="mt-1 text-right text-xs text-gray-500">
			{t("common.charCount", { current, max })}
		</p>
	);
}
