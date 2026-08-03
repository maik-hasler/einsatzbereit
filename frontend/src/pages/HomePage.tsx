import { useEffect, useId, useState } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useSearchParams } from "react-router";
import type { OrganizationSummaryDto } from "../client/api-client";
import VolunteerOpportunitiesList from "../components/VolunteerOpportunitiesList/VolunteerOpportunitiesList";
import CreateOrganizationModal from "../components/CreateOrganizationModal";
import Button from "../components/Button";
import Skeleton from "../components/Skeleton";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import { useSharedOrgFetch } from "../hooks/useSharedOrgFetch";
import { signinLocaleArgs } from "../lib/authLocale";
import { signinRedirectForRegistration } from "../lib/keycloakRegistration";
import { getActiveOrgId, resolveOrgAppPath } from "../lib/activeOrg";
import {
	BuildingOfficeIcon,
	CheckIcon,
	ClockIcon,
	HandRaisedIcon,
	MagnifyingGlassIcon,
	MapPinIcon,
	SparklesIcon,
} from "../components/icons";

// ── Page ─────────────────────────────────────────────────────────────────────

export default function HomePage() {
	const auth = useAuth();
	const api = useApiClient();
	const navigate = useNavigate();
	const { t } = useTranslation();
	usePageTitle();

	const heroTitleId = useId();
	const howItWorksTitleId = useId();
	const missionTitleId = useId();
	const orgsTeaserTitleId = useId();

	const [showCreateOrgModal, setShowCreateOrgModal] = useState(false);
	const [searchParams, setSearchParams] = useSearchParams();

	// Shared with Header, which independently needs the same top-level
	// organization list on the same mount (#1396) - see useSharedOrgFetch.
	const [orgsData, , orgsError] = useSharedOrgFetch<OrganizationSummaryDto[]>(
		`organizations:${auth.isAuthenticated}`,
		() => (auth.isAuthenticated ? api.getOrganizations() : Promise.resolve([])),
	);
	const orgs = auth.isAuthenticated ? (orgsData ?? []) : [];
	// useSharedOrgFetch leaves orgsData null both while the fetch is still in
	// flight and after it rejects (see its error branch) - without checking
	// for that, both looked identical to "this user genuinely organizes
	// nothing", and a signed-in organizer could see the "create an
	// organisation" CTA (and, if clicked, create a duplicate org) while their
	// real org list was still loading or had failed to load. Split the two:
	// a pulsing skeleton implies work in progress, which is only honest while
	// still loading - once it's failed there's nothing further to wait for
	// (no retry is wired up here, see HomePageOrgCtaTests.cs's regression
	// test), so that slot renders nothing instead of a permanently animating
	// placeholder.
	const orgsLoading = auth.isAuthenticated && orgsData === null && !orgsError;
	const orgsFailed = auth.isAuthenticated && orgsData === null && !!orgsError;

	const orgAppPath = resolveOrgAppPath(orgs, getActiveOrgId());

	useEffect(() => {
		if (searchParams.get("createOrg") === "1" && auth.isAuthenticated) {
			setShowCreateOrgModal(true);
			const next = new URLSearchParams(searchParams);
			next.delete("createOrg");
			setSearchParams(next, { replace: true });
		}
	}, [searchParams, auth.isAuthenticated, setSearchParams]);

	function handleOrgCta() {
		if (auth.isAuthenticated) {
			setShowCreateOrgModal(true);
		} else {
			void signinRedirectForRegistration({
				...signinLocaleArgs(),
				state: { returnTo: "/?createOrg=1" },
			});
		}
	}

	const steps = [
		{
			step: 1,
			icon: <MagnifyingGlassIcon className="h-6 w-6" />,
			title: t("landing.step1Title"),
			desc: t("landing.step1Desc"),
		},
		{
			step: 2,
			icon: <HandRaisedIcon className="h-6 w-6" />,
			title: t("landing.step2Title"),
			desc: t("landing.step2Desc"),
		},
		{
			step: 3,
			icon: <SparklesIcon className="h-6 w-6" />,
			title: t("landing.step3Title"),
			desc: t("landing.step3Desc"),
		},
	];

	const stats = [
		{
			id: "time",
			value: t("landing.heroStat1Value"),
			label: t("landing.heroStat1Label"),
		},
		{
			id: "cost",
			value: t("landing.heroStat2Value"),
			label: t("landing.heroStat2Label"),
		},
		{
			id: "oss",
			value: t("landing.heroStat3Value"),
			label: t("landing.heroStat3Label"),
		},
	];

	return (
		<>
			{/* Hero */}
			<section
				aria-labelledby={heroTitleId}
				className="full-bleed relative -mt-[var(--header-offset)] mb-20 overflow-hidden bg-brand-800"
			>
				{/* Decorative glow blobs */}
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

				{/* Content grid */}
				<div className="relative mx-auto max-w-7xl px-4 pt-22 pb-16 sm:px-6 sm:pt-28 sm:pb-20 lg:px-8 lg:pt-32 xl:grid xl:grid-cols-[200px_1fr_200px] xl:items-center xl:gap-8">
					{/* Left feature cards - xl+ only */}
					<div className="hidden xl:flex xl:flex-col xl:gap-3">
						<div className="flex items-center gap-3 rounded-card border border-white/15 bg-white/8 px-4 py-3 backdrop-blur-sm">
							<div
								aria-hidden="true"
								className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white/15"
							>
								<ClockIcon className="h-4 w-4 text-white" />
							</div>
							<div>
								<p className="text-sm font-semibold text-white">
									{t("landing.heroLeftCard1Label")}
								</p>
								<p className="text-xs text-brand-200">
									{t("landing.heroLeftCard1Desc")}
								</p>
							</div>
						</div>
						<div className="ml-4 flex items-center gap-3 rounded-card border border-white/15 bg-white/8 px-4 py-3 backdrop-blur-sm">
							<div
								aria-hidden="true"
								className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white/15"
							>
								<MapPinIcon className="h-4 w-4 text-white" />
							</div>
							<div>
								<p className="text-sm font-semibold text-white">
									{t("landing.heroLeftCard2Label")}
								</p>
								<p className="text-xs text-brand-200">
									{t("landing.heroLeftCard2Desc")}
								</p>
							</div>
						</div>
						<div className="flex items-center gap-3 rounded-card border border-white/15 bg-white/8 px-4 py-3 backdrop-blur-sm">
							<div
								aria-hidden="true"
								className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white/15"
							>
								<CheckIcon className="h-4 w-4 text-white" />
							</div>
							<div>
								<p className="text-sm font-semibold text-white">
									{t("landing.heroLeftCard3Label")}
								</p>
								<p className="text-xs text-brand-200">
									{t("landing.heroLeftCard3Desc")}
								</p>
							</div>
						</div>
					</div>

					{/* Center hero content */}
					<div className="text-center">
						<div className="animate-fade-up mb-6 inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-3 py-1 text-xs font-medium text-brand-100 sm:mb-8 sm:px-4 sm:py-1.5 sm:text-sm">
							<span
								className="h-1.5 w-1.5 shrink-0 rounded-full bg-accent-400"
								aria-hidden="true"
							/>
							{t("landing.heroTagline")}
						</div>
						<h1
							id={heroTitleId}
							className="animate-fade-up-d1 mb-4 text-3xl font-bold tracking-tight text-white sm:mb-5 sm:text-5xl lg:text-6xl"
						>
							{t("landing.heroTitle")}
						</h1>
						<p className="animate-fade-up-d2 mb-7 text-sm leading-relaxed text-brand-100 sm:mb-10 sm:text-xl">
							{t("landing.heroSubtitle")}
						</p>
						<div className="animate-fade-up-d3 flex flex-col items-center gap-3 sm:flex-row sm:justify-center sm:gap-4">
							<a
								href="#opportunities"
								className="w-full rounded-xl bg-white px-8 py-3 text-base font-semibold text-brand-800 shadow-lg transition-colors hover:bg-brand-50 sm:w-auto sm:py-3.5"
							>
								{t("landing.heroCta")}
							</a>
							{auth.isAuthenticated && orgAppPath ? (
								<Link
									to={orgAppPath}
									className="w-full rounded-xl border border-white/50 px-8 py-3 text-base font-semibold text-white transition-colors hover:border-white hover:bg-brand-700 sm:w-auto sm:py-3.5"
								>
									{t("landing.heroCtaOrgOverview")}
								</Link>
							) : orgsLoading ? (
								<Skeleton className="h-13 w-full rounded-xl sm:w-56" />
							) : orgsFailed ? null : (
								<button
									type="button"
									onClick={handleOrgCta}
									className="w-full rounded-xl border border-white/50 px-8 py-3 text-base font-semibold text-white transition-colors hover:border-white hover:bg-brand-700 sm:w-auto sm:py-3.5"
								>
									{t("landing.heroCtaOrg")}
								</button>
							)}
						</div>
						<div className="animate-fade-up-d4 mt-8 hidden grid-cols-3 gap-6 border-t border-white/10 pt-8 sm:mt-12 sm:grid sm:pt-10">
							{stats.map(({ id, value, label }) => (
								<div key={id} className="text-center">
									<div className="text-2xl font-bold text-white sm:text-3xl">
										{value}
									</div>
									<div className="mt-1 text-sm text-brand-100">{label}</div>
								</div>
							))}
						</div>
					</div>

					{/* Right org card - xl+ only */}
					<div className="hidden xl:flex xl:flex-col xl:gap-3">
						<div className="rounded-card border border-white/15 bg-white/8 p-5 backdrop-blur-sm">
							<div className="mb-3 flex items-center gap-2">
								<BuildingOfficeIcon className="h-4 w-4 text-brand-200" />
								<p className="text-sm font-semibold text-white">
									{t("landing.heroRightCardTitle")}
								</p>
							</div>
							<ul className="space-y-2.5">
								{(
									[
										"landing.heroRightCard1",
										"landing.heroRightCard2",
										"landing.heroRightCard3",
									] as const
								).map((key) => (
									<li key={key} className="flex items-center gap-2 text-sm">
										<span
											className="h-1.5 w-1.5 shrink-0 rounded-full bg-accent-400"
											aria-hidden="true"
										/>
										<span className="text-brand-100">{t(key)}</span>
									</li>
								))}
							</ul>
						</div>
						<div className="mr-4 flex items-center gap-2.5 rounded-card border border-white/15 bg-white/8 px-4 py-3 backdrop-blur-sm">
							<span
								className="h-2 w-2 shrink-0 animate-pulse rounded-full bg-accent-400"
								aria-hidden="true"
							/>
							<p className="text-sm text-brand-100">
								{t("landing.heroRightCardActive")}
							</p>
						</div>
					</div>
				</div>

				{/* Wave bottom edge */}
				<div aria-hidden="true" className="absolute right-0 bottom-0 left-0">
					<svg
						viewBox="0 0 1440 56"
						preserveAspectRatio="none"
						className="block h-14 w-full fill-white"
					>
						<path d="M0,28 C360,56 1080,0 1440,28 L1440,56 L0,56 Z" />
					</svg>
				</div>
			</section>

			{/* How it works */}
			<section aria-labelledby={howItWorksTitleId} className="mb-20">
				<h2
					id={howItWorksTitleId}
					className="mb-12 text-center text-2xl font-bold text-gray-900"
				>
					{t("landing.howItWorksTitle")}
				</h2>
				<div className="grid gap-10 sm:grid-cols-3">
					{steps.map(({ step, icon, title, desc }) => (
						<div
							key={step}
							className={`flex flex-col items-center text-center ${
								step === 1
									? "animate-fade-up"
									: step === 2
										? "animate-fade-up-d1"
										: "animate-fade-up-d2"
							}`}
						>
							<div className="relative mb-5">
								<span
									className="text-8xl leading-none font-black text-brand-100 select-none"
									aria-hidden="true"
								>
									{"0" + step}
								</span>
								<div className="absolute inset-0 flex items-center justify-center">
									<div className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-600 text-white shadow-md">
										{icon}
									</div>
								</div>
							</div>
							<h3 className="mb-2 text-base font-semibold text-gray-900">
								{title}
							</h3>
							<p className="text-sm leading-relaxed text-gray-600">{desc}</p>
						</div>
					))}
				</div>
			</section>

			{/* Mission */}
			<section aria-labelledby={missionTitleId} className="mb-20">
				<div className="animate-fade-up overflow-hidden rounded-card bg-brand-800 px-8 py-12 text-center sm:px-16">
					<p className="mb-3 text-xs font-semibold tracking-widest text-brand-200 uppercase">
						{t("landing.missionLabel")}
					</p>
					<h2
						id={missionTitleId}
						className="mb-5 text-2xl font-bold text-white sm:text-3xl"
					>
						{t("landing.missionTitle")}
					</h2>
					<p className="mx-auto max-w-2xl text-base leading-relaxed text-brand-100">
						{t("landing.missionText")}
					</p>
					<a
						href="#opportunities"
						className="mt-8 inline-flex items-center gap-2 rounded-xl bg-white px-7 py-3 text-sm font-semibold text-brand-800 shadow-lg transition-colors hover:bg-brand-50"
					>
						{t("landing.missionCta")}
					</a>
				</div>
			</section>

			{/* Organizations directory teaser */}
			<section aria-labelledby={orgsTeaserTitleId} className="mb-20">
				<div className="animate-fade-up flex flex-col items-center gap-6 rounded-card border border-gray-100 bg-white p-8 text-center shadow-resting sm:flex-row sm:items-center sm:justify-between sm:p-10 sm:text-left">
					<div>
						<h2
							id={orgsTeaserTitleId}
							className="text-xl font-bold text-gray-900 sm:text-2xl"
						>
							{t("landing.orgsTeaserTitle")}
						</h2>
						<p className="mt-2 text-sm text-gray-600 sm:text-base">
							{t("landing.orgsTeaserDesc")}
						</p>
					</div>
					<Button
						to="/organizations"
						data-testid="organizations-teaser-cta"
						size="lg"
						className="shrink-0 shadow-sm"
					>
						{t("landing.orgsTeaserCta")}
					</Button>
				</div>
			</section>

			<div id="opportunities">
				<VolunteerOpportunitiesList />
			</div>

			{showCreateOrgModal && (
				<CreateOrganizationModal
					onClose={() => setShowCreateOrgModal(false)}
					onSuccess={async (org) => {
						setShowCreateOrgModal(false);
						// CreateOrganization grants the "organisator" realm role
						// server-side, but the access token already held by the
						// browser was minted before that grant and doesn't carry
						// it yet - EinsatzbereitOrganisatorPolicy checks that
						// static claim, so the org app shell's very next request
						// (OrgAppLayout's GetOrganizationDetails call) would 403
						// against the stale token before it ever reaches the
						// live per-organization membership check. Best-effort:
						// if silent renewal fails, still navigate - the org app
						// shell's own error state takes over from there.
						try {
							await auth.signinSilent();
						} catch {
							/* see comment above */
						}
						navigate(`/app/${org.id?.value}/dashboard`);
					}}
				/>
			)}
		</>
	);
}
