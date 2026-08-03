interface Props {
	loading: boolean;
	label: string;
	loadingLabel: string;
	onClick: () => void;
}

// Shared "load more" trigger - see einsatzbereit#1108: this markup had
// drifted into four hand-rolled copies (one with divergent padding, all with
// divergent wrapper margins) across OrganizationsPage, AdministrationPage,
// OpportunityResultsList, and ActivitySection. Margin matches LoadMoreError's
// wrapper so swapping between the two states (button vs. inline retry) on
// the same list doesn't shift layout.
export default function LoadMoreButton({
	loading,
	label,
	loadingLabel,
	onClick,
}: Props) {
	return (
		<div className="mt-6 flex justify-center">
			<button
				type="button"
				onClick={onClick}
				disabled={loading}
				className="rounded-xl border border-brand-200 bg-brand-50 px-8 py-3 text-sm font-semibold text-brand-700 transition-colors hover:bg-brand-100 disabled:opacity-40"
			>
				{loading ? loadingLabel : label}
			</button>
		</div>
	);
}
