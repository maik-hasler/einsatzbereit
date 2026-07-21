import type { ReactNode } from "react";

interface Props {
	titleId: string;
	title: string;
	action?: ReactNode;
	children: ReactNode;
	className?: string;
}

export default function WidgetCard({
	titleId,
	title,
	action,
	children,
	className,
}: Props) {
	return (
		<section
			aria-labelledby={titleId}
			className={`h-full rounded-2xl border border-gray-100 bg-white p-5 shadow-sm ${className ?? ""}`}
		>
			<div className="mb-4 flex items-center justify-between gap-3">
				<h2 id={titleId} className="text-base font-semibold text-gray-900">
					{title}
				</h2>
				{action}
			</div>
			{children}
		</section>
	);
}
