import { describe, it, expect, vi } from "vitest";
import { createRef } from "react";
import { screen } from "@testing-library/react";
import DesktopHeader from "./DesktopHeader";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import { renderWithProviders } from "../../test/render";

function menuState(): AccountMenuState {
	return {
		avatarUrl: null,
		notifications: [],
		unreadCount: 0,
		notifHasMore: false,
		notifLoadingMore: false,
		loadMoreNotifications: vi.fn(),
		notifError: null,
		notifLoading: false,
		retryNotifications: vi.fn(),
		notifOpen: false,
		setNotifOpen: vi.fn(),
		notifRef: createRef<HTMLDivElement>(),
		dropdownOpen: false,
		setDropdownOpen: vi.fn(),
		dropdownRef: createRef<HTMLDivElement>(),
		markAllRead: vi.fn(),
		markOneRead: vi.fn(),
		markOneUnread: vi.fn(),
		deleteOne: vi.fn(),
		deleteAllRead: vi.fn(),
		deletingAllRead: false,
	} as AccountMenuState;
}

const base = {
	menu: menuState(),
	displayName: "Vera Volunteer",
	initials: "VV",
	isAdmin: false,
	activeOrg: null,
	onSignOut: () => {},
	onSignIn: () => {},
	onRegister: () => {},
};

function renderHeader(route: string, isTransparent = false) {
	return renderWithProviders(
		<DesktopHeader
			{...base}
			authStatus="signedOut"
			isTransparent={isTransparent}
		/>,
		{ route },
	);
}

// jsdom has no cascade to resolve, so this is asserted on the class strings
// themselves: the underline never painted because `border-transparent` sat in
// the shared base alongside the active `border-brand-700`, two border-color
// utilities of equal specificity where the transparent one won the emitted
// order (#2329 F4). An active link that still carries both is the defect,
// whatever a browser then decides to paint.
describe("DesktopHeader active-page underline", () => {
	it("gives the current route an underline colour and nothing to override it", () => {
		renderHeader("/opportunities");

		const active = screen.getByTestId("nav-findOpportunities");
		expect(active).toHaveAttribute("aria-current", "page");
		expect(active.className).toContain("border-brand-700");
		expect(active.className).not.toContain("border-transparent");
	});

	it("leaves every other route transparent", () => {
		renderHeader("/opportunities");

		const idle = screen.getByTestId("nav-organizations");
		expect(idle).not.toHaveAttribute("aria-current");
		expect(idle.className).toContain("border-transparent");
		expect(idle.className).not.toContain("border-brand-700");
	});

	// #2311 removed the underline from the header's transparent state on
	// purpose - the fix above must not bring it back there.
	it("keeps the transparent header underline-free", () => {
		renderHeader("/opportunities", true);

		const active = screen.getByTestId("nav-findOpportunities");
		expect(active).toHaveAttribute("aria-current", "page");
		expect(active.className).toContain("border-transparent");
		expect(active.className).not.toContain("border-brand-700");
	});
});
