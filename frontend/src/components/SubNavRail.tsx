import { Link } from "react-router";

export interface SubNavItem {
	key: string;
	href: string;
	label: string;
}

// The account area's left rail, extracted so the administration area can use
// the same one rather than growing a second look for the same job. Vertical
// from lg up, horizontal tab strip below it (a rail would eat a phone
// viewport whole) - see ProfileSubNav for the reasoning behind that split.
export default function SubNavRail({
	items,
	active,
	ariaLabel,
}: {
	items: readonly SubNavItem[];
	active: string;
	ariaLabel: string;
}) {
	return (
		<nav
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
