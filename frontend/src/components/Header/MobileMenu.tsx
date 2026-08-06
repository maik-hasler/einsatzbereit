import { useEffect, useRef } from "react";
import type { Dispatch, RefObject, SetStateAction } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import Button from "../Button";
import { FOCUSABLE_SELECTOR } from "../Modal";
import LanguageSelector from "./LanguageSelector";
import type { OrganizationSummaryDto } from "../../client/api-client";
import { ORG_TABS, orgTabPath } from "../../lib/orgTabs";
import { runtimeConfig } from "../../lib/runtimeConfig";
import { useDismissableOverlay } from "../../hooks/useDismissableOverlay";
import { ChevronDownIcon } from "../icons";

// Mobile menu overlay (absolute-positioned so it doesn't push content down),
// toggled open by MobileHeader's burger button.
export default function MobileMenu({
	isTransparent,
	isLoggedIn,
	avatarUrl,
	initials,
	displayName,
	isAdmin,
	activeOrg,
	orgMenuOpen,
	setOrgMenuOpen,
	triggerRef,
	onClose,
	onSignIn,
	onRegister,
	onSignOut,
}: {
	isTransparent: boolean;
	isLoggedIn: boolean;
	avatarUrl: string | null;
	initials: string;
	displayName: string;
	isAdmin: boolean;
	activeOrg: OrganizationSummaryDto | null | undefined;
	orgMenuOpen: boolean;
	setOrgMenuOpen: Dispatch<SetStateAction<boolean>>;
	// The hamburger button that toggles this menu open/closed - rendered as a
	// sibling in MobileHeader, not a descendant here. Without treating it as
	// "inside", the outside-click check below would see the very click that
	// opens this menu as an outside click and immediately close it again.
	triggerRef: RefObject<HTMLButtonElement | null>;
	onClose: () => void;
	onSignIn: () => void;
	onRegister: () => void;
	onSignOut: () => void;
}) {
	const { t } = useTranslation();
	// Shared by the profile link, admin link, and org-menu toggle below - the
	// only exact repeat of this variant in the file (other isTransparent
	// ternaries here use their own one-off colors).
	const menuItemVariant = isTransparent
		? "text-white/90 hover:bg-white/10 hover:text-white"
		: "text-gray-700 hover:bg-brand-50 hover:text-brand-600";
	// Only ever mounted while open (see Header.tsx), so dismissal listeners
	// attach for this component's entire lifetime.
	const rootRef = useDismissableOverlay<HTMLDivElement>(true, onClose, [
		triggerRef,
	]);
	// Scoped to the panel itself (not the scrim below) - both the initial
	// focus and the Tab trap need to search only the real dialog content.
	const panelRef = useRef<HTMLDivElement>(null);

	// Move focus into the panel on open, mirroring Modal.tsx's initial-focus
	// behavior - without this, a keyboard user who opens the menu stays
	// focused on the (now expanded) hamburger button and has to Tab past it
	// again to reach the first item.
	useEffect(() => {
		panelRef.current?.querySelector<HTMLElement>(FOCUSABLE_SELECTOR)?.focus();
	}, []);

	// Body scroll lock - without this, the page behind the scrim (#1672) keeps
	// scrolling under a touch drag, which both feels broken and can scroll the
	// open menu itself out of view since it's positioned in flow, not fixed.
	useEffect(() => {
		const previousOverflow = document.body.style.overflow;
		document.body.style.overflow = "hidden";
		return () => {
			document.body.style.overflow = previousOverflow;
		};
	}, []);

	// Tab focus trap, mirroring Modal.tsx's - Escape is already handled by
	// useDismissableOverlay above, so this only needs to own Tab. Without it,
	// Tab/Shift+Tab walks straight past the panel into whatever's behind the
	// scrim (#1672 - "71 focusables remain reachable behind the open menu").
	useEffect(() => {
		function handleKeyDown(e: KeyboardEvent) {
			if (e.key !== "Tab" || !panelRef.current) return;
			const focusables = Array.from(
				panelRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR),
			).filter((el) => el.offsetParent !== null);
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
	}, []);

	return (
		<div ref={rootRef}>
			{/* Scrim: dims and blocks interaction with whatever's behind the panel
			(the hero on the homepage, in particular) - separates the backdrop-
			button from the dialog container per the repo's modal a11y convention
			(see Modal.tsx), even though this dialog isn't portaled like Modal is.
			Starts below --header-height rather than the full viewport, so the
			header bar itself (its burger/bell buttons included) stays undimmed
			and directly clickable instead of hiding under the scrim. */}
			<button
				type="button"
				onClick={onClose}
				tabIndex={-1}
				aria-hidden="true"
				className="fixed top-[var(--header-height)] right-0 bottom-0 left-0 z-30 bg-black/50 md:hidden"
			/>
			<div
				ref={panelRef}
				role="dialog"
				aria-modal="true"
				aria-label={t("nav.menu")}
				// max-h + overflow-y-auto so the body scroll lock above doesn't
				// strand content taller than the viewport with no way to reach it
				// (e.g. an organizer with the org submenu expanded on a short
				// landscape-phone viewport) - overscroll-contain keeps a drag past
				// this panel's own scroll bounds from rubber-banding the (locked)
				// body underneath.
				className={`absolute top-full right-0 left-0 z-30 max-h-[calc(100dvh-var(--header-height))] overflow-y-auto overscroll-contain border-t shadow-modal md:hidden ${isTransparent ? "border-white/20 bg-brand-900" : "border-gray-100 bg-white"}`}
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
				<div className="relative space-y-2 px-4 py-4">
					<div className="pb-2">
						<LanguageSelector transparent={isTransparent} />
					</div>
					{isLoggedIn ? (
						<div className="space-y-1">
							<div className="flex items-center gap-3 px-3 py-2">
								{avatarUrl ? (
									<img
										src={avatarUrl}
										alt=""
										width={36}
										height={36}
										loading="lazy"
										className="h-9 w-9 rounded-full object-cover"
									/>
								) : (
									<div className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-700 text-sm font-semibold text-white">
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
								className={`block rounded-lg px-3 py-2 text-sm font-medium transition-colors ${menuItemVariant}`}
							>
								{t("nav.myProfile")}
							</Link>
							<Link
								to="/my-engagements"
								onClick={onClose}
								className={`block rounded-lg px-3 py-2 text-sm font-medium transition-colors ${menuItemVariant}`}
							>
								{t("nav.myEngagements")}
							</Link>
							<Link
								to="/profile/settings"
								onClick={onClose}
								className={`block rounded-lg px-3 py-2 text-sm font-medium transition-colors ${menuItemVariant}`}
							>
								{t("nav.profileSettings")}
							</Link>
							<a
								href={`${runtimeConfig.keycloakAuthorityUrl}/account`}
								target="_blank"
								rel="noopener noreferrer"
								onClick={onClose}
								className={`block rounded-lg px-3 py-2 text-sm font-medium transition-colors ${menuItemVariant}`}
							>
								{t("nav.accountSettings")}
							</a>
							{isAdmin && (
								<Link
									to="/administration"
									onClick={onClose}
									className={`block rounded-lg px-3 py-2 text-sm font-medium transition-colors ${menuItemVariant}`}
								>
									{t("nav.administration")}
								</Link>
							)}
							{activeOrg && (
								<div>
									<button
										type="button"
										onClick={() => setOrgMenuOpen((o) => !o)}
										aria-expanded={orgMenuOpen}
										className={`flex w-full items-center justify-between rounded-lg px-3 py-2 text-sm font-medium transition-colors ${menuItemVariant}`}
									>
										{t("nav.organization")}
										<ChevronDownIcon
											open={orgMenuOpen}
											className="h-4 w-4 shrink-0"
										/>
									</button>
									{orgMenuOpen && (
										<div
											className={`ml-3 space-y-1 border-l pl-3 ${isTransparent ? "border-white/20" : "border-gray-200"}`}
										>
											{ORG_TABS.map((tab) => (
												<Link
													key={tab.key}
													to={orgTabPath(activeOrg.id, tab.key)}
													onClick={onClose}
													className={`block rounded-lg px-3 py-2 text-sm font-medium transition-colors ${isTransparent ? "text-white/80 hover:bg-white/10 hover:text-white" : "text-gray-600 hover:bg-brand-50 hover:text-brand-600"}`}
												>
													{t(tab.labelKey)}
												</Link>
											))}
										</div>
									)}
								</div>
							)}
							<button
								type="button"
								onClick={onSignOut}
								className={`block w-full rounded-lg px-3 py-2 text-left text-sm font-medium transition-colors ${isTransparent ? "text-red-400 hover:bg-white/10 hover:text-red-300" : "text-red-600 hover:bg-red-50 hover:text-red-700"}`}
							>
								{t("nav.signOut")}
							</button>
						</div>
					) : (
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
				</div>
			</div>
		</div>
	);
}
