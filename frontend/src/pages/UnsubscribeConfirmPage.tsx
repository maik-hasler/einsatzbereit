import { useState } from "react";
import { useSearchParams, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import { usePageTitle } from "../hooks/usePageTitle";
import { useApiClient } from "../hooks/useApiClient";
import { getApiErrorMessage } from "../lib/apiError";
import { statusTitleClass } from "../lib/headingClasses";
import Button from "../components/Button";
import ErrorBanner from "../components/ErrorBanner";

const TYPE_LABEL_KEYS: Record<string, string> = {
	NewSignUp: "notificationPreferences.newSignUp",
	Withdrawal: "notificationPreferences.withdrawal",
	EngagementConfirmed: "notificationPreferences.engagementConfirmed",
	EngagementCancelled: "notificationPreferences.engagementCancelled",
	EngagementReminder: "notificationPreferences.engagementReminder",
};

// Confirmation interstitial the one-click unsubscribe link in transactional
// emails now points to, instead of directly at the backend's state-changing
// endpoint (#1725) - a mail scanner or link prefetcher merely loading this
// page can no longer silently opt anyone out, since nothing here mutates
// state until the recipient explicitly clicks the confirm button below.
// UnsubscribeLinkBuilder (backend) builds the userId/type/token query string.
export default function UnsubscribeConfirmPage() {
	const { t } = useTranslation();
	const navigate = useNavigate();
	const api = useApiClient();
	const [searchParams] = useSearchParams();

	const userId = searchParams.get("userId");
	const type = searchParams.get("type");
	const token = searchParams.get("token");

	const [confirming, setConfirming] = useState(false);
	const [error, setError] = useState<string | null>(null);

	usePageTitle(t("unsubscribeConfirm.title"));

	const linkIsValid = Boolean(userId && type && token);
	const typeLabelKey = type ? TYPE_LABEL_KEYS[type] : undefined;

	async function handleConfirm() {
		if (!userId || !type || !token) return;

		setConfirming(true);
		setError(null);
		try {
			await api.unsubscribe(userId, type, token);
			navigate(`/unsubscribed?type=${encodeURIComponent(type)}`);
		} catch (err) {
			setError(getApiErrorMessage(err, t("unsubscribeConfirm.error")));
			setConfirming(false);
		}
	}

	return (
		<div className="flex min-h-[70vh] items-center justify-center px-4 text-center">
			<div className="max-w-md">
				<h1 className={`mb-4 text-brand-700 ${statusTitleClass}`}>
					{t("unsubscribeConfirm.title")}
				</h1>

				{!linkIsValid && (
					<ErrorBanner message={t("unsubscribeConfirm.invalidLink")} />
				)}

				{linkIsValid && (
					<>
						<p className="mb-2 text-black">
							{t("unsubscribeConfirm.description")}
						</p>
						{typeLabelKey && (
							<p className="mb-2 font-semibold text-gray-800">
								{t(typeLabelKey)}
							</p>
						)}
						<p className="mb-8 text-sm text-gray-600">
							{t("unsubscribeConfirm.note")}
						</p>

						{error && <ErrorBanner message={error} className="mb-6" />}

						<Button
							type="button"
							size="lg"
							onClick={handleConfirm}
							disabled={confirming}
							aria-busy={confirming}
						>
							{confirming
								? t("unsubscribeConfirm.confirming")
								: t("unsubscribeConfirm.confirmButton")}
						</Button>
					</>
				)}
			</div>
		</div>
	);
}
