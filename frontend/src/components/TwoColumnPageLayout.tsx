import type { ReactNode } from "react";

// The sidebar+content grid every "sub-page with a rail" route uses - legal
// docs pair a wider outline column with more breathing room, the app's own
// sub-nav pages (Administration, MyEngagements, ProfileOverview) share a
// narrower rail. Both recipes were hand-copied into five page files with the
// two widths never drifting from each other, but nothing stopped a sixth page
// from introducing a third gap/width combination. One component, so there is
// exactly one place to change either recipe.
const VARIANT_CLASSES = {
	documentOutline: "gap-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:gap-16",
	subNav: "gap-8 lg:grid-cols-[11rem_minmax(0,1fr)] lg:gap-12",
} as const;

interface TwoColumnPageLayoutProps {
	variant: keyof typeof VARIANT_CLASSES;
	sidebar: ReactNode;
	/** Extra classes for the content column, e.g. `space-y-10` between sections. */
	contentClassName?: string;
	children: ReactNode;
}

export default function TwoColumnPageLayout({
	variant,
	sidebar,
	contentClassName = "",
	children,
}: TwoColumnPageLayoutProps) {
	return (
		<div
			data-content-wrapper
			className={`mx-auto grid max-w-5xl ${VARIANT_CLASSES[variant]}`}
		>
			{sidebar}
			<div className={`min-w-0 ${contentClassName}`.trim()}>{children}</div>
		</div>
	);
}
