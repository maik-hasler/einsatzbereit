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
			className={`flex h-full flex-col rounded-2xl border border-gray-100 bg-white p-5 shadow-sm ${className ?? ""}`}
		>
			<div className="mb-4 flex shrink-0 items-center justify-between gap-3">
				<h2 id={titleId} className="text-base font-semibold text-gray-900">
					{title}
				</h2>
				{action}
			</div>
			{/* The grid row height dashboard-wide is now fixed (see
			OrgDashboardPage's grid className), so a widget whose content is
			taller than its allotted rows scrolls within itself here instead of
			growing the shared row band and throwing off every other tile's
			height. */}
			<div className="min-h-0 flex-1 overflow-y-auto">{children}</div>
		</section>
	);
}
