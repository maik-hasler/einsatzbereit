import { describe, it, vi } from "vitest";
import { createRef } from "react";
import DesktopHeader from "./DesktopHeader";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import { renderWithProviders } from "../../test/render";
import { expectNoA11yViolations } from "../../test/a11y";

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

describe("DesktopHeader a11y", () => {
	const base = {
		isTransparent: false,
		menu: menuState(),
		displayName: "Vera Volunteer",
		initials: "VV",
		isAdmin: false,
		activeOrg: null,
		onSignOut: () => {},
		onSignIn: () => {},
		onRegister: () => {},
	};

	it("has no violations for a signed-out visitor", async () => {
		renderWithProviders(<DesktopHeader {...base} authStatus="signedOut" />);
		await expectNoA11yViolations();
	});

	it("has no violations for a signed-in visitor", async () => {
		renderWithProviders(<DesktopHeader {...base} authStatus="signedIn" />, {
			auth: { isAuthenticated: true },
		});
		await expectNoA11yViolations();
	});

	it("has no violations while probing for a live Keycloak session", async () => {
		renderWithProviders(<DesktopHeader {...base} authStatus="pending" />);
		await expectNoA11yViolations();
	});

	it("has no violations for an expired session", async () => {
		renderWithProviders(
			<DesktopHeader {...base} authStatus="sessionExpired" />,
		);
		await expectNoA11yViolations();
	});

	it("has no violations over the transparent landing-page header", async () => {
		renderWithProviders(
			<DesktopHeader {...base} authStatus="pending" isTransparent />,
		);
		await expectNoA11yViolations();
	});
});
