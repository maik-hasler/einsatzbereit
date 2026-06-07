import { useId } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import VolunteerOpportunitiesList from "../components/VolunteerOpportunitiesList";
import { usePageTitle } from "../hooks/usePageTitle";

// ── Icons ────────────────────────────────────────────────────────────────────

function BrowseIcon() {
	return (
		<svg
			className="h-6 w-6"
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="1.5"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z"
			/>
		</svg>
	);
}

function HandRaiseIcon() {
	return (
		<svg
			className="h-6 w-6"
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="1.5"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M10.05 4.575a1.575 1.575 0 1 0-3.15 0v3m3.15-3v-1.5a1.575 1.575 0 0 1 3.15 0v1.5m-3.15 0 .075 5.925m3.075.75V4.575m0 0a1.575 1.575 0 0 1 3.15 0V15M6.9 7.575a1.575 1.575 0 1 0-3.15 0v8.175a6.75 6.75 0 0 0 6.75 6.75h2.018a5.25 5.25 0 0 0 3.712-1.538l1.732-1.732a5.25 5.25 0 0 0 1.538-3.712l.003-2.024a.668.668 0 0 1 .198-.471 1.575 1.575 0 1 0-2.228-2.228 3.818 3.818 0 0 0-1.12 2.687M6.9 7.575V12m6.27 4.318A4.49 4.49 0 0 1 16.35 15m.002 0h-.002"
			/>
		</svg>
	);
}

function SparklesIcon() {
	return (
		<svg
			className="h-6 w-6"
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="1.5"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M9.813 15.904 9 18.75l-.813-2.846a4.5 4.5 0 0 0-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 0 0 3.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 0 0 3.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 0 0-3.09 3.09ZM18.259 8.715 18 9.75l-.259-1.035a3.375 3.375 0 0 0-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 0 0 2.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 0 0 2.456 2.456L21.75 6l-1.035.259a3.375 3.375 0 0 0-2.456 2.456ZM16.894 20.567 16.5 21.75l-.394-1.183a2.25 2.25 0 0 0-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 0 0 1.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 0 0 1.423 1.423l1.183.394-1.183.394a2.25 2.25 0 0 0-1.423 1.423Z"
			/>
		</svg>
	);
}

function UsersGroupIcon() {
	return (
		<svg
			className="h-7 w-7"
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="1.5"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z"
			/>
		</svg>
	);
}

function BuildingOfficeIcon() {
	return (
		<svg
			className="h-7 w-7"
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="1.5"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M3.75 21h16.5M4.5 3h15M5.25 3v18m13.5-18v18M9 6.75h1.5m-1.5 3h1.5m-1.5 3h1.5m3-6H15m-1.5 3H15m-1.5 3H15M9 21v-3.375c0-.621.504-1.125 1.125-1.125h3.75c.621 0 1.125.504 1.125 1.125V21"
			/>
		</svg>
	);
}

function CheckIcon() {
	return (
		<svg
			className="mt-0.5 h-5 w-5 shrink-0 text-brand-500"
			viewBox="0 0 20 20"
			fill="currentColor"
			aria-hidden="true"
		>
			<path
				fillRule="evenodd"
				d="M16.704 4.153a.75.75 0 0 1 .143 1.052l-8 10.5a.75.75 0 0 1-1.127.075l-4.5-4.5a.75.75 0 0 1 1.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 0 1 1.05-.143Z"
				clipRule="evenodd"
			/>
		</svg>
	);
}

function ArrowRightIcon() {
	return (
		<svg
			className="h-4 w-4"
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth="2"
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M13.5 4.5 21 12m0 0-7.5 7.5M21 12H3"
			/>
		</svg>
	);
}

// ── Page ─────────────────────────────────────────────────────────────────────

export default function HomePage() {
	const auth = useAuth();
	const { t } = useTranslation();
	usePageTitle();

	const heroTitleId = useId();
	const howItWorksTitleId = useId();
	const missionTitleId = useId();
	const volunteerTitleId = useId();
	const ngoTitleId = useId();

	const roles = (
		Array.isArray(auth.user?.profile?.roles) ? auth.user?.profile?.roles : []
	) as string[];
	const canCreateOpportunity =
		auth.isAuthenticated && roles.includes("organisator");

	const steps = [
		{
			step: 1,
			icon: <BrowseIcon />,
			title: t("landing.step1Title"),
			desc: t("landing.step1Desc"),
		},
		{
			step: 2,
			icon: <HandRaiseIcon />,
			title: t("landing.step2Title"),
			desc: t("landing.step2Desc"),
		},
		{
			step: 3,
			icon: <SparklesIcon />,
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
				className="-mx-4 -mt-16 mb-20 bg-brand-800 px-4 py-24 sm:-mx-6 sm:px-6 lg:-mx-8 lg:px-8"
			>
				<div className="mx-auto max-w-3xl text-center">
					<div className="mb-8 inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-4 py-1.5 text-sm font-medium text-brand-100">
						<span
							className="h-1.5 w-1.5 rounded-full bg-accent-400"
							aria-hidden="true"
						/>
						{t("landing.heroTagline")}
					</div>
					<h1
						id={heroTitleId}
						className="mb-5 text-5xl font-bold tracking-tight text-white sm:text-6xl"
					>
						{t("landing.heroTitle")}
					</h1>
					<p className="mb-10 text-xl leading-relaxed text-brand-100">
						{t("landing.heroSubtitle")}
					</p>
					<div className="flex flex-col items-center gap-4 sm:flex-row sm:justify-center">
						<Link
							to="#opportunities"
							className="rounded-xl bg-white px-8 py-3.5 text-base font-semibold text-brand-800 shadow-lg transition-colors hover:bg-brand-50"
						>
							{t("landing.heroCta")}
						</Link>
						{!auth.isAuthenticated && (
							<button
								type="button"
								onClick={() => void auth.signinRedirect()}
								className="rounded-xl border border-white/50 px-8 py-3.5 text-base font-semibold text-white transition-colors hover:border-white hover:bg-brand-700"
							>
								{t("landing.heroCtaOrg")}
							</button>
						)}
					</div>
					<div className="mt-12 grid grid-cols-3 gap-6 border-t border-white/10 pt-10">
						{stats.map(({ id, value, label }) => (
							<div key={id} className="text-center">
								<div className="text-2xl font-bold text-white sm:text-3xl">
									{value}
								</div>
								<div className="mt-1 text-sm text-brand-200">{label}</div>
							</div>
						))}
					</div>
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
						<div key={step} className="flex flex-col items-center text-center">
							<div className="relative mb-5">
								<span
									className="select-none text-8xl font-black leading-none text-brand-100"
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
							<p className="text-sm leading-relaxed text-gray-500">{desc}</p>
						</div>
					))}
				</div>
			</section>

			{/* Mission */}
			<section aria-labelledby={missionTitleId} className="mb-20">
				<div className="overflow-hidden rounded-2xl bg-brand-800 px-8 py-12 text-center sm:px-16">
					<p className="mb-3 text-xs font-semibold uppercase tracking-widest text-brand-200">
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
				</div>
			</section>

			{/* For volunteers + organisations */}
			<section
				aria-label={t("landing.benefitsLabel")}
				className="mb-20 grid gap-6 sm:grid-cols-2"
			>
				{/* Volunteers */}
				<div className="overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm">
					<div className="border-b border-brand-100 bg-brand-50 px-6 py-5">
						<div className="mb-3 inline-flex h-12 w-12 items-center justify-center rounded-xl bg-brand-100 text-brand-600">
							<UsersGroupIcon />
						</div>
						<h2
							id={volunteerTitleId}
							className="text-xl font-semibold text-gray-900"
						>
							{t("landing.volunteerTitle")}
						</h2>
						<p className="mt-1 text-sm text-gray-600">
							{t("landing.volunteerSubtitle")}
						</p>
					</div>
					<div className="px-6 py-5">
						<ul
							aria-labelledby={volunteerTitleId}
							className="space-y-3 text-sm text-gray-700"
						>
							{(
								[
									"volunteerBenefit1",
									"volunteerBenefit2",
									"volunteerBenefit3",
								] as const
							).map((key) => (
								<li key={key} className="flex gap-3">
									<CheckIcon />
									<span>{t(`landing.${key}`)}</span>
								</li>
							))}
						</ul>
						<Link
							to="#opportunities"
							className="mt-6 inline-flex items-center gap-2 rounded-lg bg-brand-700 px-5 py-2.5 text-sm font-medium text-white transition-colors hover:bg-brand-800"
						>
							{t("landing.volunteerCta")}
							<ArrowRightIcon />
						</Link>
					</div>
				</div>

				{/* Organisations */}
				<div className="overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm">
					<div className="border-b border-gray-100 bg-gray-50 px-6 py-5">
						<div className="mb-3 flex items-start justify-between">
							<div className="inline-flex h-12 w-12 items-center justify-center rounded-xl bg-gray-200 text-gray-600">
								<BuildingOfficeIcon />
							</div>
							<span className="rounded-full bg-brand-50 px-2.5 py-1 text-xs font-semibold text-brand-700">
								{t("landing.openSourceBadge")}
							</span>
						</div>
						<h2 id={ngoTitleId} className="text-xl font-semibold text-gray-900">
							{t("landing.ngoTitle")}
						</h2>
						<p className="mt-1 text-sm text-gray-600">
							{t("landing.ngoSubtitle")}
						</p>
					</div>
					<div className="px-6 py-5">
						<ul
							aria-labelledby={ngoTitleId}
							className="space-y-3 text-sm text-gray-700"
						>
							{(["ngoBenefit1", "ngoBenefit2", "ngoBenefit3"] as const).map(
								(key) => (
									<li key={key} className="flex gap-3">
										<CheckIcon />
										<span>{t(`landing.${key}`)}</span>
									</li>
								),
							)}
						</ul>
						<button
							type="button"
							onClick={() => void auth.signinRedirect()}
							className="mt-6 inline-flex items-center gap-2 rounded-lg border border-gray-800 px-5 py-2.5 text-sm font-medium text-gray-800 transition-colors hover:bg-gray-800 hover:text-white"
						>
							{t("landing.ngoCta")}
							<ArrowRightIcon />
						</button>
					</div>
				</div>
			</section>

			{/* Opportunity list */}
			<div id="opportunities">
				<VolunteerOpportunitiesList
					canCreateOpportunity={canCreateOpportunity}
				/>
			</div>
		</>
	);
}
