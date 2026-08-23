interface Props {
	label: string;
	size?: "sm" | "md" | "lg";
	className?: string;
}

const SIZE_CONFIG: Record<
	NonNullable<Props["size"]>,
	{ icon: string; text: string }
> = {
	sm: { icon: "h-4 w-4", text: "text-sm" },
	md: { icon: "h-5 w-5", text: "" },
	lg: { icon: "h-8 w-8", text: "" },
};

export function SpinnerIcon({ className = "" }: { className?: string }) {
	return (
		<svg
			className={`shrink-0 animate-spin text-brand-600 motion-reduce:animate-none ${className}`}
			viewBox="0 0 24 24"
			fill="none"
			aria-hidden="true"
		>
			<circle
				className="opacity-25"
				cx="12"
				cy="12"
				r="10"
				stroke="currentColor"
				strokeWidth="4"
			/>
			<path
				className="opacity-75"
				fill="currentColor"
				d="M4 12a8 8 0 0 1 8-8V0C5.373 0 0 5.373 0 12h4Z"
			/>
		</svg>
	);
}

export default function Spinner({ label, size = "md", className = "" }: Props) {
	const { icon, text } = SIZE_CONFIG[size];

	return (
		<div role="status" className={`flex items-center gap-2 ${className}`}>
			<SpinnerIcon className={icon} />
			<span className={`${text} text-gray-500`}>{label}</span>
		</div>
	);
}
