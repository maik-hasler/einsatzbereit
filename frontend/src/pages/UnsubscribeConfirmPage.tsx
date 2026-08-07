import { useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { usePageTitle } from "../hooks/usePageTitle";
import { runtimeConfig } from "../lib/runtimeConfig";
import Button from "../components/Button";
import ErrorBanner from "../components/ErrorBanner";
import { statusTitleClass } from "../lib/headingClasses";

// One-click-unsubscribe links in transactional emails point here instead of
// straight at the backend's state-changing endpoint (#1725) - a mail scanner
// or link prefetcher that follows an email link only ever loads this static
// page (no state change); the actual unsubscribe only happens once a person
// deliberately clicks the confirm link below, which navigates on to the
// backend's (unchanged) GET /v1/users/{userId}/unsubscribe.
const TYPE_LABEL_KEYS: Record<string, string> = {
	NewSignUp: "notificationPreferences.newSignUp",
	Withdrawal: "notificationPreferences.withdrawal",
	EngagementConfirmed: "notificationPreferences.engagementConfirmed",
	EngagementCancelled: "notificationPreferences.engagementCancelled",
	EngagementReminder: "notificationPreferences.engagementReminder",
};

export default function UnsubscribeConfirmPage() {
	const { t } = useTranslation();
	usePageTitle(t("unsubscribeConfirm.title"));
	const [searchParams] = useSearchParams();

	const userId = searchParams.get("userId");
	const type = searchParams.get("type");
	const token = searchParams.get("token");

	const isValid = Boolean(userId && type && token);
	const typeLabel =
		type && TYPE_LABEL_KEYS[type] ? t(TYPE_LABEL_KEYS[type]) : type;

	const confirmUrl = isValid
		? `${runtimeConfig.apiUrl}/v1/users/${encodeURIComponent(userId ?? "")}/unsubscribe?type=${encodeURIComponent(type ?? "")}&token=${encodeURIComponent(token ?? "")}`
		: undefined;

	return (
		<div className="flex min-h-[70vh] items-center justify-center px-4 text-center">
			<div className="max-w-md">
				<h1 className={`mb-4 text-brand-700 ${statusTitleClass}`}>
					{t("unsubscribeConfirm.title")}
				</h1>
				{isValid && confirmUrl ? (
					<>
						<p className="mb-8 text-black">
							{t("unsubscribeConfirm.description", { type: typeLabel })}
						</p>
						<Button href={confirmUrl} size="lg">
							{t("unsubscribeConfirm.confirm")}
						</Button>
					</>
				) : (
					<ErrorBanner message={t("unsubscribeConfirm.invalidLink")} />
				)}
			</div>
		</div>
	);
}
