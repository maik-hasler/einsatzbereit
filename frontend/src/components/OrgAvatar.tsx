import { getInitials } from "../lib/initials";

const SIZE_CLASSES = {
	sm: "h-5 w-5 text-xs",
	md: "h-6 w-6 text-xs",
	lg: "h-7 w-7 text-xs",
	xl: "h-10 w-10 text-sm",
	"2xl": "h-12 w-12 text-lg",
	"3xl": "h-16 w-16 text-2xl",
} as const;

const PIXEL_SIZES: Record<keyof typeof SIZE_CLASSES, number> = {
	sm: 20,
	md: 24,
	lg: 28,
	xl: 40,
	"2xl": 48,
	"3xl": 64,
};

export default function OrgAvatar({
	name,
	logoUrl,
	size = "md",
	lazy = false,
}: {
	name: string;
	logoUrl?: string | null;

	size?: keyof typeof SIZE_CLASSES;
	lazy?: boolean;
}) {
	const box = SIZE_CLASSES[size];
	const pixelSize = PIXEL_SIZES[size];

	if (logoUrl) {
		return (
			<img
				src={logoUrl}
				alt=""
				width={pixelSize}
				height={pixelSize}
				loading={lazy ? "lazy" : undefined}
				className={`${box} shrink-0 rounded-full object-cover`}
			/>
		);
	}

	return (
		<span
			aria-hidden="true"
			className={`${box} flex shrink-0 items-center justify-center rounded-full bg-brand-100 font-semibold text-brand-700`}
		>
			{getInitials(name)}
		</span>
	);
}
