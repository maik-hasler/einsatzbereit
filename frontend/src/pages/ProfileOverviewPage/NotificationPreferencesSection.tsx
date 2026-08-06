import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { NotificationPreferencesResponse } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { getApiErrorMessage } from "../../lib/apiError";
import { cardSubtleClass } from "../../lib/surfaceClasses";
import Button from "../../components/Button";
import Skeleton from "../../components/Skeleton";
import ErrorBanner from "../../components/ErrorBanner";
import SuccessBanner from "../../components/SuccessBanner";

type PreferenceKey =
	| "notifyOnNewSignUp"
	| "notifyOnWithdrawal"
	| "notifyOnEngagementConfirmed"
	| "notifyOnEngagementCancelled"
	| "notifyOnEngagementReminder";

const PREFERENCE_ROWS: { key: PreferenceKey; labelKey: string }[] = [
	{ key: "notifyOnNewSignUp", labelKey: "notificationPreferences.newSignUp" },
	{ key: "notifyOnWithdrawal", labelKey: "notificationPreferences.withdrawal" },
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

// Self-contained email-notification-preferences card (#1055), split out of
// ProfileOverviewPage in the same style as DangerZoneCard - owns its own
// fetch/save/error state and API calls independently of the profile form.
export default function NotificationPreferencesSection() {
	const api = useApiClient();
	const { t } = useTranslation();

	const [preferences, setPreferences] =
		useState<NotificationPreferencesResponse | null>(null);
	const [loading, setLoading] = useState(true);
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
				if (!cancelled) setLoading(false);
			});

		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

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
		<section className={`mb-6 ${cardSubtleClass}`}>
			<h2 className="mb-1 text-base font-semibold text-gray-900">
				{t("notificationPreferences.title")}
			</h2>
			<p className="mb-4 text-sm text-gray-600">
				{t("notificationPreferences.description")}
			</p>

			{loading && (
				<div className="space-y-2" role="status">
					<span className="sr-only">{t("profile.loading")}</span>
					{PREFERENCE_ROWS.map((row) => (
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

					<div className="space-y-3">
						{PREFERENCE_ROWS.map((row) => (
							<label
								key={row.key}
								htmlFor={row.key}
								className="flex cursor-pointer items-start gap-3"
							>
								<input
									type="checkbox"
									id={row.key}
									checked={preferences[row.key]}
									onChange={() => toggle(row.key)}
									className="mt-0.5 h-4 w-4 accent-brand-600"
								/>
								<span className="text-sm text-gray-800">{t(row.labelKey)}</span>
							</label>
						))}
					</div>

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
