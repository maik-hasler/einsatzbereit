import type { ReactNode } from "react";

interface Props {
	children: ReactNode;

	description?: ReactNode;
}

const BASE_CLASSES = "font-display text-2xl font-bold text-gray-900";

export default function PageSectionHeading({ children, description }: Props) {
	if (description) {
		return (
			<div className="mb-4">
				<h2 className={BASE_CLASSES}>{children}</h2>
				<p className="mt-1 text-sm text-gray-500">{description}</p>
			</div>
		);
	}

	return <h2 className={`mb-4 ${BASE_CLASSES}`}>{children}</h2>;
}
