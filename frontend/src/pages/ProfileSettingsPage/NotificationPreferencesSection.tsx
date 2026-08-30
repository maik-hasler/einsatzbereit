import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { NotificationPreferencesResponse } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { useMyOrganizations } from "../../hooks/useMyOrganizations";
import { getApiErrorMessage } from "../../lib/apiError";
import { cardClass } from "../../lib/surfaceClasses";
import Button from "../../components/Button";
import PageSectionHeading from "../../components/PageSectionHeading";
import Skeleton from "../../components/Skeleton";
import ErrorBanner from "../../components/ErrorBanner";
import SuccessBanner from "../../components/SuccessBanner";

type PreferenceKey =
	| "notifyOnNewSignUp"
	| "notifyOnWithdrawal"
	| "notifyOnEngagementConfirmed"
	| "notifyOnEngagementCancelled"
	| "notifyOnEngagementReminder";

const PREFERENCE_ROWS: {
	key: PreferenceKey;
	labelKey: string;

	organizerOnly?: boolean;
}[] = [
	{
		key: "notifyOnNewSignUp",
		labelKey: "notificationPreferences.newSignUp",
		organizerOnly: true,
	},
	{
		key: "notifyOnWithdrawal",
		labelKey: "notificationPreferences.withdrawal",
		organizerOnly: true,
	},
	{
		key: "notifyOnEngagementConfirmed",
		labelKey: "notificationPreferences.engagementConfirmed",
	},
	{
		key: "notifyOnEngagementCancelled",
		labelKey: "notificationPreferences.engagementCancelled",
	},
	{
		key: "notifyOnEngagementReminder",
		labelKey: "notificationPreferences.engagementReminder",
	},
];

function PreferenceRowList({
	rows,
	preferences,
	onToggle,
}: {
	rows: typeof PREFERENCE_ROWS;
	preferences: NotificationPreferencesResponse;
	onToggle: (key: PreferenceKey) => void;
}) {
	const { t } = useTranslation();
	return (
		<div className="space-y-3">
			{rows.map((row) => (
				<label
					key={row.key}
					htmlFor={row.key}
					className="flex cursor-pointer items-start gap-3 py-1"
				>
					<input
						type="checkbox"
						id={row.key}
						checked={preferences[row.key]}
						onChange={() => onToggle(row.key)}
						className="mt-0.5 h-4 w-4 shrink-0 accent-brand-600"
					/>
					<span className="text-sm text-gray-800">{t(row.labelKey)}</span>
				</label>
			))}
		</div>
	);
}

export default function NotificationPreferencesSection() {
	const api = useApiClient();
	const { t } = useTranslation();
	const {
		orgs,
		loading: orgsLoading,
		failed: orgsFailed,
	} = useMyOrganizations();

	const [preferences, setPreferences] =
		useState<NotificationPreferencesResponse | null>(null);
	const [preferencesLoading, setPreferencesLoading] = useState(true);
	const [loadError, setLoadError] = useState<string | null>(null);
	const [saving, setSaving] = useState(false);
	const [saveError, setSaveError] = useState<string | null>(null);
	const [savedSuccess, setSavedSuccess] = useState(false);

	useEffect(() => {
		let cancelled = false;

		api
			.getNotificationPreferences()
			.then((data) => {
				if (!cancelled) setPreferences(data);
			})
			.catch(() => {
				if (!cancelled) setLoadError(t("notificationPreferences.loadError"));
			})
			.finally(() => {
				if (!cancelled) setPreferencesLoading(false);
			});

		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	const organizerRowsVisible = orgs.length > 0 || orgsFailed;
	const visibleRows = PREFERENCE_ROWS.filter(
		(row) => !row.organizerOnly || organizerRowsVisible,
	);

	const organizerRows = visibleRows.filter((row) => row.organizerOnly);
	const volunteerRows = visibleRows.filter((row) => !row.organizerOnly);

	const loading = preferencesLoading || orgsLoading;

	function toggle(key: PreferenceKey) {
		setPreferences((prev) => (prev ? { ...prev, [key]: !prev[key] } : prev));
	}

	async function handleSave(e: React.FormEvent) {
		e.preventDefault();
		if (!preferences) return;

		setSaving(true);
		setSaveError(null);
		setSavedSuccess(false);
		try {
			await api.updateNotificationPreferences({
				notifyOnNewSignUp: preferences.notifyOnNewSignUp,
				notifyOnWithdrawal: preferences.notifyOnWithdrawal,
				notifyOnEngagementConfirmed: preferences.notifyOnEngagementConfirmed,
				notifyOnEngagementCancelled: preferences.notifyOnEngagementCancelled,
				notifyOnEngagementReminder: preferences.notifyOnEngagementReminder,
			});
			setSavedSuccess(true);
		} catch (err) {
			setSaveError(
				getApiErrorMessage(err, t("notificationPreferences.saveError")),
			);
		} finally {
			setSaving(false);
		}
	}

	return (
		<section className={`mb-6 max-w-3xl ${cardClass} sm:p-6`}>
			<PageSectionHeading>
				{t("notificationPreferences.title")}
			</PageSectionHeading>
			<p className="mb-2 text-sm text-gray-600">
				{t("notificationPreferences.description")}
			</p>
			<p className="mb-4 text-sm text-gray-600">
				{t("notificationPreferences.inAppNotice")}
			</p>

			{loading && (
				<div className="space-y-2" role="status">
					<span className="sr-only">{t("profile.loading")}</span>

					{visibleRows.map((row) => (
						<Skeleton key={row.key} className="h-5 w-64" />
					))}
				</div>
			)}

			{!loading && loadError && <ErrorBanner message={loadError} />}

			{!loading && preferences && (
				<form onSubmit={handleSave} className="space-y-4">
					{saveError && <ErrorBanner message={saveError} className="mb-2" />}
					{savedSuccess && (
						<SuccessBanner
							message={t("notificationPreferences.savedSuccess")}
							className="mb-2"
						/>
					)}

					{organizerRowsVisible ? (
						<div className="space-y-4">
							<div>
								<h3 className="mb-3 text-sm font-semibold text-gray-900">
									{t("notificationPreferences.organizerGroupLabel")}
								</h3>
								<PreferenceRowList
									rows={organizerRows}
									preferences={preferences}
									onToggle={toggle}
								/>
							</div>
							<div>
								<h3 className="mb-3 text-sm font-semibold text-gray-900">
									{t("notificationPreferences.volunteerGroupLabel")}
								</h3>
								<PreferenceRowList
									rows={volunteerRows}
									preferences={preferences}
									onToggle={toggle}
								/>
							</div>
						</div>
					) : (
						<PreferenceRowList
							rows={visibleRows}
							preferences={preferences}
							onToggle={toggle}
						/>
					)}

					<Button type="submit" size="sm" disabled={saving}>
						{saving
							? t("notificationPreferences.saving")
							: t("notificationPreferences.save")}
					</Button>
				</form>
			)}
		</section>
	);
}
