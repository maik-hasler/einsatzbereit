import type {
	AnchorHTMLAttributes,
	ButtonHTMLAttributes,
	ReactNode,
} from "react";
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
// Focus-visible styling comes from the global :focus-visible ring in
// global.css (issue #992) - not from a per-component outline-none/ring
// pair, which had too little contrast on this shared component.
const BASE_CLASSES =
	"inline-flex items-center justify-center gap-1.5 rounded-xl transition-colors disabled:cursor-not-allowed disabled:opacity-50";

// primary: solid brand-color CTA. secondary: borderless cancel/close action -
// the single style every modal's cancel/close button should share (see
// issue #847: three different visual treatments for the same action before
// this existed). tertiary: borderless brand-color action for a secondary CTA
// alongside a primary one (e.g. "Save draft" next to "Publish") - distinct
// from `secondary` since it needs to read as an action, not a dismissal.
// danger: solid destructive action, for confirmation dialogs. dangerOutline:
// bordered destructive action, for in-row/panel destructive actions (see
// issue #1105: eight different visual treatments for delete/withdraw/
// cancel/revoke actions before these two existed).
// outline: outlined, muted action for a light background - the
// action-bar/breadcrumb secondary buttons (Cancel etc.) and header
// sign-in/register pair. onDark/outlineOnDark: solid/outlined counterparts
// of primary/outline for use on a brand-800 surface (hero, transparent
// header) - see issue #1102: the header sign-in/register pair and the
// hero/mission CTAs hand-rolled their own one-off classes instead of
// routing through here, drifting to rounded-lg and font-medium.
const VARIANT_CLASSES = {
	primary: "bg-brand-700 font-semibold text-white hover:bg-brand-800",
	secondary: "text-gray-600 hover:bg-gray-100",
	danger: "bg-red-600 font-semibold text-white hover:bg-red-700",
	tertiary: "font-semibold text-brand-700 hover:bg-brand-50",
	dangerOutline: "border border-red-200 text-red-700 hover:bg-red-50",
	outline: "border border-gray-200 font-medium text-gray-700 hover:bg-gray-50",
	onDark: "bg-white font-semibold text-brand-800 hover:bg-brand-50",
	outlineOnDark:
		"border border-white/50 font-medium text-white hover:border-white hover:bg-white/10",
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
		href?: undefined;
	};

type ButtonAsLink = CommonProps &
	Omit<LinkProps, "className"> & { href?: undefined };

// Plain <a href>, not a router Link - for in-page hash anchors (e.g. the
// hero CTA scrolling to #opportunities) and external URLs, where handing
// navigation to react-router's Link would change how same-page hash
// scrolling behaves.
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
