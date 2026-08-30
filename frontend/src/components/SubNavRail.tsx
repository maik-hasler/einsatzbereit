import { useEffect, useRef } from "react";
import { Link } from "react-router";

export interface SubNavItem {
	key: string;
	href: string;
	label: string;
}

export default function SubNavRail({
	items,
	active,
	ariaLabel,
}: {
	items: readonly SubNavItem[];
	active: string;
	ariaLabel: string;
}) {
	const railRef = useRef<HTMLElement>(null);

	// Below lg: the rail is a horizontally scrolling strip, and it opened at
	// scrollLeft 0 no matter which tab was active - so on a 320-390px viewport
	// the tab you were actually on sat past the right edge, half-cut or gone
	// entirely, and the strip showed no selection at all (#2321). Scroll the
	// active tab into the middle of the strip instead. `scrollTo` on the rail
	// moves only the rail; `scrollIntoView` would drag the page with it.
	useEffect(() => {
		const rail = railRef.current;
		if (!rail) return;
		const activeTab = rail.querySelector<HTMLElement>('[aria-current="page"]');
		if (!activeTab) return;
		// At lg: the rail is a vertical column with no overflow, so there is
		// nothing to scroll and this is a no-op.
		if (rail.scrollWidth <= rail.clientWidth) return;
		rail.scrollTo({
			left: Math.max(
				0,
				activeTab.offsetLeft - (rail.clientWidth - activeTab.offsetWidth) / 2,
			),
		});
	}, [active, items]);

	return (
		<nav
			ref={railRef}
			aria-label={ariaLabel}
			// lg:self-start is load-bearing: as a grid item the nav otherwise
			// stretches to the row's full height, dragging its border-l rule
			// hundreds of pixels past the last tab. Sticky so the rail stays
			// reachable down a long page.
			className="mb-6 flex gap-1 overflow-x-auto border-b border-gray-200 lg:sticky lg:top-24 lg:mb-0 lg:flex-col lg:gap-0.5 lg:self-start lg:overflow-x-visible lg:border-b-0 lg:border-l lg:border-gray-200"
		>
			{items.map((item) => (
				<Link
					key={item.key}
					to={item.href}
					aria-current={active === item.key ? "page" : undefined}
					data-testid={`subnav-${item.key}`}
					className={`shrink-0 border-b-2 px-3 py-2 text-sm font-medium whitespace-nowrap transition-colors lg:-ml-px lg:border-b-0 lg:border-l-2 lg:py-1.5 lg:pl-4 ${
						active === item.key
							? "border-brand-600 text-brand-700 lg:border-brand-700 lg:font-semibold"
							: "border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700 lg:hover:border-gray-300 lg:hover:text-gray-900"
					}`}
				>
					{item.label}
				</Link>
			))}
		</nav>
	);
}
