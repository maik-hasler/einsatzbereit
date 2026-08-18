import type {
	AnchorHTMLAttributes,
	ButtonHTMLAttributes,
	ReactNode,
} from "react";
import { Link, type LinkProps } from "react-router";

const SIZE_CLASSES = {
	sm: "px-3 py-1.5 text-xs",
	// min-h-10 (not a fixed h-10) matches the 40px height inputSurfaceClass
	// now guarantees for text inputs and selects (see formClasses.ts) so a
	// filter row's input/select/button trio lines up on one baseline instead
	// of drifting across three different heights (issue #1673) - min, not
	// fixed, so label text that wraps to two lines never gets clipped.
	md: "min-h-10 px-4 py-2 text-sm",
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
	"inline-flex items-center justify-center gap-1.5 transition-colors disabled:cursor-not-allowed disabled:opacity-50";

// rounded-xl is the default; `pill` swaps in rounded-full for the handful of
// pill-shaped CTAs (e.g. the hero search button sitting inside a pill-shaped
// search bar) - a real prop instead of an inline style override fighting
// BASE_CLASSES' own radius class at equal Tailwind specificity.
const SHAPE_CLASS = { default: "rounded-xl", pill: "rounded-full" } as const;

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
// success: solid positive-confirmation action (e.g. "Confirm" an engagement
// request), for the row-level counterpart to dangerOutline's cancel action -
// built on the brand ramp like every other variant here rather than raw
// Tailwind green, since brand-700 (#226947) already reads as "confirming
// green" (see issue #1673: two visibly different greens side by side in the
// organizer's core workflow before this existed). Same brand-700/800 pair as
// primary, not a lighter brand-600 - brand-600 under white text-xs text
// measures ~4.3:1, under axe-core's WCAG AA 4.5:1 floor (caught by
// AccessibilityTests.cs), while brand-700 (already proven at every primary
// button site) clears it comfortably.
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
	success: "bg-brand-700 font-semibold text-white hover:bg-brand-800",
	tertiary: "font-semibold text-brand-700 hover:bg-brand-50",
	// border-*-500, not the lighter -200 these used before (issue #2048):
	// -200 measures ~1.2-1.5:1 against a white/light background, well under
	// WCAG 1.4.11's 3:1 floor for a non-text UI-component boundary. -500
	// clears it comfortably (border-gray-500 ~4.8:1, border-red-500 ~3.8:1)
	// without touching the variant's text/hover colors.
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

// Plain <a href>, not a router Link - for in-page hash anchors (e.g. the
// footer's "/#for-organizations" link) and external URLs, where handing
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
