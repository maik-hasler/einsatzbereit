import type { HTMLAttributes, ReactNode } from "react";

// Shared pill primitive for tags, categories, and status indicators - see
// issue #1125: the same "tag" concept previously had a different
// background/size/weight in the wizard, the profile view, and the
// opportunity detail page.
const TONE_CLASSES = {
	brand: "bg-brand-50 text-brand-700",
	neutral: "bg-gray-100 text-gray-600",
	success: "bg-green-50 text-green-700",
	warning: "bg-amber-50 text-amber-700",
	danger: "bg-red-50 text-red-600",
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
		| { onRemove?: undefined; removeLabel?: undefined }
		| { onRemove: () => void; removeLabel: string }
	);

export default function Chip({
	tone = "neutral",
	size = "md",
	className = "",
	children,
	onRemove,
	removeLabel,
	...rest
}: ChipProps) {
	const classes = [
		"inline-flex items-center gap-1 rounded-full font-medium",
		TONE_CLASSES[tone],
		SIZE_CLASSES[size],
		className,
	]
		.filter(Boolean)
		.join(" ");

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
					<svg
						aria-hidden="true"
						className="h-3 w-3"
						fill="none"
						stroke="currentColor"
						strokeWidth={2.5}
						viewBox="0 0 24 24"
					>
						<path
							strokeLinecap="round"
							strokeLinejoin="round"
							d="M6 18L18 6M6 6l12 12"
						/>
					</svg>
				</button>
			)}
		</span>
	);
}
