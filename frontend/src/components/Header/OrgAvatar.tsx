import { getInitials } from "../../lib/initials";

// An organization's logo, or its initials on a brand tile when it has none.
// The same eight-line pair of branches had been written out three times in
// this folder - the switcher's current-org button, the switcher's dropdown
// rows, and (since #1785) the header's top-level organization entry - so it
// lives here once instead, like the rest of the repo's shared primitives.
export default function OrgAvatar({
	name,
	logoUrl,
	size = "md",
	lazy = false,
}: {
	// Empty when the organization isn't resolved yet - getInitials renders "?"
	// for it, which is what the switcher shows before an org is picked.
	name: string;
	logoUrl?: string | null;
	// "sm" (20px) is the header nav entry's size: the top-level nav is width-
	// constrained at tablet widths (#1793), so every pixel there is spent on
	// the name instead. "md" (24px) is the org switcher's.
	size?: "sm" | "md";
	lazy?: boolean;
}) {
	const box = size === "sm" ? "h-5 w-5" : "h-6 w-6";

	if (logoUrl) {
		return (
			<img
				src={logoUrl}
				alt=""
				width={size === "sm" ? 20 : 24}
				height={size === "sm" ? 20 : 24}
				loading={lazy ? "lazy" : undefined}
				className={`${box} shrink-0 rounded-md object-cover`}
			/>
		);
	}

	return (
		<span
			className={`${box} flex shrink-0 items-center justify-center rounded-md bg-brand-100 text-xs font-semibold text-brand-700 before:content-[attr(data-initial)]`}
			aria-hidden="true"
			data-initial={getInitials(name)}
		/>
	);
}
