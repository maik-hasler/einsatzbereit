import type {
	AnchorHTMLAttributes,
	ButtonHTMLAttributes,
	ReactNode,
} from "react";
import { Link, type LinkProps } from "react-router";

const SIZE_CLASSES = {
	sm: "px-3 py-1.5 text-xs",

	md: "min-h-10 px-4 py-2 text-sm",
	lg: "px-6 py-3 text-base",
} as const;

type Size = keyof typeof SIZE_CLASSES;

const BASE_CLASSES =
	"inline-flex items-center justify-center gap-1.5 transition-colors disabled:cursor-not-allowed disabled:opacity-50";

const SHAPE_CLASS = { default: "rounded-xl", pill: "rounded-full" } as const;

const VARIANT_CLASSES = {
	primary: "bg-brand-700 font-semibold text-white hover:bg-brand-800",
	secondary: "text-gray-600 hover:bg-gray-100",
	danger: "bg-red-600 font-semibold text-white hover:bg-red-700",
	success: "bg-brand-700 font-semibold text-white hover:bg-brand-800",
	tertiary: "font-semibold text-brand-700 hover:bg-brand-50",

	dangerOutline: "border border-red-500 text-red-700 hover:bg-red-50",
	outline: "border border-gray-500 font-medium text-gray-700 hover:bg-gray-50",
	onDark: "bg-white font-semibold text-brand-800 hover:bg-brand-50",
	outlineOnDark:
		"border border-white/50 font-medium text-white hover:border-white hover:bg-white/10",
} as const;

type Variant = keyof typeof VARIANT_CLASSES;

interface CommonProps {
	variant?: Variant;
	size?: Size;
	fullWidth?: boolean;
	pill?: boolean;
	className?: string;
	children: ReactNode;
}

type ButtonAsButton = CommonProps &
	Omit<ButtonHTMLAttributes<HTMLButtonElement>, "className"> & {
		to?: undefined;
		href?: undefined;
	};

type ButtonAsLink = CommonProps &
	Omit<LinkProps, "className"> & { href?: undefined };

type ButtonAsAnchor = CommonProps &
	Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "className"> & {
		href: string;
		to?: undefined;
	};

type ButtonProps = ButtonAsButton | ButtonAsLink | ButtonAsAnchor;

export default function Button(props: ButtonProps) {
	const {
		variant = "primary",
		size = "md",
		fullWidth = false,
		pill = false,
		className = "",
		children,
		...rest
	} = props;
	const classes = [
		BASE_CLASSES,
		SHAPE_CLASS[pill ? "pill" : "default"],
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

	if ("href" in rest && rest.href !== undefined) {
		return (
			<a
				className={classes}
				{...(rest as Omit<
					AnchorHTMLAttributes<HTMLAnchorElement>,
					"className"
				>)}
			>
				{children}
			</a>
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
