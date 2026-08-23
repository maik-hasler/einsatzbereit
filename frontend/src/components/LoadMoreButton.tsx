interface Props {
	loading: boolean;
	label: string;
	loadingLabel: string;
	onClick: () => void;
}

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
				data-testid="load-more"
				onClick={onClick}
				disabled={loading}

				className="rounded-xl border border-brand-600 bg-brand-50 px-8 py-3 text-sm font-semibold text-brand-700 transition-colors hover:bg-brand-100 disabled:opacity-40"
			>
				{loading ? loadingLabel : label}
			</button>
		</div>
	);
}
