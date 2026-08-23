import { useRef, type ReactNode } from "react";
import { cardClass } from "../../../lib/surfaceClasses";
import { useScrollFade } from "../../../hooks/useScrollFade";

interface Props {
	titleId: string;
	title: string;
	action?: ReactNode;
	children: ReactNode;
	className?: string;

	stretchedLink?: ReactNode;
}

export default function WidgetCard({
	titleId,
	title,
	action,
	children,
	className,
	stretchedLink,
}: Props) {
	const scrollRef = useRef<HTMLDivElement>(null);
	const { canScrollStart, canScrollEnd } = useScrollFade(scrollRef, "x");

	return (
		<section
			aria-labelledby={titleId}
			className={`relative flex h-full flex-col ${cardClass} ${className ?? ""}`}
		>
			<div className="mb-4 flex shrink-0 items-center justify-between gap-3">
				<h2 id={titleId} className="text-base font-semibold text-gray-900">
					{title}
				</h2>
				{action}
			</div>

			<div className="relative min-h-0 flex-1">
				<div ref={scrollRef} className="h-full overflow-x-auto overflow-y-auto">
					{children}
				</div>
				<div
					aria-hidden="true"
					className={`pointer-events-none absolute inset-y-0 left-0 w-8 bg-gradient-to-r from-white to-transparent transition-opacity duration-200 ${
						canScrollStart ? "opacity-100" : "opacity-0"
					}`}
				/>
				<div
					aria-hidden="true"
					className={`pointer-events-none absolute inset-y-0 right-0 w-8 bg-gradient-to-l from-white to-transparent transition-opacity duration-200 ${
						canScrollEnd ? "opacity-100" : "opacity-0"
					}`}
				/>
			</div>
			{stretchedLink}
		</section>
	);
}
