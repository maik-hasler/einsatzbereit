import { forwardRef, type HTMLAttributes } from "react";

interface Props extends Omit<
	HTMLAttributes<HTMLParagraphElement>,
	"className"
> {
	message: string;
	className?: string;
}

// Single shared style for an inline "action failed" message - the box every
// page's error state should share (see issue #853: pages alternated between
// a bare red line and a boxed banner, with different border radii, for the
// same state).
const BASE_CLASSES = "rounded-card bg-red-50 px-4 py-3 text-sm text-red-700";

const ErrorBanner = forwardRef<HTMLParagraphElement, Props>(
	({ message, className = "", ...rest }, ref) => (
		<p
			ref={ref}
			role="alert"
			aria-live="assertive"
			className={[BASE_CLASSES, className].filter(Boolean).join(" ")}
			{...rest}
		>
			{message}
		</p>
	),
);
ErrorBanner.displayName = "ErrorBanner";

export default ErrorBanner;
