import Button from "./Button";

interface Props {
	title: string;
	message?: string;
	// An empty screen is an invitation to act, so the CTA is the point of it.
	// `to` renders the CTA as a real link (navigation should be a link, not a
	// button that happens to navigate); `onClick` for in-place actions like
	// opening a modal. Supply one or the other.
	action?: {
		label: string;
		to?: string;
		onClick?: () => void;
		// Lets a caller reuse an existing testid on the CTA - e.g. ProfileOverviewPage
		// points this at "profile-edit" so the empty-state CTA is reachable the same
		// way as the header edit button it replaces while the panel is empty (#2066).
		testId?: string;
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
	// The full-size variant gets a dashed outline (#1755). Without one it was
	// centred text floating in the page background, which on a short page - the
	// admin Reports/Audit Log sections, My Activity with no sign-ups - read as
	// a rendering failure rather than as "nothing here yet", and left every
	// other section on those pages boxed while this one wasn't. Dashed rather
	// than solid so it reads as an outline waiting to be filled, matching how
	// BadgeGrid draws a not-yet-earned badge.
	//
	// `compact` stays bare on purpose: it exists for empty states already
	// inside a widget card or dropdown panel, where a second frame would
	// double up on the one around it.
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
