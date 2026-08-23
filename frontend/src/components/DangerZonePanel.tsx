import Button from "./Button";

interface Props {
	title: string;
	description: string;
	actionLabel: string;
	onAction: () => void;
	disabled?: boolean;
	className?: string;
}

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
