import { useEffect, useRef } from "react";
import type { RefObject } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import Button from "../Button";
import { FOCUSABLE_SELECTOR } from "../Modal";
import LanguageSelector from "./LanguageSelector";
import OrgAvatar from "./OrgAvatar";
import type { OrganizationSummaryDto } from "../../client/api-client";
import { ORG_TABS, orgTabPath } from "../../lib/orgTabs";
import { buildPrimaryNav } from "../../lib/headerNav";
import { useDismissableOverlay } from "../../hooks/useDismissableOverlay";
import { lockScroll } from "../../lib/scrollLock";

export default function MobileMenu({
	isTransparent,
	isLoggedIn,
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
	isTransparent: boolean;
	isLoggedIn: boolean;
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
			<button
				type="button"
				onClick={onClose}
				tabIndex={-1}
				aria-hidden="true"
				className="fixed top-[var(--header-height)] right-0 bottom-0 left-0 z-30 bg-black/50 lg:hidden"
			/>
			<div
				ref={panelRef}
				role="dialog"
				aria-modal="true"
				aria-label={t("nav.menu")}

				className={`absolute top-full right-0 left-0 z-30 max-h-[calc(100dvh-var(--header-height))] overflow-y-auto overscroll-contain border-t shadow-modal lg:hidden ${isTransparent ? "border-white/20 bg-brand-900" : "border-gray-100 bg-white"}`}
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
					<div
						className={`space-y-1 border-b pb-3 ${isTransparent ? "border-white/20" : "border-gray-100"}`}
					>
						{buildPrimaryNav(activeOrg).map((link) => {
							const rowBase = `rounded-lg px-3 py-2 text-sm font-medium transition-colors ${menuItemVariant}`;

							if (link.kind === "organization") {
								return (
									<div key={link.key}>
										<Link
											to={link.to}
											onClick={onClose}
											data-testid={`mobile-nav-${link.key}`}
											className={`${rowBase} flex items-center gap-2`}
										>
											<OrgAvatar
												name={link.org.name}
												logoUrl={link.org.logoUrl}
												size="sm"
											/>
											<span className="truncate">{link.org.name}</span>
										</Link>

										<div
											className={`ml-3 space-y-1 border-l pl-3 ${isTransparent ? "border-white/20" : "border-gray-200"}`}
										>
											{ORG_TABS.filter((tab) => tab.key !== "dashboard").map(
												(tab) => (
													<Link
														key={tab.key}
														to={orgTabPath(link.org.id, tab.key)}
														onClick={onClose}
														className={`block rounded-lg px-3 py-2 text-sm font-medium transition-colors ${isTransparent ? "text-white/80 hover:bg-white/10 hover:text-white" : "text-gray-600 hover:bg-brand-50 hover:text-brand-700"}`}
													>
														{t(tab.labelKey)}
													</Link>
												),
											)}
										</div>
									</div>
								);
							}

							return link.hash ? (
								<a
									key={link.key}
									href={link.to}
									onClick={onClose}
									data-testid={`mobile-nav-${link.key}`}
									className={`${rowBase} block`}
								>
									{t(`nav.${link.key}`)}
								</a>
							) : (
								<Link
									key={link.key}
									to={link.to}
									onClick={onClose}
									data-testid={`mobile-nav-${link.key}`}
									className={`${rowBase} block`}
								>
									{t(`nav.${link.key}`)}
								</Link>
							);
						})}
					</div>
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
									<div className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-700 text-sm font-semibold tracking-widest text-white">
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
								to="/my-signups"
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
							{isAdmin && (
								<Link
									to="/administration"
									onClick={onClose}
									className={`block rounded-lg px-3 py-2 text-sm font-medium transition-colors ${menuItemVariant}`}
								>
									{t("nav.administration")}
								</Link>
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
