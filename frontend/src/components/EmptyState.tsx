import Button from "./Button";

interface Props {
	title: string;
	message?: string;

	action?: {
		label: string;
		to?: string;
		onClick?: () => void;

		testId?: string;
	};

	compact?: boolean;
}

export default function EmptyState({
	title,
	message,
	action,
	compact = false,
}: Props) {
	return (
		<div
			className={
				compact
					? "py-4 text-center"
					: "rounded-card border border-dashed border-gray-200 px-4 py-12 text-center"
			}
		>
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
			{action &&
				(action.to ? (
					<Button
						to={action.to}
						onClick={action.onClick}
						size={compact ? "sm" : "md"}
						className={compact ? "mt-3" : "mt-4"}
						data-testid={action.testId}
					>
						{action.label}
					</Button>
				) : (
					<Button
						type="button"
						onClick={action.onClick}
						size={compact ? "sm" : "md"}
						className={compact ? "mt-3" : "mt-4"}
						data-testid={action.testId}
					>
						{action.label}
					</Button>
				))}
		</div>
	);
}
