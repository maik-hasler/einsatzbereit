import { forwardRef, type HTMLAttributes } from "react";

interface Props extends Omit<
	HTMLAttributes<HTMLParagraphElement>,
	"className"
> {
	message: string;
	className?: string;
}

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
