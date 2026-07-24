import Button from "./Button";

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
				<Button onClick={action.onClick} className="mt-4">
					{action.label}
				</Button>
			)}
		</div>
	);
}
