// Small icon set shared by quick actions in the action bar (see
// QuickActionsContext.tsx / Header.tsx). Icons are decorative - the visible
// label (or aria-label on mobile, where the label text is hidden) carries
// the accessible name, so every icon here is aria-hidden.
function iconProps() {
	return {
		className: "h-4 w-4",
		fill: "none" as const,
		viewBox: "0 0 24 24",
		strokeWidth: "1.5",
		stroke: "currentColor",
		"aria-hidden": "true" as const,
	};
}

export function EditIcon() {
	return (
		<svg {...iconProps()}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L6.832 19.82a4.5 4.5 0 0 1-1.897 1.13l-2.685.8.8-2.685a4.5 4.5 0 0 1 1.13-1.897L16.863 4.487Zm0 0L19.5 7.125"
			/>
		</svg>
	);
}

export function SaveIcon() {
	return (
		<svg {...iconProps()}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M4.5 12.75l6 6 9-13.5"
			/>
		</svg>
	);
}

export function CancelIcon() {
	return (
		<svg {...iconProps()}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M6 18 18 6M6 6l12 12"
			/>
		</svg>
	);
}

export function PlusIcon() {
	return (
		<svg {...iconProps()}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M12 4.5v15m7.5-7.5h-15"
			/>
		</svg>
	);
}

export function TrashIcon() {
	return (
		<svg {...iconProps()}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M6 7h12M9 7V5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2M6 7l1 12.5A2 2 0 0 0 9 21h6a2 2 0 0 0 2-1.5L18 7M10 11v6M14 11v6"
			/>
		</svg>
	);
}
