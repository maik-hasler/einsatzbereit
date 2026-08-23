import { useEffect, useRef } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { ORG_TABS, orgTabPath } from "../lib/orgTabs";
import { useQuickActionsList } from "../contexts/QuickActionsContext";
import { useScrollFade } from "../hooks/useScrollFade";
import Button from "./Button";
import { ChevronLeftIcon } from "./icons";

interface Props {
	organizationId: string;
	orgName: string;

	title: string;

	titleLang?: string;
	activeTabKey: string;

	back?: { href: string; label: string } | null;
}

export default function OrgPageHeader({
	organizationId,
	orgName,
	title,
	titleLang,
	activeTabKey,
	back,
}: Props) {
	const { t } = useTranslation();

	const actions = useQuickActionsList();

	const navRef = useRef<HTMLElement>(null);
	const { canScrollStart: canScrollLeft, canScrollEnd: canScrollRight } =
		useScrollFade(navRef, "x");

	const activeTabRef = useRef<HTMLAnchorElement>(null);
	useEffect(() => {
		const nav = navRef.current;
		const tab = activeTabRef.current;
		if (!nav || !tab) return;
		const tabStart = tab.offsetLeft;
		const tabEnd = tabStart + tab.offsetWidth;
		if (tabStart < nav.scrollLeft) {
			nav.scrollLeft = tabStart;
		} else if (tabEnd > nav.scrollLeft + nav.clientWidth) {
			nav.scrollLeft = tabEnd - nav.clientWidth;
		}
	}, [activeTabKey]);

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
				<div className="min-w-0">
					<p className="truncate text-xs font-semibold tracking-widest text-gray-500 uppercase">
						{orgName}
					</p>
					<h1
						lang={titleLang}
						className="font-display text-2xl font-bold tracking-tight text-gray-900 sm:text-3xl"
					>
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

			<div className="relative mt-4">
				<nav
					ref={navRef}
					aria-label={t("orgApp.sectionsNavLabel")}
					className="flex gap-1 overflow-x-auto border-b border-gray-200"
				>
					{ORG_TABS.map((tab) => {
						const isActive = tab.key === activeTabKey;
						return (
							<Link
								key={tab.key}
								ref={isActive ? activeTabRef : undefined}
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
				<div
					aria-hidden="true"
					className={`pointer-events-none absolute inset-y-0 left-0 w-8 bg-gradient-to-r from-gray-50 to-transparent transition-opacity duration-200 ${
						canScrollLeft ? "opacity-100" : "opacity-0"
					}`}
				/>
				<div
					aria-hidden="true"
					className={`pointer-events-none absolute inset-y-0 right-0 w-8 bg-gradient-to-l from-gray-50 to-transparent transition-opacity duration-200 ${
						canScrollRight ? "opacity-100" : "opacity-0"
					}`}
				/>
			</div>
		</div>
	);
}
