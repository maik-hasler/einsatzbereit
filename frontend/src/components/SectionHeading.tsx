import type { ReactNode } from "react";

interface Props {
	children: ReactNode;
}

// Shared small-caps subsection heading - the same "Badges" title rendered
// with different size/case/color depending on which page it appeared on
// (own profile overview vs. public profile) - see issue #1110.
const BASE_CLASSES =
	"mb-3 text-xs font-semibold tracking-wider text-gray-600 uppercase";

export default function SectionHeading({ children }: Props) {
	return <h2 className={BASE_CLASSES}>{children}</h2>;
}
