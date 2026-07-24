import type { ButtonHTMLAttributes, ReactNode } from "react";
import { Link, type LinkProps } from "react-router";

const SIZE_CLASSES = {
	sm: "px-3 py-1.5 text-xs",
	md: "px-4 py-2 text-sm",
	lg: "px-6 py-3 text-base",
} as const;

type Size = keyof typeof SIZE_CLASSES;

// Shared shape/behavior every button/link in the app should share (see
// issue #846: four different border-radius values across primary buttons
// before this existed).
const BASE_CLASSES =
	"inline-flex items-center justify-center gap-1.5 rounded-xl transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-400/30 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50";

// primary: solid brand-color CTA. secondary: borderless cancel/close action -
// the single style every modal's cancel/close button should share (see
// issue #847: three different visual treatments for the same action before
// this existed).
const VARIANT_CLASSES = {
	primary: "bg-brand-700 font-semibold text-white hover:bg-brand-800",
	secondary: "text-gray-600 hover:bg-gray-100",
} as const;

type Variant = keyof typeof VARIANT_CLASSES;

interface CommonProps {
	variant?: Variant;
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
		variant = "primary",
		size = "md",
		fullWidth = false,
		className = "",
		children,
		...rest
	} = props;
	const classes = [
		BASE_CLASSES,
		VARIANT_CLASSES[variant],
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
