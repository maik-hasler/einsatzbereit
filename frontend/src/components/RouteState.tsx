import { useTranslation } from "react-i18next";
import Button from "./Button";
import ErrorBanner from "./ErrorBanner";
import { statusTitleClass } from "../lib/headingClasses";
import {
	ExclamationTriangleIcon,
	LockClosedIcon,
	QuestionMarkCircleIcon,
	SignalSlashIcon,
} from "./icons";

/**
 * The four ways a route can fail to show what the user asked for. Before
 * #1774 three of them collapsed into the fourth: an unknown organization id,
 * a missing admin role and a dropped connection all ended on the same
 * "something went wrong / try again" screen, which told the user nothing true
 * about their situation and, in two of the three cases, offered a retry that
 * could not possibly succeed.
 */
export type RouteStateVariant = "notFound" | "forbidden" | "offline" | "error";

const VARIANT_ICONS = {
	notFound: QuestionMarkCircleIcon,
	forbidden: LockClosedIcon,
	offline: SignalSlashIcon,
	error: ExclamationTriangleIcon,
} as const;

// Tinted disc behind the glyph, one tone per situation so the four states are
// distinguishable before a word of copy is read. Only `error` is red - being
// offline or lacking a role is a fact about the situation, not a fault, and
// painting all four red is how they read as interchangeable in the first
// place.
const VARIANT_TONES = {
	notFound: "bg-gray-100 text-gray-600",
	forbidden: "bg-amber-50 text-amber-700",
	offline: "bg-brand-50 text-brand-700",
	error: "bg-red-50 text-red-700",
} as const;

interface Props {
	variant: RouteStateVariant;
	title: string;
	message: string;
	/**
	 * Honoured for `error` only - the one variant where trying the same thing
	 * again can actually work. A 403/404 is permanent, and retrying while
	 * offline is guaranteed to fail, so those two recover by other means (see
	 * the `online`-event refetches in OrgAppLayout and useLoadMore) rather
	 * than by handing the user a button that does nothing.
	 */
	onRetry?: () => void;
	/** Escape hatch out of a dead end - rendered as a real link, not a button. */
	action?: { label: string; to: string };
	/**
	 * Renders in flow, without the page-level <h1>, for a state that replaces
	 * one section of a page rather than the whole route (e.g. the opportunity
	 * results area, which sits under a page that already owns the <h1>).
	 */
	inline?: boolean;
	"data-testid"?: string;
}

export default function RouteState({
	variant,
	title,
	message,
	onRetry,
	action,
	inline = false,
	"data-testid": testId,
}: Props) {
	const { t } = useTranslation();
	const Icon = VARIANT_ICONS[variant];
	const canRetry = variant === "error" && !!onRetry;

	return (
		<div
			data-testid={testId}
			className={
				inline
					? "mt-6 flex flex-col items-center rounded-card border border-dashed border-gray-200 px-4 py-10 text-center"
					: "mx-auto flex max-w-lg flex-col items-center px-4 py-14 text-center sm:py-20"
			}
		>
			<span
				className={`mb-5 flex h-16 w-16 items-center justify-center rounded-full ${VARIANT_TONES[variant]}`}
			>
				<Icon />
			</span>

			{inline ? (
				<p className="text-lg font-semibold text-gray-900">{title}</p>
			) : (
				<h1 className={`text-gray-900 ${statusTitleClass}`}>{title}</h1>
			)}

			{/* The error variant keeps ErrorBanner's role="alert"/aria-live: a
			retry that fails again re-renders this same branch with no navigation,
			which a screen reader would otherwise never hear (#1224). `offline`
			gets the polite equivalent - it is reached by a state change rather
			than by navigation too, but it is not an emergency and it clears
			itself. notFound/forbidden are navigation results, announced by the
			heading like any other page. */}
			{variant === "error" ? (
				<ErrorBanner message={message} className="mt-4 max-w-md" />
			) : (
				<p
					role={variant === "offline" ? "status" : undefined}
					className="mt-4 max-w-md leading-relaxed text-gray-600"
				>
					{message}
				</p>
			)}

			{(canRetry || action) && (
				<div className="mt-8 flex flex-wrap justify-center gap-3">
					{canRetry && <Button onClick={onRetry}>{t("orgApp.retry")}</Button>}
					{action && (
						<Button to={action.to} variant={canRetry ? "secondary" : "primary"}>
							{action.label}
						</Button>
					)}
				</div>
			)}
		</div>
	);
}
