import { forwardRef, type HTMLAttributes } from "react";

interface Props extends Omit<HTMLAttributes<HTMLDivElement>, "className"> {
	message: string | null;
	className?: string;
}

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
