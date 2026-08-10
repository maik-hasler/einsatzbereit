import type { ReactNode } from "react";

interface Props {
	children: ReactNode;
}

// Shared small-caps subsection heading - the same "Badges" title rendered
// with different size/case/color depending on which page it appeared on
// (own profile overview vs. public profile) - see issue #1110.
//
// #1755 moved it onto the landing page's own eyebrow recipe (brand-700,
// tracking-widest) instead of text-gray-600/tracking-wider. Every section
// label on the marketing pages already looked like this; the app's own
// section labels were the odd ones out, and a low-contrast grey kicker is a
// large part of why those pages read as flat. brand-700 clears 7.5:1 on
// white, so this is a contrast improvement over the grey it replaces rather
// than a trade.
const BASE_CLASSES =
	"mb-3 text-xs font-semibold tracking-widest text-brand-700 uppercase";

export default function SectionHeading({ children }: Props) {
	return <h2 className={BASE_CLASSES}>{children}</h2>;
}
