import { useEffect, useRef } from "react";
import type { RefObject } from "react";
import { useTranslation } from "react-i18next";
import { Link, NavLink } from "react-router";
import Button from "../Button";
import { FOCUSABLE_SELECTOR } from "../Modal";
import LanguageSelector from "./LanguageSelector";
import OrgAvatar from "../OrgAvatar";
import { SpinnerIcon } from "../Spinner";
import type { OrganizationSummaryDto } from "../../client/api-client";
import type { AuthDisplayStatus } from "../../hooks/useAuthDisplayStatus";
import { visibleOrgTabs, orgTabPath } from "../../lib/orgTabs";
import { buildPrimaryNav } from "../../lib/headerNav";
import { useDismissableOverlay } from "../../hooks/useDismissableOverlay";
import { lockScroll } from "../../lib/scrollLock";

export default function MobileMenu({
	className = "lg:hidden",
	isTransparent,
	authStatus,
	avatarUrl,
	initials,
	displayName,
	isAdmin,
	activeOrg,
	triggerRef,
	onClose,
	onSignIn,
	onRegister,
	onSignOut,
}: {
	className?: string;
	isTransparent: boolean;
	authStatus: AuthDisplayStatus;
	avatarUrl: string | null;
	initials: string;
	displayName: string;
	isAdmin: boolean;

	activeOrg: OrganizationSummaryDto | null | undefined;

	triggerRef: RefObject<HTMLButtonElement | null>;
	onClose: () => void;
	onSignIn: () => void;
	onRegister: () => void;
	onSignOut: () => void;
}) {
	const { t } = useTranslation();

	const menuItemVariant = isTransparent
		? "text-white/90 hover:bg-white/10 hover:text-white"
		: "text-gray-700 hover:bg-brand-50 hover:text-brand-700";

	const rootRef = useDismissableOverlay<HTMLDivElement>(true, onClose, [
		triggerRef,
	]);

	const panelRef = useRef<HTMLDivElement>(null);

	useEffect(() => {
		panelRef.current?.querySelector<HTMLElement>(FOCUSABLE_SELECTOR)?.focus();
	}, []);

	useEffect(() => lockScroll(), []);

	useEffect(() => {
		function handleKeyDown(e: KeyboardEvent) {
			if (e.key !== "Tab" || !panelRef.current) return;
			const panelFocusables = Array.from(
				panelRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR),
			).filter((el) => el.offsetParent !== null);
			// The toggle that opened this menu lives in the header, outside
			// panelRef, so it's included here explicitly - otherwise Tab/Shift+Tab
			// can cycle through the panel forever without ever reaching it.
			const focusables = triggerRef.current
				? [triggerRef.current, ...panelFocusables]
				: panelFocusables;
			if (focusables.length === 0) return;
			const first = focusables[0];
			const last = focusables[focusables.length - 1];
			if (e.shiftKey && document.activeElement === first) {
				e.preventDefault();
				last.focus();
			} else if (!e.shiftKey && document.activeElement === last) {
				e.preventDefault();
				first.focus();
			}
		}
		document.addEventListener("keydown", handleKeyDown);
		return () => document.removeEventListener("keydown", handleKeyDown);
	}, [triggerRef]);

	return (
		<div ref={rootRef}>
			<button
				type="button"
				onClick={onClose}
				tabIndex={-1}
				aria-hidden="true"
				className={`animate-fade-in fixed top-[var(--header-height)] right-0 bottom-0 left-0 z-30 bg-black/50 ${className}`}
			/>
			<div
				ref={panelRef}
				role="dialog"
				aria-modal="true"
				aria-label={t("nav.menu")}
				className={`animate-fade-up absolute top-full right-0 left-0 z-30 max-h-[calc(100dvh-var(--header-height))] overflow-y-auto overscroll-contain border-t shadow-modal ${className} ${isTransparent ? "border-white/20 bg-brand-900" : "border-gray-100 bg-white"}`}
			>
				{isTransparent && (
					<div
						className="pointer-events-none absolute inset-0 overflow-hidden"
						aria-hidden="true"
					>
						<div className="absolute -top-10 -left-20 h-64 w-64 rounded-full bg-brand-700 opacity-60 blur-3xl" />
						<div className="absolute -top-8 -right-16 h-48 w-48 rounded-full bg-brand-600 opacity-40 blur-3xl" />
					</div>
				)}
				<div className="relative space-y-1.5 px-3 py-3">
					<div
						className={`space-y-0.5 border-b pb-2 ${isTransparent ? "border-white/20" : "border-gray-100"}`}
					>
						{buildPrimaryNav(activeOrg).map((link) => {
							// 44px minimum: a drawer row is a phone's primary touch target and
							// these were 32px tall, under every touch-target guideline going
							// (#2327). The height comes from `min-h-11` rather than more padding
							// so the rows keep their compact rhythm.
							const rowBase = `flex min-h-11 items-center rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${menuItemVariant}`;
							// NavLink, not Link: the drawer renders the same five items as
							// the desktop nav, and used to mark none of them - no "you are
							// here" affordance below the `lg:` breakpoint, and no
							// `aria-current` on any route (#2329 F5). NavLink sets
							// `aria-current="page"` itself; `activeRow` is its visual half.
							const activeRow = isTransparent
								? "bg-white/15 font-semibold text-white"
								: "bg-brand-50 font-semibold text-brand-700";
							const rowClass = ({ isActive }: { isActive: boolean }) =>
								`${rowBase} ${isActive ? activeRow : ""}`;

							if (link.kind === "organization") {
								return (
									<div key={link.key}>
										{/* `end`, unlike the desktop nav's org link: this row is the
										dashboard link and its own section list sits right under it, so
										without it every section route prefix-matches the row too and
										the drawer claims two current pages. */}
										<NavLink
											to={link.to}
											end
											onClick={onClose}
											data-testid={`mobile-nav-${link.key}`}
											className={({ isActive }) =>
												`${rowClass({ isActive })} gap-2`
											}
										>
											<OrgAvatar
												name={link.org.name}
												logoUrl={link.org.logoUrl}
												size="sm"
											/>
											<span className="truncate">{link.org.name}</span>
										</NavLink>

										<div
											data-testid="mobile-nav-org-sections"
											className={`ml-3 space-y-0.5 border-l pl-3 ${isTransparent ? "border-white/20" : "border-gray-200"}`}
										>
											{visibleOrgTabs(link.org.role === "Organizer")
												.filter((tab) => tab.key !== "dashboard")
												.map((tab) => (
													<NavLink
														key={tab.key}
														to={orgTabPath(link.org.id, tab.key)}
														onClick={onClose}
														className={({ isActive }) =>
															`flex min-h-11 items-center rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${isActive ? activeRow : isTransparent ? "text-white/80 hover:bg-white/10 hover:text-white" : "text-gray-600 hover:bg-brand-50 hover:text-brand-700"}`
														}
													>
														{t(tab.labelKey)}
													</NavLink>
												))}
										</div>
									</div>
								);
							}

							return link.hash ? (
								// See DesktopHeader: a router <Link> so the fragment survives
								// the landing page's lazy mount (#2324).
								<Link
									key={link.key}
									to={link.to}
									onClick={onClose}
									data-testid={`mobile-nav-${link.key}`}
									className={rowBase}
								>
									{t(`nav.${link.key}`)}
								</Link>
							) : (
								<NavLink
									key={link.key}
									to={link.to}
									end={link.to === "/"}
									onClick={onClose}
									data-testid={`mobile-nav-${link.key}`}
									className={({ isActive }) => rowClass({ isActive })}
								>
									{t(`nav.${link.key}`)}
								</NavLink>
							);
						})}
					</div>
					<div className="pb-1.5">
						<LanguageSelector transparent={isTransparent} />
					</div>
					{authStatus === "signedIn" && (
						<div className="space-y-0.5">
							<div
								className={`mb-0.5 flex items-center gap-2.5 border-b px-3 pb-2 ${isTransparent ? "border-white/20" : "border-gray-100"}`}
							>
								{avatarUrl ? (
									<img
										src={avatarUrl}
										alt=""
										width={32}
										height={32}
										loading="lazy"
										className="h-8 w-8 rounded-full object-cover"
									/>
								) : (
									<div className="flex h-8 w-8 items-center justify-center rounded-full bg-brand-700 text-xs font-semibold tracking-widest text-white">
										{initials}
									</div>
								)}
								<span
									className={`text-sm font-medium ${isTransparent ? "text-white/90" : "text-gray-700"}`}
								>
									{displayName}
								</span>
							</div>
							<Link
								to="/profile"
								onClick={onClose}
								className={`flex min-h-11 items-center rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${menuItemVariant}`}
							>
								{t("nav.myProfile")}
							</Link>
							<Link
								to="/my-signups"
								onClick={onClose}
								className={`flex min-h-11 items-center rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${menuItemVariant}`}
							>
								{t("nav.myEngagements")}
							</Link>
							{isAdmin && (
								<Link
									to="/administration"
									onClick={onClose}
									className={`flex min-h-11 items-center rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${menuItemVariant}`}
								>
									{t("nav.administration")}
								</Link>
							)}
							<button
								type="button"
								onClick={onSignOut}
								className={`flex min-h-11 w-full items-center rounded-lg px-3 py-1.5 text-left text-sm font-medium transition-colors ${menuItemVariant}`}
							>
								{t("nav.signOut")}
							</button>
						</div>
					)}

					{authStatus === "signedOut" && (
						<div className="space-y-2">
							<Button
								type="button"
								onClick={onSignIn}
								variant={isTransparent ? "onDark" : "primary"}
								fullWidth
							>
								{t("nav.signIn")}
							</Button>
							<Button
								type="button"
								onClick={onRegister}
								variant={isTransparent ? "outlineOnDark" : "outline"}
								fullWidth
							>
								{t("nav.register")}
							</Button>
						</div>
					)}

					{authStatus === "pending" && (
						<div
							role="status"
							className={`flex items-center gap-2 px-3 py-2 text-sm ${isTransparent ? "text-white/70" : "text-gray-500"}`}
						>
							<SpinnerIcon
								className={`h-4 w-4 ${isTransparent ? "brightness-0 invert" : ""}`}
							/>
							<span>{t("nav.checkingSignIn")}</span>
						</div>
					)}

					{authStatus === "sessionExpired" && (
						<Button
							type="button"
							onClick={onSignIn}
							variant={isTransparent ? "onDark" : "primary"}
							fullWidth
						>
							{t("nav.sessionExpired")}
						</Button>
					)}
				</div>
			</div>
		</div>
	);
}
