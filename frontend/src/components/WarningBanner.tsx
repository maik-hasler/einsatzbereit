import { forwardRef, type HTMLAttributes } from "react";

interface Props extends Omit<
	HTMLAttributes<HTMLParagraphElement>,
	"className"
> {
	message: string;
	className?: string;
}

// The ErrorBanner/SuccessBanner twin (see ErrorBanner.tsx's #853 rationale)
// for an advisory that isn't a failure - e.g. warning a volunteer they're
// close to a churn limit before they hit it (#2043). role="status" rather
// than ErrorBanner's role="alert": this doesn't interrupt like an error does.
const BASE_CLASSES =
	"rounded-card bg-amber-50 px-4 py-3 text-sm text-amber-800";

const WarningBanner = forwardRef<HTMLParagraphElement, Props>(
	({ message, className = "", ...rest }, ref) => (
		<p
			ref={ref}
			role="status"
			aria-live="polite"
			className={[BASE_CLASSES, className].filter(Boolean).join(" ")}
			{...rest}
		>
			{message}
		</p>
	),
);
WarningBanner.displayName = "WarningBanner";

export default WarningBanner;
