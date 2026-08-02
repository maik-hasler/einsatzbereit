import { Fragment } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import type { BreadcrumbItem } from "../../contexts/ToolbarContext";
import type { QuickAction } from "../../contexts/QuickActionsContext";
import { HomeIcon } from "../icons";

// Icon-led action bar rendered directly beneath <header> (a sibling, not a
// descendant - visually attached to the header but not part of it). Home
// icon links to homeHref; items chain after it with `>` separators, and the
// last item is always the current page (plain text, no link, aria-current).
// This is the single implementation both the org app shell (via its
// orgSwitcher-style breadcrumb prop) and the public site (via
// usePageToolbar, see ToolbarContext.tsx) render through - see Header's
// `breadcrumb` prop. `actions` (set by any page under either shell via
// useQuickActions, see QuickActionsContext.tsx - both AppLayout.tsx and
// OrgAppLayout.tsx wrap their pages in QuickActionsProvider) render
// right-aligned next to the breadcrumb - icon+label on desktop, icon-only
// (label becomes the button's aria-label) below the `sm` breakpoint.
export default function BreadcrumbBar({
	homeHref,
	items,
	actions,
}: {
	homeHref: string;
	items: BreadcrumbItem[];
	actions?: QuickAction[];
}) {
	const { t } = useTranslation();
	return (
		<div className="border-b border-gray-200 bg-white">
			<div className="mx-auto flex max-w-7xl items-center justify-between gap-3 px-4 py-3 sm:px-6 lg:px-8">
				<nav
					aria-label={t("breadcrumb.label")}
					className="flex min-w-0 items-center gap-1.5 text-sm"
				>
					<Link
						to={homeHref}
						aria-label={t("breadcrumb.home")}
						className="flex shrink-0 items-center text-gray-400 transition-colors hover:text-brand-700"
					>
						<HomeIcon className="h-4 w-4" />
					</Link>
					{items.map((item, index) => {
						const isLast = index === items.length - 1;
						return (
							<Fragment key={item.href ?? item.label}>
								<span className="shrink-0 text-gray-300" aria-hidden="true">
									&rsaquo;
								</span>
								{isLast ? (
									<span
										className="truncate font-medium text-gray-900"
										aria-current="page"
									>
										{item.label}
									</span>
								) : item.href !== undefined ? (
									<Link
										to={item.href}
										className="shrink-0 truncate font-medium text-gray-500 transition-colors hover:text-brand-700"
									>
										{item.label}
									</Link>
								) : (
									<span className="shrink-0 truncate font-medium text-gray-500">
										{item.label}
									</span>
								)}
							</Fragment>
						);
					})}
				</nav>
				{actions && actions.length > 0 && (
					<div className="flex shrink-0 items-center gap-2">
						{actions.map((action) => (
							<button
								key={action.key}
								type="button"
								onClick={action.onClick}
								disabled={action.disabled}
								title={action.title}
								aria-label={action.label}
								data-testid={`quick-action-${action.key}`}
								className={`inline-flex shrink-0 items-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${
									action.variant === "primary"
										? "bg-brand-700 font-semibold text-white shadow-sm hover:bg-brand-800"
										: "border border-gray-200 text-gray-700 hover:bg-gray-50"
								}`}
							>
								{action.icon}
								<span className="hidden sm:inline">{action.label}</span>
							</button>
						))}
					</div>
				)}
			</div>
		</div>
	);
}
