import { forwardRef, type HTMLAttributes } from "react";

interface Props extends Omit<
	HTMLAttributes<HTMLParagraphElement>,
	"className"
> {
	message: string;
	className?: string;
}

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
