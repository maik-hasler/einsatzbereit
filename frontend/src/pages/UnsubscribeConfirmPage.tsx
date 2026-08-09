import { useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { usePageTitle } from "../hooks/usePageTitle";
import { runtimeConfig } from "../lib/runtimeConfig";
import Button from "../components/Button";
import ErrorBanner from "../components/ErrorBanner";
import { EnvelopeIcon } from "../components/icons";
import { statusTitleClass } from "../lib/headingClasses";
import { cardClass } from "../lib/surfaceClasses";

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

	// Boxed rather than floating text on white - see the same note on
	// UnsubscribePage, the step this one leads into (#1755).
	return (
		<div className="mx-auto max-w-md py-10 sm:py-16">
			<div className={`${cardClass} text-center sm:p-8`}>
				<div
					aria-hidden="true"
					className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-brand-100 text-brand-700"
				>
					<EnvelopeIcon className="h-7 w-7" />
				</div>
				<h1 className={`mt-6 text-gray-900 ${statusTitleClass}`}>
					{t("unsubscribeConfirm.title")}
				</h1>
				{isValid && confirmUrl ? (
					<>
						<p className="mt-4 leading-relaxed text-gray-700">
							{t("unsubscribeConfirm.description", { type: typeLabel })}
						</p>
						<Button href={confirmUrl} size="lg" className="mt-8">
							{t("unsubscribeConfirm.confirm")}
						</Button>
					</>
				) : (
					<div className="mt-6 text-left">
						<ErrorBanner message={t("unsubscribeConfirm.invalidLink")} />
					</div>
				)}
			</div>
		</div>
	);
}
