import type { SelectHTMLAttributes } from "react";
import { ChevronDownIcon } from "./icons";
import { selectClass } from "../lib/formClasses";

// The one native <select> wrapper every dropdown filter/field should render
// through (#2225) - it draws the chevron as an inline <svg> layered on top of
// the control instead of a CSS `data:` background image, which the deployed
// CSP's img-src blocks (no `data:` there), leaving `appearance-none` selects
// with no visible arrow at all.
export default function Select({
	className = "",
	...rest
}: SelectHTMLAttributes<HTMLSelectElement>) {
	return (
		<div className="relative">
			<select
				className={`${selectClass}${className ? ` ${className}` : ""}`}
				{...rest}
			/>
			<ChevronDownIcon className="pointer-events-none absolute top-1/2 right-2 h-5 w-5 -translate-y-1/2 text-gray-500" />
		</div>
	);
}
