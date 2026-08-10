import type { ReactNode } from "react";

interface Props {
	children: ReactNode;
	/**
	 * Optional one-line explanation of what the section holds. Passed in rather
	 * than rendered by the caller so the heading and its own subtitle stay a
	 * single spacing unit - see the note below.
	 */
	description?: ReactNode;
}

// Shared page-level section heading (dashboard/admin pages carved into
// several named sections) - was hand-rolled slightly differently per page
// - see issue #1110.
//
// #1755 moved it onto --font-display at a larger step. At text-lg in the body
// face it sat only two steps above the body copy underneath it and in the very
// same typeface, so a page carved into several named sections didn't visibly
// read as carved into anything - the "no real sections" half of that issue.
// Barlow Condensed is narrow enough that text-2xl here occupies roughly the
// width text-lg did before.
const BASE_CLASSES = "font-display text-2xl font-bold text-gray-900";

export default function PageSectionHeading({ children, description }: Props) {
	// With a description, the gap has to live on the pair, not the heading:
	// callers that rendered their own <p> after this component got the
	// heading's mb-4 *plus* the paragraph's own top margin between the two,
	// which put a section's subtitle further from its heading than from the
	// content below it - so it read as a caption for the list rather than as
	// part of the heading (visible on OrgOpportunitiesPage's "Published").
	if (description) {
		return (
			<div className="mb-4">
				<h2 className={BASE_CLASSES}>{children}</h2>
				<p className="mt-1 text-sm text-gray-500">{description}</p>
			</div>
		);
	}

	return <h2 className={`mb-4 ${BASE_CLASSES}`}>{children}</h2>;
}
