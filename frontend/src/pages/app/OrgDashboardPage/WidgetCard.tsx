import { useRef, type ReactNode } from "react";
import { cardClass } from "../../../lib/surfaceClasses";
import { useScrollFade } from "../../../hooks/useScrollFade";

interface Props {
	titleId: string;
	title: string;
	action?: ReactNode;
	children: ReactNode;
	className?: string;
	/** The "stretched Link" pattern (frontend/AGENTS.md's Accessibility
	 * conventions - `absolute inset-0` covering the whole clickable card,
	 * e.g. SettingsIconWidget) - rendered as a section-level sibling of
	 * the scrollable content instead of inside `children`, and last in
	 * DOM order so it paints on top and stays clickable everywhere. Inside
	 * `children` it would only size/paint to the new #2062 edge-fade
	 * wrapper below (also position:relative, for the same reason), losing
	 * the header row from its hit area. */
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
			{/* The grid row height is fixed only while editing (see
			OrgDashboardPage's grid className and EditableWidgetTile's h-full -
			#2045 stopped forcing that outside edit mode so a card sizes to its
			own content instead of stretching into empty whitespace), so a widget
			whose content is taller than its allotted rows scrolls within itself
			here instead of growing the shared row band and throwing off every
			other tile's height. overflow-x-auto is the same idea sideways: html sets
			overflow-x: clip page-wide (see global.css), so a widget whose
			content is wider than its rendered width - e.g. the Calendar
			widget's toolbar/agenda table on a narrow viewport - would
			otherwise blow out this grid cell's column and get silently
			clipped with no way to reach it, rather than scrolling within the
			one box that actually has room to show a scrollbar. The edge fades
			below are the affordance that a horizontal scroll is actually
			possible (#2062 - the agenda table's event titles clipped on mobile
			with no hint that scrolling revealed the rest), same device as
			OrgPageHeader's tab-strip fades. */}
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
