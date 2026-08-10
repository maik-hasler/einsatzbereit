import type { ReactNode } from "react";

interface Props {
	/** Anchor target - must match the matching <DocumentOutline> entry. */
	id: string;
	/** Clause number shown above the title. Omit for unnumbered sections. */
	number?: number;
	title: string;
	children: ReactNode;
}

// One clause of a legal document (terms, privacy policy). Shared by both so
// the two texts can't drift into different heading scales and paragraph
// rhythms, which is what they had done before issue #1755 - h2 at text-xl in
// the body face, a uniform mb-8 between every section, and no separator, so
// nine sections read as one undifferentiated column.
//
// The clause number is real content, not decoration: German legal texts are
// cited by number. It therefore lives inside the <h2> rather than beside it,
// so the heading's accessible name is "1 Scope" and a screen-reader user can
// cite the same clause a sighted reader can - no aria-hidden flourish and no
// separate sr-only string to translate.
//
// brand-700 (7.5:1 on white), not the accent gold: gold at ~2:1 would fail the
// text contrast floor, and spending the page's one accent colour once per
// clause would drown out the single gold note in the header band above.
//
// scroll-mt clears the sticky site header so an anchor jump doesn't park the
// heading underneath it.
export default function DocumentSection({
	id,
	number,
	title,
	children,
}: Props) {
	return (
		<section
			id={id}
			aria-labelledby={`${id}-title`}
			className="scroll-mt-28 border-t border-gray-200 pt-10 first:border-t-0 first:pt-0"
		>
			<h2 id={`${id}-title`} className="font-display text-gray-900">
				{number !== undefined && (
					<span className="block text-2xl font-bold text-brand-700 tabular-nums">
						{number}
					</span>
				)}
				<span className="mt-1 block text-3xl font-bold sm:text-4xl">
					{title}
				</span>
			</h2>
			<div className="mt-5 space-y-4 text-base leading-7 text-gray-700">
				{children}
			</div>
		</section>
	);
}
