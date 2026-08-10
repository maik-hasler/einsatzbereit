import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router";
import VolunteerOpportunitiesList from "../components/VolunteerOpportunitiesList/VolunteerOpportunitiesList";
import PageHeaderBand from "../components/PageHeaderBand";
import Button from "../components/Button";
import { usePageTitle } from "../hooks/usePageTitle";
import { MagnifyingGlassIcon } from "../components/icons";

// The browse/search page. Until #1755's follow-up this list lived inside
// HomePage behind an "#opportunities" anchor, which is why the header had no
// primary navigation: there was no destination to navigate *to*, only a
// fragment on the landing page. Giving the list its own route is what makes
// "Find opportunities" a real nav item.
//
// Only the keyword box lives up here. Location deliberately does not: the
// filter bar below already owns a Location dropdown, and having both a hero
// "city" field and a "Standort" filter on one page meant two controls writing
// the same URL params with no indication of which one was in effect.
export default function OpportunitiesPage() {
	const { t } = useTranslation();
	usePageTitle(t("opportunitiesPage.title"));
	const [searchParams, setSearchParams] = useSearchParams();

	const urlKeyword = searchParams.get("q") ?? "";
	const [keyword, setKeyword] = useState(urlKeyword);

	// The list's own keyword pill can clear `q` out from under this box, so
	// follow the URL rather than owning the value outright.
	useEffect(() => {
		setKeyword(urlKeyword);
	}, [urlKeyword]);

	function handleSearch(e: FormEvent) {
		e.preventDefault();
		const next = new URLSearchParams(searchParams);
		if (keyword.trim()) next.set("q", keyword.trim());
		else next.delete("q");
		setSearchParams(next, { replace: true });
	}

	return (
		<>
			<PageHeaderBand
				eyebrow={t("opportunitiesPage.eyebrow")}
				title={t("opportunitiesPage.title")}
				lead={t("opportunitiesPage.lead")}
			>
				<form onSubmit={handleSearch} className="max-w-xl">
					<div className="flex flex-col gap-3 rounded-full bg-white/10 p-3 shadow-lg backdrop-blur-sm sm:flex-row sm:items-stretch">
						<div className="relative flex-1 rounded-full border border-gray-200 bg-gray-50 text-left transition-colors focus-within:border-brand-400 focus-within:bg-white">
							<MagnifyingGlassIcon className="pointer-events-none absolute top-1/2 left-4 h-4 w-4 -translate-y-1/2 text-gray-400" />
							<input
								type="text"
								aria-label={t("landing.heroSearchKeywordLabel")}
								placeholder={t("landing.heroSearchKeywordPlaceholder")}
								value={keyword}
								onChange={(e) => setKeyword(e.target.value)}
								data-testid="opportunities-keyword-input"
								className="w-full rounded-full border-0 bg-transparent py-3 pr-3 pl-10 text-sm text-gray-900 placeholder:text-gray-400 focus:outline-none"
							/>
						</div>
						<Button type="submit" size="lg" pill className="shrink-0 shadow-md">
							{t("landing.heroSearchButton")}
						</Button>
					</div>
				</form>
			</PageHeaderBand>

			<div id="opportunities" className="mb-20">
				<VolunteerOpportunitiesList />
			</div>
		</>
	);
}
