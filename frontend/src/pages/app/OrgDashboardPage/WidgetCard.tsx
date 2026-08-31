import { useRef, type ReactNode } from "react";
import { cardClass } from "../../../lib/surfaceClasses";
import { useScrollFade } from "../../../hooks/useScrollFade";

interface Props {
	titleId: string;
	title: string;

	/** A single control in the card's header row, aligned opposite the title. */
	action?: ReactNode;

	/**
	 * One "and the rest of them" affordance, pinned under the body instead of
	 * scrolling away with it. The upcoming-opportunities link used to sit at the
	 * end of the scrolling list, so on a full tile it was never on screen.
	 */
	footer?: ReactNode;

	children: ReactNode;
	className?: string;

	/**
	 * Turns the body's own scrolling off, for a widget that brings its own
	 * (the calendar's agenda table) or must not have any.
	 */
	scroll?: boolean;

	stretchedLink?: ReactNode;
}

// Barlow Condensed, uppercase and tracked: the label of a duty roster rather
// than the heading of an article. Every tile on the board is a card of the same
// weight, so a widget's name is signposting, not content - it steps back and
// lets the rows, counts and actions inside be what the eye lands on. gray-500
// clears the 4.5:1 text floor (4.9:1 on white).
const WIDGET_TITLE_CLASS =
	"truncate font-display text-sm font-semibold tracking-wider text-gray-500 uppercase";

export default function WidgetCard({
	titleId,
	title,
	action,
	footer,
	children,
	className,
	scroll = true,
	stretchedLink,
}: Props) {
	const scrollRef = useRef<HTMLDivElement>(null);
	// Vertical, not horizontal. A dashboard tile that scrolls sideways hides
	// content in the one direction nobody thinks to look; every widget here
	// wraps or truncates instead. What a tile genuinely does run out of is
	// height, and the fade is what says "this list carries on".
	const { canScrollStart, canScrollEnd } = useScrollFade(scrollRef, "y");

	return (
		<section
			aria-labelledby={titleId}
			className={`relative flex h-full flex-col ${cardClass} ${className ?? ""}`}
		>
			<div className="mb-3 flex shrink-0 items-center justify-between gap-3">
				<h2 id={titleId} className={WIDGET_TITLE_CLASS}>
					{title}
				</h2>
				{action}
			</div>

			<div className="relative min-h-0 flex-1">
				<div
					ref={scrollRef}
					className={`h-full ${scroll ? "overflow-y-auto" : "overflow-hidden"}`}
				>
					{children}
				</div>
				<div
					aria-hidden="true"
					data-testid="widget-scroll-fade-start"
					className={`pointer-events-none absolute inset-x-0 top-0 h-6 bg-gradient-to-b from-white to-transparent transition-opacity duration-200 ${
						canScrollStart ? "opacity-100" : "opacity-0"
					}`}
				/>
				<div
					aria-hidden="true"
					data-testid="widget-scroll-fade-end"
					className={`pointer-events-none absolute inset-x-0 bottom-0 h-6 bg-gradient-to-t from-white to-transparent transition-opacity duration-200 ${
						canScrollEnd ? "opacity-100" : "opacity-0"
					}`}
				/>
			</div>

			{footer && (
				<div className="mt-3 shrink-0 border-t border-gray-100 pt-3">
					{footer}
				</div>
			)}
			{stretchedLink}
		</section>
	);
}
