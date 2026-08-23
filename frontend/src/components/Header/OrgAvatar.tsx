import { getInitials } from "../../lib/initials";

export default function OrgAvatar({
	name,
	logoUrl,
	size = "md",
	lazy = false,
}: {
	name: string;
	logoUrl?: string | null;

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
