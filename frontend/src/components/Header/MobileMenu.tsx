import type { Dispatch, RefObject, SetStateAction } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import LanguageSelector from "./LanguageSelector";
import type { OrganizationSummaryDto } from "../../client/api-client";
import { ORG_TABS, orgTabPath } from "../../lib/orgTabs";
import { useDismissableOverlay } from "../../hooks/useDismissableOverlay";

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

	return (
		<div
			ref={rootRef}
			className={`absolute left-0 right-0 top-full border-t md:hidden shadow-lg ${isTransparent ? "border-white/20 bg-brand-800" : "border-gray-100 bg-white"}`}
		>
			{isTransparent && (
				<div
					className="pointer-events-none absolute inset-0 overflow-hidden"
					aria-hidden="true"
				>
					<div className="absolute -left-20 -top-10 h-64 w-64 rounded-full bg-brand-700 opacity-60 blur-3xl" />
					<div className="absolute -right-16 -top-8 h-48 w-48 rounded-full bg-brand-600 opacity-40 blur-3xl" />
				</div>
			)}
			<div className="relative px-4 py-4 space-y-2">
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
									className="w-9 h-9 rounded-full object-cover"
								/>
							) : (
								<div className="w-9 h-9 rounded-full bg-brand-700 text-white flex items-center justify-center text-sm font-semibold">
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
							className={`block px-3 py-2 rounded-lg text-sm font-medium transition-colors ${menuItemVariant}`}
						>
							{t("nav.myProfile")}
						</Link>
						{isAdmin && (
							<Link
								to="/administration"
								onClick={onClose}
								className={`block px-3 py-2 rounded-lg text-sm font-medium transition-colors ${menuItemVariant}`}
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
									className={`flex w-full items-center justify-between px-3 py-2 rounded-lg text-sm font-medium transition-colors ${menuItemVariant}`}
								>
									{t("nav.organization")}
									<svg
										className={`h-4 w-4 shrink-0 transition-transform ${orgMenuOpen ? "rotate-180" : ""}`}
										fill="none"
										viewBox="0 0 24 24"
										strokeWidth="2"
										stroke="currentColor"
										aria-hidden="true"
									>
										<path
											strokeLinecap="round"
											strokeLinejoin="round"
											d="m19.5 8.25-7.5 7.5-7.5-7.5"
										/>
									</svg>
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
												className={`block px-3 py-2 rounded-lg text-sm font-medium transition-colors ${isTransparent ? "text-white/80 hover:bg-white/10 hover:text-white" : "text-gray-600 hover:bg-brand-50 hover:text-brand-600"}`}
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
							className={`block w-full text-left px-3 py-2 rounded-lg text-sm font-medium transition-colors ${isTransparent ? "text-red-400 hover:bg-white/10 hover:text-red-300" : "text-red-600 hover:bg-red-50 hover:text-red-700"}`}
						>
							{t("nav.signOut")}
						</button>
					</div>
				) : (
					<div className="space-y-2">
						<button
							type="button"
							onClick={onSignIn}
							className={`block w-full text-center rounded-lg px-4 py-2 text-sm font-medium transition-colors ${isTransparent ? "bg-white text-brand-800 hover:bg-brand-50" : "bg-brand-700 text-white hover:bg-brand-800"}`}
						>
							{t("nav.signIn")}
						</button>
						<button
							type="button"
							onClick={onRegister}
							className={`block w-full text-center rounded-lg border px-4 py-2 text-sm font-medium transition-colors ${isTransparent ? "border-white/50 text-white hover:bg-white/10" : "border-brand-700 text-brand-700 hover:bg-brand-50"}`}
						>
							{t("nav.register")}
						</button>
					</div>
				)}
			</div>
		</div>
	);
}
