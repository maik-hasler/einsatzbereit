import type { ReactNode } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { WAVE_PATH } from "../lib/wavePath";
import { useOverlaysHeader } from "../contexts/HeaderOverlayContext";
import { useQuickActionsList } from "../contexts/QuickActionsContext";
import Button from "./Button";
import { ChevronLeftIcon } from "./icons";

interface Props {
	/**
	 * Small uppercase kicker above the title - names the page's category.
	 * ReactNode, not string: the opportunity page puts a link to the owning
	 * organization here, which is the one piece of chrome that used to live in
	 * the breadcrumb and has nowhere else to go once the bar is gone.
	 */
	eyebrow: ReactNode;
	title: string;
	/** Optional one-line standfirst under the title. */
	lead?: string;
	/** Optional trailing row (a "last updated" chip, a CTA). */
	children?: ReactNode;
}

// Shared title band for the standalone public pages (help, contact, imprint,
// legal texts). Those pages previously opened with a text-2xl <h1> on plain
// white, so a visitor arriving from the landing page's footer landed on
// something that shared none of its visual language - no display face, no
// brand surface, no wave motif (see issue #1755).
//
// The band deliberately reuses what the landing page already established
// rather than inventing a second system for subpages: brand-800 stage and
// blur-blob lighting from the hero, WAVE_PATH bottom cap from the org-CTA and
// founder bands, uppercase brand-200 eyebrow from every section heading there.
//
// Layout escapes, because AppLayout's <main> is a padded, max-w-page column
// and this has to sit edge-to-edge and run *behind* the header:
//
//   - `left-1/2 w-screen -translate-x-1/2` breaks out horizontally, the same
//     pattern HomePage's bands use (safe because global.css sets
//     html { overflow-x: clip }).
//   - The negative top margin cancels both <main>'s own top padding and the
//     flow space the sticky header occupies, sliding the band up underneath
//     it. The header keeps its z-40, so it paints over the band rather than
//     being covered by it, and useOverlaysHeader below tells it to go
//     transparent while that's true. --header-height is added back as top
//     padding so the eyebrow doesn't start underneath the header bar.
//
// These pages carry no BreadcrumbBar (the pages using this band stopped
// calling usePageToolbar in #1755): a separate grey bar restating the page
// title directly above a band that states it in 72px display type was pure
// duplication, and it drove a hard white line straight through the middle of
// the treatment. The way back home is the link inside the band instead.
export default function PageHeaderBand({
	eyebrow,
	title,
	lead,
	children,
}: Props) {
	const { t } = useTranslation();
	useOverlaysHeader();
	// Same QuickActionsContext the BreadcrumbBar reads. A page using this band
	// renders no action bar (see the note above), so without this its
	// usePageToolbar-published actions - the profile page's Edit/Save/Cancel -
	// would have nowhere to go. Same keys and data-testids as BreadcrumbBar's
	// buttons, on-dark variants because this sits on brand-800.
	const actions = useQuickActionsList();

	return (
		<div className="relative left-1/2 -mt-[calc(var(--header-height)+var(--main-top-padding))] mb-12 w-screen -translate-x-1/2 sm:mb-16">
			<div className="relative isolate overflow-hidden bg-brand-800">
				<div
					aria-hidden="true"
					className="pointer-events-none absolute -top-32 -left-24 h-80 w-80 rounded-full bg-brand-700 opacity-60 blur-3xl"
				/>
				<div
					aria-hidden="true"
					className="pointer-events-none absolute -right-20 -bottom-32 h-72 w-72 rounded-full bg-accent-400 opacity-10 blur-3xl"
				/>

				{/* Two nested constraints, mirroring exactly what the band sits
				inside: AppLayout's <main> (max-w-page + its responsive px-*),
				then the max-w-5xl column each consuming page centres within
				that. Reproducing both is what puts the title on the same left
				edge as the text it heads - collapsing them into a single
				max-w-5xl + px-8 lands 32px off, and dropping to max-w-page
				alone lands ~175px off. The band's *background* still runs edge
				to edge; only its text is brought into the document measure. */}
				<div className="relative mx-auto max-w-page px-4 sm:px-6 lg:px-8">
					{/* Vertical padding tightened from pt+3rem/pb-20. At the old
					values the band ran ~420px tall to hold an eyebrow, a title and
					at most two lines of lead - so on /help, /contact and the account
					pages roughly 60% of the tallest, darkest surface on the page was
					empty. The type scale is unchanged; only the air around it is. */}
					<div className="mx-auto max-w-5xl pt-[calc(var(--header-height)+1.5rem)] pb-10 sm:pt-[calc(var(--header-height)+2rem)] sm:pb-14">
						{/* Replaces the BreadcrumbBar these pages used to render - one
						way back, in the band, instead of a grey strip above it
						repeating the title. */}
						<Link
							to="/"
							className="animate-fade-up -ml-1 inline-flex items-center gap-1 rounded-lg px-1 py-1 text-sm font-medium text-brand-100 transition-colors hover:text-white"
						>
							<ChevronLeftIcon className="h-4 w-4" />
							{t("breadcrumb.home")}
						</Link>
						{actions.length > 0 && (
							<div className="animate-fade-up-d1 float-right ml-4 flex shrink-0 items-center gap-2">
								{actions.map((action) => (
									<Button
										key={action.key}
										type="button"
										onClick={action.onClick}
										disabled={action.disabled}
										title={action.title}
										aria-label={action.label}
										data-testid={`quick-action-${action.key}`}
										variant={
											action.variant === "primary" ? "onDark" : "outlineOnDark"
										}
										className="shrink-0"
									>
										{action.icon}
										<span className="hidden sm:inline">{action.label}</span>
									</Button>
								))}
							</div>
						)}
						<p className="animate-fade-up mt-6 text-xs font-semibold tracking-widest text-brand-200 uppercase">
							{eyebrow}
						</p>
						<h1 className="animate-fade-up-d1 mt-3 max-w-4xl font-display text-5xl font-bold tracking-tight text-white sm:text-6xl lg:text-7xl">
							{title}
						</h1>
						{lead && (
							<p className="animate-fade-up-d2 mt-5 max-w-2xl text-base leading-relaxed text-brand-100 sm:text-lg">
								{lead}
							</p>
						)}
						{children && (
							<div className="animate-fade-up-d3 mt-6">{children}</div>
						)}
					</div>
				</div>
			</div>

			{/* Bottom cap - rotated so the fill sits above the wavy edge, fading
			this band's own brand-800 into the white page below (same direction
			the founder band's closing cap uses on HomePage). */}
			<svg
				aria-hidden="true"
				viewBox="0 0 1440 60"
				preserveAspectRatio="none"
				className="block h-8 w-full rotate-180 text-brand-800 sm:h-12"
			>
				<path d={WAVE_PATH} fill="currentColor" />
			</svg>
		</div>
	);
}
