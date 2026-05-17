import { Link } from "react-router";
import { useTranslation } from "react-i18next";

export default function ErrorPage() {
	const { t } = useTranslation();
	return (
		<div className="flex min-h-[70vh] flex-col items-center justify-center gap-6 px-4 text-center">
			<h1 className="text-4xl font-bold text-gray-900">
				{t("error.500Title")}
			</h1>
			<p className="max-w-md text-gray-500">{t("error.500Message")}</p>
			<Link
				to="/"
				className="rounded bg-black px-4 py-2 text-sm text-white hover:bg-gray-800"
			>
				{t("error.backHome")}
			</Link>
		</div>
	);
}
