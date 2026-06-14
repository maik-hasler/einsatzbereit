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
		<div className="max-w-2xl">
			<h1 className="mb-2 text-2xl font-bold text-gray-900">{profile.name}</h1>

			{profile.description && (
				<p className="mb-6 text-gray-700">{profile.description}</p>
			)}

			<div className="mb-6 space-y-2 text-sm text-gray-600">
				{profile.contactEmail && (
					<p>
						<span className="font-medium">{t("orgProfile.email")} </span>
						<a
							href={`mailto:${profile.contactEmail}`}
							className="text-blue-600 hover:underline"
						>
							{profile.contactEmail}
						</a>
					</p>
				)}
				{profile.contactPhone && (
					<p>
						<span className="font-medium">{t("orgProfile.phone")} </span>
						<a
							href={`tel:${profile.contactPhone}`}
							className="text-blue-600 hover:underline"
						>
							{profile.contactPhone}
						</a>
					</p>
				)}
				{profile.website && (
					<p>
						<span className="font-medium">{t("orgProfile.website")} </span>
						<a
							href={profile.website}
							target="_blank"
							rel="noopener noreferrer"
							className="text-blue-600 hover:underline"
						>
							{profile.website}
						</a>
					</p>
				)}
				{profile.address && (
					<p>
						<span className="font-medium">{t("orgProfile.address")} </span>
						{profile.address.street} {profile.address.houseNumber},{" "}
						{profile.address.zipCode} {profile.address.city}
					</p>
				)}
			</div>

			<h2 className="mb-3 text-lg font-semibold text-gray-900">
				{t("orgProfile.currentNeeds")}
			</h2>

			{profile.openOpportunities.length === 0 ? (
				<p className="text-gray-500">{t("orgProfile.noOpportunities")}</p>
			) : (
				<ul className="space-y-3">
					{profile.openOpportunities.map((opp) => (
						<li
							key={opp.id}
							className="rounded border p-4 hover:bg-gray-50 transition-colors"
						>
							<Link to={`/volunteer-opportunities/${opp.id}`} className="block">
								<strong className="block text-sm font-medium text-gray-900">
									{opp.title}
								</strong>
								<p className="mt-1 text-sm text-gray-600">{opp.description}</p>
								<div className="mt-2 flex flex-wrap gap-2">
									<span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700">
										{formatOccurrence(opp.occurrence, t)}
									</span>
									<span className="rounded-full bg-blue-50 px-2 py-0.5 text-xs text-blue-700">
										{formatParticipationType(opp.participationType, t)}
									</span>
									{opp.isRemote ? (
										<span className="rounded-full bg-green-50 px-2 py-0.5 text-xs text-green-700">
											{t("opportunities.remote")}
										</span>
									) : opp.street ? (
										<span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700">
											{opp.street} {opp.houseNumber}, {opp.zipCode} {opp.city}
										</span>
									) : null}
								</div>
							</Link>
						</li>
					))}
				</ul>
			)}
		</div>
	);
}
