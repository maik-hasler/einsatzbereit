import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { ORG_TABS, orgTabPath } from "../lib/orgTabs";
import { useQuickActionsList } from "../contexts/QuickActionsContext";
import Button from "./Button";
import { ChevronLeftIcon } from "./icons";

interface Props {
	organizationId: string;
	orgName: string;
	/** The current page's own name - the tab's label, or a nested page's title. */
	title: string;
	activeTabKey: string;
	/** Only set on a nested page (e.g. an opportunity's sign-ups), pointing at
	 * the tab that owns it. A tab page needs none: its own entry in the section
	 * rail below already says where it sits. */
	back?: { href: string; label: string } | null;
}

// The org app's page header. Deliberately NOT PageHeaderBand: that band is the
// public site's opening statement (brand-800 stage, 72px display type, blur
// blobs, a wave cap) and it costs ~400px before the first row of real content.
// On the public site that trade is right - a visitor arriving from the landing
// page is being introduced to a page. An organizer opening the app is not:
// they came to see what is going on in their organization right now, and every
// pixel spent restating "Dashboard" in display type is a pixel not spent on
// the widgets that answer that question.
//
// So this is app chrome, not marketing chrome: one line of identity, one line
// of title plus the page's own actions, and the section rail - roughly a
// quarter of the band's height, with navigation in the space the band used for
// atmosphere. It also stays out of HeaderOverlayContext (no useOverlaysHeader):
// nothing dark runs behind the header here, so the header keeps its normal
// opaque treatment.
export default function OrgPageHeader({
	organizationId,
	orgName,
	title,
	activeTabKey,
	back,
}: Props) {
	const { t } = useTranslation();
	// Same QuickActionsContext PageHeaderBand reads, with the same keys and
	// data-testids - the dashboard's Edit/Save/Cancel/Add-widget actions and
	// the opportunity tab's Create action land here unchanged, just in
	// on-light variants now that they no longer sit on brand-800.
	const actions = useQuickActionsList();

	return (
		<div data-testid="org-app-header" className="mb-6 sm:mb-8">
			{back && (
				<Link
					to={back.href}
					className="mb-2 -ml-1 inline-flex items-center gap-1 rounded-lg px-1 py-1 text-sm font-medium text-gray-500 transition-colors hover:text-brand-700"
				>
					<ChevronLeftIcon className="h-4 w-4" />
					{t("orgApp.backTo", { section: back.label })}
				</Link>
			)}
			<div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-3">
				{/* No logo mark here on purpose: the header's org switcher already
				names the organization, and anything sitting left of the title
				would push it off the left edge every page's content is aligned to.
				The eyebrow says which organization this is; the h1 says which of
				its pages you are on. */}
				<div className="min-w-0">
					<p className="truncate text-xs font-semibold tracking-widest text-gray-500 uppercase">
						{orgName}
					</p>
					<h1 className="font-display text-2xl font-bold tracking-tight text-gray-900 sm:text-3xl">
						{title}
					</h1>
				</div>
				{actions.length > 0 && (
					<div className="flex shrink-0 items-center gap-2">
						{actions.map((action) => (
							<Button
								key={action.key}
								type="button"
								onClick={action.onClick}
								disabled={action.disabled}
								title={action.title}
								aria-label={action.label}
								data-testid={`quick-action-${action.key}`}
								variant={action.variant === "primary" ? "primary" : "outline"}
								size="sm"
								className="shrink-0"
							>
								{action.icon}
								<span className="hidden sm:inline">{action.label}</span>
							</Button>
						))}
					</div>
				)}
			</div>

			{/* The org app's sections, in the app itself. Until now they were
			reachable only from the avatar dropdown's collapsible submenu or the
			mobile burger - so an organizer looking at their dashboard had no
			visible route to sign-ups, opportunities, members or settings at all.
			Horizontally scrollable rather than wrapped on narrow viewports, same
			pattern as the account area's SubNavRail below `lg`. */}
			<nav
				aria-label={t("orgApp.sectionsNavLabel")}
				className="mt-4 flex gap-1 overflow-x-auto border-b border-gray-200"
			>
				{ORG_TABS.map((tab) => {
					const isActive = tab.key === activeTabKey;
					return (
						<Link
							key={tab.key}
							to={orgTabPath(organizationId, tab.key)}
							aria-current={isActive ? "page" : undefined}
							data-testid={`org-tab-${tab.key}`}
							className={`shrink-0 border-b-2 px-3 py-2 text-sm whitespace-nowrap transition-colors ${
								isActive
									? "border-brand-600 font-semibold text-brand-700"
									: "border-transparent font-medium text-gray-500 hover:border-gray-300 hover:text-gray-900"
							}`}
						>
							{t(tab.labelKey)}
						</Link>
					);
				})}
			</nav>
		</div>
	);
}
