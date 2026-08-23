import { useState } from "react";
import { useDismissableOverlay } from "../hooks/useDismissableOverlay";
import { EllipsisHorizontalIcon } from "./icons";

export interface RowAction {
	key: string;
	label: string;
	onClick: () => void;
	disabled?: boolean;

	destructive?: boolean;
	testId?: string;
}

export default function RowActionsMenu({
	actions,
	label,
}: {
	actions: RowAction[];

	label: string;
}) {
	const [open, setOpen] = useState(false);
	const rootRef = useDismissableOverlay<HTMLDivElement>(open, () =>
		setOpen(false),
	);

	if (actions.length === 0) return null;

	return (
		<div ref={rootRef} className="relative">
			<button
				type="button"
				onClick={() => setOpen((o) => !o)}
				aria-expanded={open}
				aria-label={label}
				data-testid="row-actions-trigger"
				className="inline-flex h-8 w-8 items-center justify-center rounded-lg border border-gray-200 text-gray-500 transition hover:bg-gray-50 hover:text-gray-700"
			>
				<EllipsisHorizontalIcon className="h-4 w-4" />
			</button>

			{open && (
				<ul
					aria-label={label}
					className="absolute top-full right-0 z-50 mt-1 w-56 rounded-lg border border-gray-200 bg-white py-1 text-sm shadow-modal"
				>
					{actions.map((action) => (
						<li key={action.key}>
							<button
								type="button"
								disabled={action.disabled}
								data-testid={action.testId}
								onClick={() => {
									setOpen(false);
									action.onClick();
								}}
								className={`block w-full px-3 py-2 text-left transition disabled:cursor-not-allowed disabled:opacity-50 ${
									action.destructive
										? "text-red-600 hover:bg-red-50"
										: "text-gray-700 hover:bg-gray-50"
								}`}
							>
								{action.label}
							</button>
						</li>
					))}
				</ul>
			)}
		</div>
	);
}
