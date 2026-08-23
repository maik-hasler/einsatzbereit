import { lazy, Suspense, useEffect, useId, useState } from "react";
import type { FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useSearchParams } from "react-router";
import { useAuth } from "react-oidc-context";
import type { CitySuggestion } from "../components/VolunteerOpportunitiesList/useCitySuggestions";
import LocationSearchInput from "../components/LocationSearchInput";
import Button from "../components/Button";
import FaqAccordion from "../components/FaqAccordion";
import LatestOpportunitiesSection from "../components/LatestOpportunitiesSection";
import ModalLoadingFallback from "../components/ModalLoadingFallback";
import Skeleton from "../components/Skeleton";
import { usePageTitle } from "../hooks/usePageTitle";
import { useMyOrganizations } from "../hooks/useMyOrganizations";
import { useApiClient } from "../hooks/useApiClient";
import { WAVE_PATH } from "../lib/wavePath";
import { signinLocaleArgs } from "../lib/authLocale";
import { signinRedirectForRegistration } from "../lib/keycloakRegistration";
import { getActiveOrgId, resolveOrgAppPath } from "../lib/activeOrg";
import {
	filterByLabelMatch,
	sortByLabelPrefixMatch,
} from "../lib/citySuggestionSort";
import { dispatchToast } from "../lib/toastBus";
import {
	MagnifyingGlassIcon,
	PlusIcon,
	UserGroupIcon,
	ShieldCheckIcon,
} from "../components/icons";
import type { Organization } from "../client/api-client";

const CreateOrganizationModal = lazy(
	() => import("../components/CreateOrganizationModal"),
);

export default function HomePage() {
	const { t } = useTranslation();
	usePageTitle(t("landing.pageTitle"));
	const auth = useAuth();
	const navigate = useNavigate();
	const api = useApiClient();

	const heroTitleId = useId();
	const missionTitleId = useId();
	const orgCtaTitleId = useId();
	const faqTitleId = useId();

	const [showOrgModal, setShowOrgModal] = useState(false);
	const [searchParams, setSearchParams] = useSearchParams();

	const {
		orgs,
		loading: orgsLoading,
		failed: orgsFailed,
	} = useMyOrganizations();
	const orgAppPath = resolveOrgAppPath(orgs, getActiveOrgId());

	const [heroKeyword, setHeroKeyword] = useState(
		() => searchParams.get("q") ?? "",
	);
	const [heroCityInput, setHeroCityInput] = useState(
		() => searchParams.get("city") ?? "",
	);
	const [heroLocation, setHeroLocation] = useState<CitySuggestion | null>(null);
	const [resolvingHeroLocation, setResolvingHeroLocation] = useState(false);

	useEffect(() => {
		if (searchParams.get("createOrg") === "1" && auth.isAuthenticated) {
			setShowOrgModal(true);
			const next = new URLSearchParams(searchParams);
			next.delete("createOrg");
			setSearchParams(next, { replace: true });
		}
	}, [searchParams, auth.isAuthenticated, setSearchParams]);

	const urlKeyword = searchParams.get("q") ?? "";
	const urlCity = searchParams.get("city") ?? "";
	const urlLat = searchParams.get("lat") ?? "";
	const urlLng = searchParams.get("lng") ?? "";
	useEffect(() => {
		setHeroKeyword(urlKeyword);
		setHeroCityInput(urlCity);
		setHeroLocation(
			urlCity && urlLat && urlLng
				? { label: urlCity, lat: Number(urlLat), lng: Number(urlLng) }
				: null,
		);
	}, [urlKeyword, urlCity, urlLat, urlLng]);

	async function handleHeroSearch(e: FormEvent) {
		e.preventDefault();
		let location = heroLocation;
		if (!location && heroCityInput.trim()) {
			setResolvingHeroLocation(true);
			try {
				const places = await api.searchCities(heroCityInput.trim());
				const [best] = sortByLabelPrefixMatch(
					filterByLabelMatch(
						places.map((place) => ({
							label: place.label,
							lat: place.latitude,
							lng: place.longitude,
						})),
						heroCityInput,
					),
					heroCityInput,
				);
				location = best ?? null;
			} catch {
				location = null;
			} finally {
				setResolvingHeroLocation(false);
			}
			if (!location) {
				dispatchToast("info", t("landing.heroSearchLocationNotFound"));
			}
		}
		const next = new URLSearchParams();
		if (heroKeyword.trim()) next.set("q", heroKeyword.trim());
		if (location) {
			next.set("city", location.label);
			next.set("lat", String(location.lat));
			next.set("lng", String(location.lng));
			next.set("radius", "10");
		}
		const query = next.toString();
		navigate(query ? `/opportunities?${query}` : "/opportunities");
	}

	function handleOrgCta() {
		if (auth.isAuthenticated) {
			setShowOrgModal(true);
		} else {
			void signinRedirectForRegistration({
				...signinLocaleArgs(),
				state: { returnTo: "/?createOrg=1" },
			});
		}
	}

	function handleOrgCreated(newOrg: Organization) {
		setShowOrgModal(false);
		navigate(`/app/${newOrg.id?.value}/dashboard`);
	}

	const orgFeatures = [
		{
			icon: <PlusIcon className="h-6 w-6" />,
			title: t("landing.orgFeature1Title"),
			desc: t("landing.orgFeature1Desc"),
		},
		{
			icon: <UserGroupIcon className="h-6 w-6" />,
			title: t("landing.orgFeature2Title"),
			desc: t("landing.orgFeature2Desc"),
		},
		{
			icon: <ShieldCheckIcon className="h-6 w-6" />,
			title: t("landing.orgFeature3Title"),
			desc: t("landing.orgFeature3Desc"),
		},
	];

	const faqItems = [
		{ q: t("help.generalQ1"), a: t("help.generalA1") },
		{ q: t("help.generalQ2"), a: t("help.generalA2") },
		{ q: t("help.generalQ3"), a: t("help.generalA3") },
		{ q: t("help.generalQ4"), a: t("help.generalA4") },
	];

	return (
		<>
			<section aria-labelledby={heroTitleId} className="mb-20">
				<div className="animate-fade-up relative isolate overflow-hidden rounded-card bg-brand-800 shadow-resting">
					<div
						aria-hidden="true"
						className="pointer-events-none absolute -top-40 -left-40 h-120 w-120 rounded-full bg-brand-700 opacity-60 blur-3xl"
					/>
					<div
						aria-hidden="true"
						className="pointer-events-none absolute top-0 -right-32 h-80 w-80 rounded-full bg-brand-600 opacity-40 blur-3xl"
					/>
					<div
						aria-hidden="true"
						className="pointer-events-none absolute bottom-12 left-1/2 h-56 w-125 -translate-x-1/2 rounded-full bg-accent-400 opacity-10 blur-3xl"
					/>

					<svg width="0" height="0" aria-hidden="true" className="absolute">
						<defs>
							<clipPath id="hero-stone-1" clipPathUnits="objectBoundingBox">
								<path d="M0.941,0.199 C1,0.306,1,0.775,0.909,0.857 C0.81,0.939,0.354,1,0.239,0.986 C0.125,0.936,0,0.648,0,0.569 C0,0.463,0.082,0.068,0.197,0.017 C0.337,-0.044,0.823,0.069,0.941,0.199" />
							</clipPath>
							<clipPath id="hero-stone-2" clipPathUnits="objectBoundingBox">
								<path d="M0.924,0.106 C0.986,0.152,1,0.253,1,0.324 C1,0.395,0.824,0.999,0.622,1 C0.415,1,0.162,0.843,0.042,0.663 C-0.018,0.573,-0.001,0.324,0.018,0.252 C0.037,0.18,0.157,0.03,0.24,0.005 C0.33,-0.022,0.863,0.061,0.924,0.106" />
							</clipPath>
							<clipPath id="hero-stone-3" clipPathUnits="objectBoundingBox">
								<path d="M0.517,0 C0.58,0.003,0.64,0.02,0.694,0.049 C0.747,0.079,0.791,0.12,0.821,0.169 C0.868,0.245,1,0.955,0.955,0.996 C0.842,1,0.7,0.869,0.453,0.806 C0.218,0.747,0.032,0.737,0.003,0.628 C-0.025,0.519,0.134,0.188,0.202,0.126 C0.285,0.05,0.398,0.005,0.517,0" />
							</clipPath>
							<clipPath id="hero-stone-4" clipPathUnits="objectBoundingBox">
								<path d="M0.704,0.043 C0.64,0.017,0.448,-0.023,0.349,0.018 C0.245,0.062,0.117,0.317,0.072,0.418 C0.028,0.519,-0.027,0.818,0.016,0.897 C0.045,0.95,0.109,1,0.242,0.979 C0.411,0.898,0.976,0.458,0.999,0.381 C1,0.305,0.768,0.069,0.704,0.043" />
							</clipPath>
							<clipPath id="hero-stone-5" clipPathUnits="objectBoundingBox">
								<path d="M0.996,0.23 C1,0.296,0.946,0.473,0.861,0.648 C0.776,0.823,0.71,0.984,0.613,0.999 C0.519,1,0.337,0.862,0.218,0.735 C0.131,0.643,0.048,0.549,0.025,0.502 C-0.013,0.426,-0.006,0.312,0.038,0.257 C0.085,0.197,0.175,0.204,0.417,0.139 C0.602,0.089,0.668,-0.008,0.76,0 C0.837,0.007,0.975,0.164,0.996,0.23" />
							</clipPath>
						</defs>
					</svg>

					<div
						aria-hidden="true"
						style={{ clipPath: "url(#hero-stone-1)" }}
						className="pointer-events-none absolute -top-2 -left-2 hidden h-20 w-20 shadow-raised sm:block sm:h-24 sm:w-24 lg:-top-8 lg:-left-8 lg:h-64 lg:w-64 xl:h-72 xl:w-72"
					>
						<img
							src="/images/hero/volunteer-1.jpg"
							alt=""
							className="h-full w-full object-cover"
						/>
					</div>
					<div
						aria-hidden="true"
						style={{ clipPath: "url(#hero-stone-2)" }}
						className="pointer-events-none absolute top-52 -left-12 hidden h-40 w-40 bg-brand-500 shadow-raised lg:block"
					/>
					<div
						aria-hidden="true"
						style={{ clipPath: "url(#hero-stone-3)" }}
						className="pointer-events-none absolute -bottom-2 -left-2 hidden h-20 w-20 shadow-raised sm:block sm:h-24 sm:w-24 lg:bottom-6 lg:left-28 lg:h-36 lg:w-36"
					>
						<img
							src="/images/hero/volunteer-3.jpg"
							alt=""
							className="h-full w-full object-cover"
						/>
					</div>
					<div
						aria-hidden="true"
						style={{ clipPath: "url(#hero-stone-4)" }}
						className="pointer-events-none absolute -top-2 -right-2 h-24 w-24 bg-accent-400 shadow-raised sm:h-28 sm:w-28 lg:-top-8 lg:-right-8 lg:h-72 lg:w-72 xl:h-80 xl:w-80"
					/>
					<div
						aria-hidden="true"
						style={{ clipPath: "url(#hero-stone-5)" }}
						className="pointer-events-none absolute -right-2 -bottom-2 hidden h-20 w-20 shadow-raised sm:block sm:h-24 sm:w-24 lg:-right-8 lg:bottom-2 lg:h-64 lg:w-64 xl:h-72 xl:w-72"
					>
						<img
							src="/images/hero/volunteer-2.jpg"
							alt=""
							className="h-full w-full object-cover"
						/>
					</div>

					<div className="relative px-4 pt-24 pb-12 text-center sm:px-8 sm:pt-28 sm:pb-32 lg:px-10 lg:py-28">
						<h1
							id={heroTitleId}
							className="animate-fade-up-d1 mx-auto max-w-3xl font-display text-5xl font-bold tracking-tight text-white sm:text-6xl lg:text-7xl xl:text-8xl"
						>
							{t("landing.heroTitle")}
						</h1>
						<p className="animate-fade-up-d2 mx-auto mt-5 max-w-xl text-base leading-relaxed text-brand-100 sm:mt-6 sm:text-lg xl:text-xl">
							{t("landing.heroSubtitle")}
						</p>

						<form
							onSubmit={handleHeroSearch}
							className="animate-fade-up-d3 mx-auto mt-8 max-w-2xl sm:mt-10"
						>
							<div className="flex flex-col gap-3 rounded-full bg-white/10 p-3 shadow-lg backdrop-blur-sm sm:flex-row sm:items-stretch">
								<div className="flex-1 rounded-full border border-gray-200 bg-gray-50 text-left transition-colors focus-within:border-brand-400 focus-within:bg-white">
									<LocationSearchInput
										id="hero-location-search"
										value={heroCityInput}
										onValueChange={(value) => {
											setHeroCityInput(value);
											setHeroLocation(null);
										}}
										onSelect={(suggestion) => {
											setHeroLocation(suggestion);
											setHeroCityInput(suggestion.label);
										}}
										placeholder={t("landing.heroSearchLocationPlaceholder")}
										ariaLabel={t("landing.heroSearchLocationLabel")}
										inputClassName="w-full rounded-full border-0 bg-transparent py-3 pr-8 pl-10 text-sm text-gray-900 placeholder:text-gray-600 focus:outline-none"
									/>
								</div>

								<div className="relative flex-1 rounded-full border border-gray-200 bg-gray-50 text-left transition-colors focus-within:border-brand-400 focus-within:bg-white">
									<MagnifyingGlassIcon className="pointer-events-none absolute top-1/2 left-4 h-4 w-4 -translate-y-1/2 text-gray-400" />
									<input
										type="text"
										aria-label={t("landing.heroSearchKeywordLabel")}
										placeholder={t("landing.heroSearchKeywordPlaceholder")}
										value={heroKeyword}
										onChange={(e) => setHeroKeyword(e.target.value)}
										data-testid="hero-keyword-input"
										className="w-full rounded-full border-0 bg-transparent py-3 pr-3 pl-10 text-sm text-gray-900 placeholder:text-gray-600 focus:outline-none"
									/>
								</div>

								<Button
									type="submit"
									size="lg"
									pill
									disabled={resolvingHeroLocation}
									className="shrink-0 shadow-md"
								>
									{t("landing.heroSearchButton")}
								</Button>
							</div>
						</form>
					</div>
				</div>
			</section>

			<LatestOpportunitiesSection />

			<section
				id="for-organizations"
				aria-labelledby={orgCtaTitleId}
				className="relative left-1/2 w-screen -translate-x-1/2 scroll-mt-[var(--header-height)]"
			>
				<svg
					aria-hidden="true"
					viewBox="0 0 1440 60"
					preserveAspectRatio="none"
					className="block h-8 w-full text-brand-800 sm:h-12"
				>
					<path d={WAVE_PATH} fill="currentColor" />
				</svg>

				<div className="bg-brand-800">
					<div className="relative isolate overflow-hidden">
						<div
							aria-hidden="true"
							className="pointer-events-none absolute -top-16 -left-16 h-64 w-64 rounded-full bg-brand-700 opacity-60 blur-3xl"
						/>
						<div
							aria-hidden="true"
							className="pointer-events-none absolute -right-10 -bottom-10 h-56 w-56 rounded-full bg-accent-400 opacity-10 blur-3xl"
						/>

						<div className="relative mx-auto max-w-page px-4 py-10 sm:px-6 sm:py-14 lg:px-8">
							<div className="animate-fade-up mx-auto max-w-2xl text-center">
								<p className="mb-3 text-xs font-semibold tracking-widest text-brand-100 uppercase">
									{t("landing.orgCtaLabel")}
								</p>
								<h2
									id={orgCtaTitleId}
									className="font-display text-3xl font-bold text-white sm:text-4xl"
								>
									{t("landing.orgCtaTitle")}
								</h2>
								<p className="mt-4 text-base leading-relaxed text-white/80">
									{t("landing.orgCtaText")}
								</p>
							</div>

							<div className="mx-auto mt-10 grid max-w-4xl gap-8 sm:grid-cols-3">
								{orgFeatures.map(({ icon, title, desc }, index) => (
									<div
										key={title}
										className={`text-center ${
											index === 0
												? "animate-fade-up"
												: index === 1
													? "animate-fade-up-d1"
													: "animate-fade-up-d2"
										}`}
									>
										<div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-white/15 text-white">
											{icon}
										</div>
										<h3 className="mb-2 text-base font-semibold text-white">
											{title}
										</h3>
										<p className="text-sm leading-relaxed text-white/80">
											{desc}
										</p>
									</div>
								))}
							</div>

							<div className="animate-fade-up-d2 mt-10 text-center">
								{auth.isAuthenticated && orgAppPath ? (
									<Button
										to={orgAppPath}
										variant="onDark"
										size="lg"
										className="shadow-md"
									>
										{t("landing.heroCtaOrgOverview")}
									</Button>
								) : orgsLoading ? (
									<Skeleton className="mx-auto h-13 w-56 rounded-xl" />
								) : orgsFailed ? null : (
									<Button
										type="button"
										onClick={handleOrgCta}
										variant="onDark"
										size="lg"
										className="shadow-md"
									>
										{t("landing.heroCtaOrg")}
									</Button>
								)}
							</div>
						</div>
					</div>

					<svg
						aria-hidden="true"
						viewBox="0 0 1440 60"
						preserveAspectRatio="none"
						className="block h-8 w-full text-brand-50 sm:h-12"
					>
						<path d={WAVE_PATH} fill="currentColor" />
					</svg>
				</div>
			</section>

			<section
				aria-labelledby={missionTitleId}
				className="relative left-1/2 mb-20 w-screen -translate-x-1/2"
			>
				<div className="bg-brand-50 py-10 sm:py-14">
					<div className="mx-auto grid max-w-page items-center gap-10 px-4 sm:px-6 lg:grid-cols-5 lg:gap-16 lg:px-8">
						<div className="animate-fade-up relative mx-auto w-full max-w-64 lg:col-span-2 lg:max-w-none">
							<div
								aria-hidden="true"
								style={{ clipPath: "url(#hero-stone-3)" }}
								className="absolute -inset-6 bg-accent-400/50"
							/>
							<img
								src="/images/founder.png"
								alt=""
								className="relative aspect-square w-full rounded-card object-cover shadow-raised"
							/>
						</div>

						<div className="animate-fade-up-d1 text-center lg:col-span-3 lg:text-left">
							<p className="mb-3 text-xs font-semibold tracking-widest text-brand-700 uppercase">
								{t("landing.missionLabel")}
							</p>
							<h2
								id={missionTitleId}
								className="font-display text-3xl font-bold text-gray-900 sm:text-4xl"
							>
								{t("landing.missionTitle")}
							</h2>
							<p className="mt-4 text-base leading-relaxed text-gray-700">
								{t("landing.missionText")}
							</p>
							<p className="mt-4 text-sm font-semibold text-brand-700">
								{t("landing.missionAuthor")}
							</p>
							<div className="mt-8">
								<Button href="/opportunities" size="lg" className="shadow-md">
									{t("landing.missionCta")}
								</Button>
							</div>
						</div>
					</div>
				</div>

				<svg
					aria-hidden="true"
					viewBox="0 0 1440 60"
					preserveAspectRatio="none"
					className="block h-8 w-full rotate-180 text-brand-50 sm:h-12"
				>
					<path d={WAVE_PATH} fill="currentColor" />
				</svg>
			</section>

			<section aria-labelledby={faqTitleId} className="mb-10">
				<div className="animate-fade-up mx-auto max-w-3xl text-center">
					<p className="mb-3 text-xs font-semibold tracking-widest text-brand-700 uppercase">
						{t("landing.faqLabel")}
					</p>
					<h2
						id={faqTitleId}
						className="font-display text-3xl font-bold text-gray-900 sm:text-4xl"
					>
						{t("landing.faqTitle")}
					</h2>
				</div>

				<FaqAccordion
					items={faqItems}
					className="animate-fade-up-d1 mx-auto mt-10 max-w-3xl"
				/>

				<p className="animate-fade-up-d1 mt-6 text-center text-sm text-gray-600">
					<Link
						to="/help"
						className="font-medium text-brand-700 underline-offset-2 hover:underline"
					>
						{t("landing.faqMoreLink")}
					</Link>
				</p>
			</section>

			{showOrgModal && (
				<Suspense
					fallback={
						<ModalLoadingFallback onClose={() => setShowOrgModal(false)} />
					}
				>
					<CreateOrganizationModal
						onClose={() => setShowOrgModal(false)}
						onSuccess={handleOrgCreated}
					/>
				</Suspense>
			)}
		</>
	);
}
