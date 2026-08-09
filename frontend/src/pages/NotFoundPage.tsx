import { useTranslation } from "react-i18next";
import PageEaten from "../assets/page-eaten.svg?react";
import { usePageTitle } from "../hooks/usePageTitle";
import Button from "../components/Button";
import { statusTitleClass } from "../lib/headingClasses";

export default function NotFoundPage() {
	const { t } = useTranslation();
	usePageTitle(t("notFound.title"));

	// The illustration used to be absolutely positioned behind the copy at 35%
	// opacity, which put the dog's head directly across the heading and the
	// description at common desktop viewport sizes (#1755). It now stacks above
	// the text at full strength and carries the page instead of haunting it -
	// a soft brand-100 glow behind it keeps the shape from floating on bare
	// white, echoing the blur-blob lighting the landing hero uses.
	return (
		<div className="mx-auto flex max-w-lg flex-col items-center px-4 py-10 text-center sm:py-16">
			<div className="relative mb-8 flex items-center justify-center">
				<div
					aria-hidden="true"
					className="pointer-events-none absolute h-56 w-56 rounded-full bg-brand-100 blur-3xl"
				/>
				{/* h-auto is load-bearing: the asset carries its own width/height
				attributes (645x750), so setting only a width left the box 750px
				tall and letterboxed the drawing inside ~258px of dead space top
				and bottom. aria-hidden because the file also hardcodes a German
				role="img" aria-label - which announced itself in German on the
				English site and only restated the <h1> below in any case. */}
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
