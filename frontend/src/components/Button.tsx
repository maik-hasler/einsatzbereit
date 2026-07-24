import type { ButtonHTMLAttributes, ReactNode } from "react";
import { Link, type LinkProps } from "react-router";

const SIZE_CLASSES = {
	sm: "px-3 py-1.5 text-xs",
	md: "px-4 py-2 text-sm",
	lg: "px-6 py-3 text-base",
} as const;

type Size = keyof typeof SIZE_CLASSES;

// Solid brand-color primary CTA - the single style every "call to action"
// button/link in the app should share (see issue #846: four different
// border-radius values across primary buttons before this existed).
const BASE_CLASSES =
	"inline-flex items-center justify-center gap-1.5 rounded-xl bg-brand-700 font-semibold text-white transition-colors hover:bg-brand-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-400/30 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50";

interface CommonProps {
	size?: Size;
	fullWidth?: boolean;
	className?: string;
	children: ReactNode;
}

type ButtonAsButton = CommonProps &
	Omit<ButtonHTMLAttributes<HTMLButtonElement>, "className"> & {
		to?: undefined;
	};

type ButtonAsLink = CommonProps & Omit<LinkProps, "className">;

type ButtonProps = ButtonAsButton | ButtonAsLink;

export default function Button(props: ButtonProps) {
	const {
		size = "md",
		fullWidth = false,
		className = "",
		children,
		...rest
	} = props;
	const classes = [
		BASE_CLASSES,
		SIZE_CLASSES[size],
		fullWidth && "w-full",
		className,
	]
		.filter(Boolean)
		.join(" ");

	if ("to" in rest && rest.to !== undefined) {
		return (
			<Link className={classes} {...(rest as Omit<LinkProps, "className">)}>
				{children}
			</Link>
		);
	}

	return (
		<button
			className={classes}
			{...(rest as Omit<ButtonHTMLAttributes<HTMLButtonElement>, "className">)}
		>
			{children}
		</button>
	);
}
