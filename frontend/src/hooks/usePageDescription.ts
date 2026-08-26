import { useEffect } from "react";

const DESCRIPTION_SELECTORS = [
	'meta[name="description"]',
	'meta[property="og:description"]',
	'meta[name="twitter:description"]',
];

export function usePageDescription(description?: string | null) {
	useEffect(() => {
		if (description === null) return;

		const elements = DESCRIPTION_SELECTORS.map((selector) =>
			document.querySelector(selector),
		);
		const previousContent = elements.map(
			(el) => el?.getAttribute("content") ?? null,
		);

		if (description) {
			elements.forEach((el) => el?.setAttribute("content", description));
		}

		return () => {
			elements.forEach((el, i) => {
				const value = previousContent[i];
				if (el && value !== null) el.setAttribute("content", value);
			});
		};
	}, [description]);
}
