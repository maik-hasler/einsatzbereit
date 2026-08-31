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

// The active nav link is marked by `aria-current` plus a bolder/darker
// label, deliberately with no border/underline treatment (#2329 F4 had a
// border-color specificity bug in the underline; this removes the
// underline outright rather than fixing it).
describe("DesktopHeader active-page styling", () => {
	it("marks the current route as current and carries no border classes", () => {
		renderHeader("/opportunities");

		const active = screen.getByTestId("nav-findOpportunities");
		expect(active).toHaveAttribute("aria-current", "page");
		expect(active.className).not.toMatch(/\bborder\b/);
	});

	it("leaves every other route without aria-current or border classes", () => {
		renderHeader("/opportunities");

		const idle = screen.getByTestId("nav-organizations");
		expect(idle).not.toHaveAttribute("aria-current");
		expect(idle.className).not.toMatch(/\bborder\b/);
	});

	it("keeps the transparent header underline-free too", () => {
		renderHeader("/opportunities", true);

		const active = screen.getByTestId("nav-findOpportunities");
		expect(active).toHaveAttribute("aria-current", "page");
		expect(active.className).not.toMatch(/\bborder\b/);
	});
});
