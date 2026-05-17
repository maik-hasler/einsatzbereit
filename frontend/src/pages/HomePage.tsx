import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import VolunteerOpportunitiesList from "../components/VolunteerOpportunitiesList";

function CheckIcon() {
	return (
		<svg
			className="mt-0.5 h-5 w-5 flex-shrink-0 text-brand-500"
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

export default function HomePage() {
	const auth = useAuth();
	const { t } = useTranslation();
	const roles = (
		Array.isArray(auth.user?.profile?.roles) ? auth.user?.profile?.roles : []
	) as string[];
	const canCreateOpportunity =
		auth.isAuthenticated && roles.includes("organisator");

	return (
		<>
			{/* Hero */}
			<section className="-mx-4 -mt-16 mb-16 bg-brand-800 px-4 py-20 sm:-mx-6 sm:px-6 lg:-mx-8 lg:px-8">
				<div className="mx-auto max-w-3xl text-center">
					<h1 className="mb-4 text-4xl font-bold tracking-tight text-white sm:text-5xl">
						{t("landing.heroTitle")}
					</h1>
					<p className="mb-8 text-lg text-brand-100">
						{t("landing.heroSubtitle")}
					</p>
					<div className="flex flex-col items-center gap-3 sm:flex-row sm:justify-center">
						<Link
							to="#opportunities"
							className="rounded-lg bg-white px-6 py-3 text-sm font-semibold text-brand-800 shadow hover:bg-gray-100"
						>
							{t("landing.heroCta")}
						</Link>
						{!auth.isAuthenticated && (
							<button
								type="button"
								onClick={() => void auth.signinRedirect()}
								className="rounded-lg border border-white px-6 py-3 text-sm font-semibold text-white hover:bg-brand-700"
							>
								{t("landing.heroCtaOrg")}
							</button>
						)}
					</div>
				</div>
			</section>

			{/* Benefits grid */}
			<section className="mb-16 grid gap-8 sm:grid-cols-2">
				{/* For volunteers */}
				<div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
					<h2 className="mb-2 text-xl font-semibold text-gray-900">
						{t("landing.volunteerTitle")}
					</h2>
					<p className="mb-5 text-sm text-gray-500">
						{t("landing.volunteerSubtitle")}
					</p>
					<ul className="space-y-3 text-sm text-gray-700">
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
						className="mt-6 inline-block rounded-md bg-brand-800 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
					>
						{t("landing.volunteerCta")}
					</Link>
				</div>

				{/* For organisations */}
				<div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
					<h2 className="mb-2 text-xl font-semibold text-gray-900">
						{t("landing.ngoTitle")}
					</h2>
					<p className="mb-5 text-sm text-gray-500">
						{t("landing.ngoSubtitle")}
					</p>
					<ul className="space-y-3 text-sm text-gray-700">
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
						onClick={() =>
							auth.isAuthenticated ? undefined : void auth.signinRedirect()
						}
						className="mt-6 inline-block rounded-md border border-brand-800 px-4 py-2 text-sm font-medium text-brand-800 hover:bg-brand-50"
					>
						{t("landing.ngoCta")}
					</button>
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
