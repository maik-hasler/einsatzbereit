import { useState } from "react";
import { useDismissableOverlay } from "../hooks/useDismissableOverlay";
import { EllipsisHorizontalIcon } from "./icons";

export interface RowAction {
	key: string;
	label: string;
	onClick: () => void;
	disabled?: boolean;
	/** Renders the item in the destructive tone. */
	destructive?: boolean;
	testId?: string;
}

// Overflow menu for a row's secondary actions. The org app's opportunity
// cards used to render every action as its own button - up to five per card
// (Edit, Delete, Unpublish, Cancel, Manage sign-ups), three of them reading
// as destructive and two of those in the same red outline - so the row's
// actual primary action had no more weight than "Delete", and telling
// Unpublish from Cancel from Delete meant reading all three every time.
export default function RowActionsMenu({
	actions,
	label,
}: {
	actions: RowAction[];
	/** Accessible name for the trigger, e.g. "More actions for {title}". */
	label: string;
}) {
	const [open, setOpen] = useState(false);
	const rootRef = useDismissableOverlay<HTMLDivElement>(open, () =>
		setOpen(false),
	);

	if (actions.length === 0) return null;

	return (
		<div ref={rootRef} className="relative">
			{/* A disclosure, not a WAI-ARIA menu. An earlier version claimed
			aria-haspopup="menu" + role="menu"/"menuitem", which is structurally
			valid and passes axe but promises keyboard behaviour this does not
			implement: focus is never moved into the list on open and there is no
			Arrow/Home/End roving tabindex, so a screen-reader user told "menu"
			reaches for arrow keys and gets nothing. Tab through a labelled list
			of buttons is what it actually does, so that is what it announces. */}
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
