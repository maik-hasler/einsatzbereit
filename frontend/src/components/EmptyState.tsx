import Button from "./Button";

interface Props {
	title: string;
	message?: string;
	action?: {
		label: string;
		onClick: () => void;
	};
	// Smaller padding/type for dashboard widgets and dropdown panels, where
	// the default py-12 is too much space for the room available - #1122.
	compact?: boolean;
}

export default function EmptyState({
	title,
	message,
	action,
	compact = false,
}: Props) {
	return (
		<div className={compact ? "py-4 text-center" : "py-12 text-center"}>
			<p
				className={
					compact
						? "text-sm font-medium text-gray-900"
						: "font-medium text-gray-900"
				}
			>
				{title}
			</p>
			{message && (
				<p
					className={
						compact
							? "mt-1 text-xs text-gray-500"
							: "mt-1 text-sm text-gray-500"
					}
				>
					{message}
				</p>
			)}
			{action && (
				<Button
					onClick={action.onClick}
					size={compact ? "sm" : "md"}
					className={compact ? "mt-3" : "mt-4"}
				>
					{action.label}
				</Button>
			)}
		</div>
	);
}
