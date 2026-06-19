import { useEffect, useState } from "react";
import { useParams, Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { PublicOrganizationProfileResponse } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { formatOccurrence, formatParticipationType } from "../lib/format";
import { usePageTitle } from "../hooks/usePageTitle";
import { getApiErrorMessage } from "../lib/apiError";

export default function OrganizationProfilePage() {
	const { organizationId } = useParams<{ organizationId: string }>();
	const api = useApiClient();
	const { t } = useTranslation();

	const [profile, setProfile] =
		useState<PublicOrganizationProfileResponse | null>(null);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);

	usePageTitle(profile?.name ?? t("orgProfile.loading"));

	useEffect(() => {
		if (!organizationId) return;
		api
			.getPublicOrganizationProfile(organizationId)
			.then(setProfile)
			.catch((err) => setError(getApiErrorMessage(err, t("error.serverError"))))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	if (loading)
		return <p className="text-gray-500">{t("orgProfile.loading")}</p>;
	if (error)
		return (
			<p className="text-red-600">
				{t("orgProfile.error", { message: error })}
			</p>
		);
	if (!profile)
		return <p className="text-gray-500">{t("orgProfile.notFound")}</p>;

	return (
		<>
			<div className="mb-6 flex items-center gap-2">
				<h1 className="text-2xl font-bold text-gray-900">{profile.name}</h1>
				{profile.isVerified && (
					<svg
						className="h-6 w-6 shrink-0 text-brand-600"
						viewBox="0 0 20 20"
						fill="currentColor"
						aria-label={t("organizations.verified")}
						role="img"
					>
						<path
							fillRule="evenodd"
							d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm3.857-9.809a.75.75 0 0 0-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 1 0-1.06 1.061l2.5 2.5a.75.75 0 0 0 1.137-.089l4-5.5Z"
							clipRule="evenodd"
						/>
					</svg>
				)}
			</div>

			<div className="max-w-2xl">
				{profile.description && (
					<p className="mb-6 leading-relaxed text-gray-600">
						{profile.description}
					</p>
				)}

				{(profile.contactEmail ||
					profile.contactPhone ||
					profile.website ||
					profile.address) && (
					<div className="mb-6 space-y-2.5 rounded-2xl border border-gray-100 bg-gray-50 px-4 py-4 text-sm text-gray-700">
						{profile.contactEmail && (
							<div className="flex items-center gap-3">
								<svg
									className="h-4 w-4 shrink-0 text-gray-400"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="1.5"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M21.75 6.75v10.5a2.25 2.25 0 0 1-2.25 2.25h-15a2.25 2.25 0 0 1-2.25-2.25V6.75m19.5 0A2.25 2.25 0 0 0 19.5 4.5h-15a2.25 2.25 0 0 0-2.25 2.25m19.5 0v.243a2.25 2.25 0 0 1-1.07 1.916l-7.5 4.615a2.25 2.25 0 0 1-2.36 0L3.32 8.91a2.25 2.25 0 0 1-1.07-1.916V6.75"
									/>
								</svg>
								<a
									href={`mailto:${profile.contactEmail}`}
									className="text-brand-700 transition-colors hover:text-brand-800 hover:underline"
								>
									{profile.contactEmail}
								</a>
							</div>
						)}
						{profile.contactPhone && (
							<div className="flex items-center gap-3">
								<svg
									className="h-4 w-4 shrink-0 text-gray-400"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="1.5"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M2.25 6.75c0 8.284 6.716 15 15 15h2.25a2.25 2.25 0 0 0 2.25-2.25v-1.372c0-.516-.351-.966-.852-1.091l-4.423-1.106c-.44-.11-.902.055-1.173.417l-.97 1.293c-.282.376-.769.542-1.21.38a12.035 12.035 0 0 1-7.143-7.143c-.162-.441.004-.928.38-1.21l1.293-.97c.363-.271.527-.734.417-1.173L6.963 3.102a1.125 1.125 0 0 0-1.091-.852H4.5A2.25 2.25 0 0 0 2.25 4.5v2.25Z"
									/>
								</svg>
								<a
									href={`tel:${profile.contactPhone}`}
									className="text-brand-700 transition-colors hover:text-brand-800 hover:underline"
								>
									{profile.contactPhone}
								</a>
							</div>
						)}
						{profile.website && (
							<div className="flex items-center gap-3">
								<svg
									className="h-4 w-4 shrink-0 text-gray-400"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="1.5"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M12 21a9.004 9.004 0 0 0 8.716-6.747M12 21a9.004 9.004 0 0 1-8.716-6.747M12 21c2.485 0 4.5-4.03 4.5-9S14.485 3 12 3m0 18c-2.485 0-4.5-4.03-4.5-9S9.515 3 12 3m0 0a8.997 8.997 0 0 1 7.843 4.582M12 3a8.997 8.997 0 0 0-7.843 4.582m15.686 0A11.953 11.953 0 0 1 12 10.5c-2.998 0-5.74-1.1-7.843-2.918m15.686 0A8.959 8.959 0 0 1 21 12c0 .778-.099 1.533-.284 2.253m0 0A17.919 17.919 0 0 1 12 16.5c-3.162 0-6.133-.815-8.716-2.247m0 0A9.015 9.015 0 0 1 3 12c0-1.605.42-3.113 1.157-4.418"
									/>
								</svg>
								<a
									href={profile.website}
									target="_blank"
									rel="noopener noreferrer"
									className="text-brand-700 transition-colors hover:text-brand-800 hover:underline"
								>
									{profile.website}
								</a>
							</div>
						)}
						{profile.address && (
							<div className="flex items-center gap-3">
								<svg
									className="h-4 w-4 shrink-0 text-gray-400"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="1.5"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M15 10.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
									/>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1 1 15 0Z"
									/>
								</svg>
								<span>
									{profile.address.street} {profile.address.houseNumber},{" "}
									{profile.address.zipCode} {profile.address.city}
								</span>
							</div>
						)}
					</div>
				)}

				<h2 className="mb-3 text-xs font-semibold uppercase tracking-wider text-gray-400">
					{t("orgProfile.currentNeeds")}
				</h2>

				{profile.openOpportunities.length === 0 ? (
					<p className="text-gray-500">{t("orgProfile.noOpportunities")}</p>
				) : (
					<ul className="space-y-3">
						{profile.openOpportunities.map((opp) => (
							<li
								key={opp.id}
								className="relative rounded-xl border border-gray-100 bg-white p-4 shadow-sm transition-shadow hover:shadow-md"
							>
								<Link
									to={`/volunteer-opportunities/${opp.id}`}
									className="absolute inset-0 rounded-xl"
									aria-label={opp.title}
								/>
								<strong className="block text-sm font-semibold text-gray-900">
									{opp.title}
								</strong>
								{opp.description && (
									<p className="mt-1 line-clamp-2 text-sm text-gray-500">
										{opp.description}
									</p>
								)}
								<div className="mt-2 flex flex-wrap gap-2">
									<span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600">
										{formatOccurrence(opp.occurrence, t)}
									</span>
									<span className="rounded-full bg-brand-50 px-2 py-0.5 text-xs text-brand-700">
										{formatParticipationType(opp.participationType, t)}
									</span>
									{opp.isRemote ? (
										<span className="rounded-full bg-green-50 px-2 py-0.5 text-xs text-green-700">
											{t("opportunities.remote")}
										</span>
									) : opp.street ? (
										<span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600">
											{opp.street} {opp.houseNumber}, {opp.zipCode} {opp.city}
										</span>
									) : null}
								</div>
							</li>
						))}
					</ul>
				)}
			</div>
		</>
	);
}
