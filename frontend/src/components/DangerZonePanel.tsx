import Button from "./Button";

interface Props {
	title: string;
	description: string;
	actionLabel: string;
	onAction: () => void;
	disabled?: boolean;
	className?: string;
}

// Shared panel for irreversible account-level actions (delete organization,
// delete account) - see issue #1105: the two danger zones disagreed on
// radius, border color and padding before this existed.
export default function DangerZonePanel({
	title,
	description,
	actionLabel,
	onAction,
	disabled = false,
	className = "",
}: Props) {
	return (
		<div
			className={`rounded-card border border-red-100 bg-red-50 p-6 ${className}`.trim()}
		>
			<h2 className="mb-1 text-base font-semibold text-red-800">{title}</h2>
			{/* Body colour, not an error colour (#1792): setting the whole
			    explanation in red spent the signal on prose - the heading and
			    the destructive button carry it instead. */}
			<p className="mb-4 text-sm text-gray-600">{description}</p>
			<Button
				type="button"
				variant="dangerOutline"
				onClick={onAction}
				disabled={disabled}
			>
				{actionLabel}
			</Button>
		</div>
	);
}
