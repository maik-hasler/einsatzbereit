import { ChevronDownIcon } from "./icons";

export interface FaqItem {
	q: string;
	a: string;
}

interface Props {
	items: FaqItem[];
	className?: string;
}

// The landing page's FAQ block and the Help page answer the same kind of
// question and even link to each other ("More questions? See Help"), but were
// two unrelated pieces of markup - a boxed accordion on one, a flat run of
// h3/p pairs on the other, so following that link visibly left the design
// system (issue #1755). One component now backs both.
//
// Native <details>/<summary> rather than a hand-rolled accordion: no open/close
// state to wire up, and it is keyboard- and screen-reader-operable for free.
// group-open:rotate-180 reads the element's own [open] attribute through
// Tailwind's group variant, so the chevron animates without any JS tracking
// which item is expanded.
export default function FaqAccordion({ items, className = "" }: Props) {
	return (
		<div
			className={`divide-y divide-gray-200 rounded-card border border-gray-100 bg-white px-6 shadow-resting ${className}`}
		>
			{items.map(({ q, a }) => (
				<details key={q} className="group py-5">
					<summary className="flex cursor-pointer list-none items-center justify-between gap-4 text-base font-semibold text-gray-900 [&::-webkit-details-marker]:hidden">
						{q}
						<ChevronDownIcon className="h-4 w-4 shrink-0 text-gray-400 transition-transform group-open:rotate-180" />
					</summary>
					<p className="mt-3 text-sm leading-relaxed text-gray-600">{a}</p>
				</details>
			))}
		</div>
	);
}
