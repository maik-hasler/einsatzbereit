import type { HTMLAttributes, ReactNode } from "react";
import { Link } from "react-router";
import { CloseIcon } from "./icons";

const TONE_CLASSES = {
	brand: "bg-brand-50 text-brand-700",
	neutral: "bg-gray-100 text-gray-600",
	success: "bg-green-50 text-green-700",
	warning: "bg-amber-50 text-amber-700",
	danger: "bg-red-50 text-red-700",
} as const;

export type ChipTone = keyof typeof TONE_CLASSES;

const SIZE_CLASSES = {
	sm: "px-2 py-0.5 text-xs",
	md: "px-3 py-1 text-xs",
} as const;

export type ChipSize = keyof typeof SIZE_CLASSES;

interface CommonProps {
	tone?: ChipTone;
	size?: ChipSize;
	children: ReactNode;
}

type ChipProps = CommonProps &
	Omit<HTMLAttributes<HTMLSpanElement>, "className" | "children"> & {
		className?: string;
	} & (
		| { onRemove?: undefined; removeLabel?: undefined; to?: undefined }
		| { onRemove: () => void; removeLabel: string; to?: undefined }
		| { to: string; onRemove?: undefined; removeLabel?: undefined }
	);

export default function Chip({
	tone = "neutral",
	size = "md",
	className = "",
	children,
	onRemove,
	removeLabel,
	to,
	...rest
}: ChipProps) {
	const classes = [
		"inline-flex items-center gap-1 rounded-full font-medium",
		TONE_CLASSES[tone],
		SIZE_CLASSES[size],
		to ? "transition-colors hover:brightness-95" : "",
		className,
	]
		.filter(Boolean)
		.join(" ");

	if (to) {
		return (
			<Link to={to} className={classes} {...rest}>
				{children}
			</Link>
		);
	}

	return (
		<span className={classes} {...rest}>
			{children}
			{onRemove && (
				<button
					type="button"
					onClick={onRemove}
					aria-label={removeLabel}
					className="rounded-full opacity-70 hover:opacity-100"
				>
					<CloseIcon className="h-3 w-3" />
				</button>
			)}
		</span>
	);
}
