import type { ReactNode } from "react";

interface Props {
	children: ReactNode;
}

const BASE_CLASSES =
	"mb-3 text-xs font-semibold tracking-widest text-brand-700 uppercase";

export default function SectionHeading({ children }: Props) {
	return <h2 className={BASE_CLASSES}>{children}</h2>;
}
