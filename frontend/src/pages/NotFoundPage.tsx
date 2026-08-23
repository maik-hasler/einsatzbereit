import { useTranslation } from "react-i18next";
import PageEaten from "../assets/page-eaten.svg?react";
import { usePageTitle } from "../hooks/usePageTitle";
import Button from "../components/Button";
import { statusTitleClass } from "../lib/headingClasses";

export default function NotFoundPage() {
	const { t } = useTranslation();
	usePageTitle(t("notFound.title"));

	return (
		<div className="mx-auto flex max-w-lg flex-col items-center px-4 py-10 text-center sm:py-16">
			<div className="relative mb-8 flex items-center justify-center">
				<div
					aria-hidden="true"
					className="pointer-events-none absolute h-56 w-56 rounded-full bg-brand-100 blur-3xl"
				/>

				<PageEaten
					aria-hidden="true"
					className="relative h-auto w-52 text-brand-500 sm:w-64"
				/>
			</div>

			<h1 className={`text-gray-900 ${statusTitleClass}`}>
				{t("notFound.title")}
			</h1>
			<p className="mt-4 leading-relaxed text-gray-600">
				{t("notFound.description")}
			</p>

			<Button to="/" size="lg" className="mt-8 shadow-md">
				{t("notFound.backHome")}
			</Button>
		</div>
	);
}
