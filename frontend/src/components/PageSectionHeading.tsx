import type { ReactNode } from "react";

interface Props {
	children: ReactNode;
}

// Shared page-level section heading (dashboard/admin pages carved into
// several named sections) - was hand-rolled slightly differently per page
// - see issue #1110.
const BASE_CLASSES = "mb-4 text-lg font-semibold text-gray-900";

export default function PageSectionHeading({ children }: Props) {
	return <h2 className={BASE_CLASSES}>{children}</h2>;
}
