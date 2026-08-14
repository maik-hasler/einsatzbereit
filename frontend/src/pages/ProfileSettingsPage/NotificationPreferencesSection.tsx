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
	// Both emails are sent to the organizer of an opportunity, which nobody
	// can be without belonging to an organization first - so these two rows
	// are filtered out for everyone else, see organizerRowsVisible below.
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

// Extracted so the grouped (organizer + volunteer) and flat (volunteer-only)
// layouts below render the exact same row markup instead of two copies of it.
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
						className="mt-0.5 h-4 w-4 accent-brand-600"
					/>
					<span className="text-sm text-gray-800">{t(row.labelKey)}</span>
				</label>
			))}
		</div>
	);
}

// Self-contained email-notification-preferences card (#1055), split out of
// ProfileOverviewPage in the same style as DangerZoneCard, relocated to
// ProfileSettingsPage - see #1684. Owns its own fetch/save/error state and
// API calls independently of the profile form.
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

	// #1783: a volunteer who belongs to no organization can never organize an
	// opportunity, so the two organizer emails can never fire for her - and
	// their labels ("... you organize") read as if she does. Hide them rather
	// than offer settings with no effect. Their stored values are still saved
	// untouched below, so they survive her later joining an organization.
	//
	// Fail open when the organization list itself failed to load: that state
	// is indistinguishable from "no organizations" here, and silently dropping
	// a real organizer's own settings is the worse of the two outcomes.
	const organizerRowsVisible = orgs.length > 0 || orgsFailed;
	const visibleRows = PREFERENCE_ROWS.filter(
		(row) => !row.organizerOnly || organizerRowsVisible,
	);
	// #1844: once the organizer rows are visible, the flat list mixes two
	// audiences of the same account with no separation - split it into the two
	// groups below rather than growing a third layout for the always-volunteer
	// case, where a single group heading would just repeat the card title.
	const organizerRows = visibleRows.filter((row) => row.organizerOnly);
	const volunteerRows = visibleRows.filter((row) => !row.organizerOnly);
	// Held until the organization list resolves too, so the two organizer rows
	// appear with the rest of the list instead of popping in a beat later.
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
		// White card, not the gray-50 cardSubtleClass: this is the page's primary
		// content, not an aside, and a grey slab was the largest surface on it.
		// max-w-3xl caps the measure inside the page's shared max-w-5xl column -
		// a checkbox list has no reason to run the full width even though the
		// column does (see the note on the page's own wrapper).
		<section className={`mb-6 max-w-3xl ${cardClass} sm:p-6`}>
			<PageSectionHeading>
				{t("notificationPreferences.title")}
			</PageSectionHeading>
			<p className="mb-4 text-sm text-gray-600">
				{t("notificationPreferences.description")}
			</p>

			{loading && (
				<div className="space-y-2" role="status">
					<span className="sr-only">{t("profile.loading")}</span>
					{/* visibleRows, not PREFERENCE_ROWS: membership is still
					unknown here, so this is the volunteer-sized list - the card
					then grows into the organizer rows rather than five
					placeholders collapsing into three for everyone else. */}
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
