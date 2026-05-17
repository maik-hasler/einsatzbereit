interface Props {
	title: string;
	message?: string;
	action?: {
		label: string;
		onClick: () => void;
	};
}

export default function EmptyState({ title, message, action }: Props) {
	return (
		<div className="py-12 text-center">
			<p className="font-medium text-gray-900">{title}</p>
			{message && <p className="mt-1 text-sm text-gray-500">{message}</p>}
			{action && (
				<button
					onClick={action.onClick}
					className="mt-4 rounded bg-black px-4 py-2 text-sm text-white hover:bg-gray-800"
				>
					{action.label}
				</button>
			)}
		</div>
	);
}
