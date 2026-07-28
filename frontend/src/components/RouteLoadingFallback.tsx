import { useTranslation } from "react-i18next";
import Spinner from "./Spinner";

export default function RouteLoadingFallback() {
	const { t } = useTranslation();
	return (
		<div className="flex min-h-[50vh] items-center justify-center">
			<Spinner label={t("common.loading")} size="lg" />
		</div>
	);
}
