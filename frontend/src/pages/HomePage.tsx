import { useId } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
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

// ── Page ─────────────────────────────────────────────────────────────────────

export default function HomePage() {
	const auth = useAuth();
	const { t } = useTranslation();
	usePageTitle();

	const heroTitleId = useId();
	const howItWorksTitleId = useId();
	const missionTitleId = useId();

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
					<div className="animate-fade-up mb-8 inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-4 py-1.5 text-sm font-medium text-brand-100">
						<span
							className="h-1.5 w-1.5 rounded-full bg-accent-400"
							aria-hidden="true"
						/>
						{t("landing.heroTagline")}
					</div>
					<h1
						id={heroTitleId}
						className="animate-fade-up-d1 mb-5 text-5xl font-bold tracking-tight text-white sm:text-6xl"
					>
						{t("landing.heroTitle")}
					</h1>
					<p className="animate-fade-up-d2 mb-10 text-xl leading-relaxed text-brand-100">
						{t("landing.heroSubtitle")}
					</p>
					<div className="animate-fade-up-d3 flex flex-col items-center gap-4 sm:flex-row sm:justify-center">
						<a
							href="#opportunities"
							className="rounded-xl bg-white px-8 py-3.5 text-base font-semibold text-brand-800 shadow-lg transition-colors hover:bg-brand-50"
						>
							{t("landing.heroCta")}
						</a>
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
					<div className="animate-fade-up-d4 mt-12 grid grid-cols-3 gap-6 border-t border-white/10 pt-10">
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
				<div className="animate-fade-up overflow-hidden rounded-2xl bg-brand-800 px-8 py-12 text-center sm:px-16">
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
					<a
						href="#opportunities"
						className="mt-8 inline-flex items-center gap-2 rounded-xl bg-white px-7 py-3 text-sm font-semibold text-brand-800 shadow-lg transition-colors hover:bg-brand-50"
					>
						{t("landing.missionCta")}
					</a>
				</div>
			</section>

			<div id="opportunities">
				<VolunteerOpportunitiesList
					canCreateOpportunity={canCreateOpportunity}
				/>
			</div>
		</>
	);
}
