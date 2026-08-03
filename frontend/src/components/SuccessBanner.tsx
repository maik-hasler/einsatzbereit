import { forwardRef, type HTMLAttributes } from "react";

interface Props extends Omit<HTMLAttributes<HTMLDivElement>, "className"> {
	message: string | null;
	className?: string;
}

// Shared style for an inline "action succeeded" message - the ErrorBanner
// twin (see issue #1107: three pages hand-rolled this box, one of them with
// the wrong border radius, sitting directly beside an ErrorBanner using the
// correct one). Renders as an always-visible box when `message` is set;
// otherwise collapses to a visually-hidden but still-mounted status region,
// matching ErrorBanner's role/aria-live pattern (#972) for callers that keep
// the region mounted across a success/no-success toggle instead of
// conditionally rendering the component itself.
const BASE_CLASSES =
	"rounded-card bg-green-50 px-4 py-3 text-sm text-green-700";

const SuccessBanner = forwardRef<HTMLDivElement, Props>(
	({ message, className = "", ...rest }, ref) => (
		<div
			ref={ref}
			role="status"
			aria-live="polite"
			className={
				message
					? [BASE_CLASSES, className].filter(Boolean).join(" ")
					: "sr-only"
			}
			{...rest}
		>
			{message}
		</div>
	),
);
SuccessBanner.displayName = "SuccessBanner";

export default SuccessBanner;
