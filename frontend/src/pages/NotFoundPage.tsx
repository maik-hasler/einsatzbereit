import { useTranslation } from "react-i18next";
import PageEaten from "../assets/page-eaten.svg?react";
import { usePageTitle } from "../hooks/usePageTitle";
import Button from "../components/Button";
import { statusTitleClass } from "../lib/headingClasses";

export default function NotFoundPage() {
	const { t } = useTranslation();
	usePageTitle(t("notFound.title"));

	return (
		<div className="relative flex min-h-[70vh] items-center justify-center overflow-hidden px-4 text-center">
			<PageEaten className="absolute top-1/2 w-85 translate-y-[-60%] text-brand-500 opacity-35 sm:w-105" />

			{/* Content */}
			<div className="relative z-10 max-w-md">
				<h1
					className={`mb-4 tracking-tight text-brand-700 ${statusTitleClass}`}
				>
					{t("notFound.title")}
				</h1>

				<p className="mb-8 text-black">{t("notFound.description")}</p>

				<Button to="/" size="lg" className="shadow-lg">
					{t("notFound.backHome")}
				</Button>
			</div>
		</div>
	);
}
