import { describe, it, expect, vi } from "vitest";
import { createRef } from "react";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import AccountControls from "./AccountControls";
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
		dropdownOpen: true,
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

function render(isAdmin: boolean) {
	return renderWithProviders(
		<AccountControls
			menu={menuState()}
			displayName={isAdmin ? "Platform Admin" : "Vera Volunteer"}
			initials={isAdmin ? "PA" : "VV"}
			isAdmin={isAdmin}
			onSignOut={() => {}}
			onNotificationNavigate={() => {}}
		/>,
		{ auth: { isAuthenticated: true, roles: isAdmin ? ["admin"] : ["user"] } },
	);
}

describe("account dropdown", () => {
	it("offers an admin the way into /administration", () => {
		render(true);

		const link = screen.getByRole("link", { name: "Administration" });
		expect(link).toHaveAttribute("href", "/administration");
	});

	it("does not offer it to a non-admin", () => {
		render(false);

		expect(
			screen.getByRole("link", { name: "My profile" }),
		).toBeInTheDocument();
		expect(screen.queryByRole("link", { name: "Administration" })).toBeNull();
	});

	it("no longer links out to Keycloak's own account console", () => {
		render(false);

		expect(
			screen.getByRole("link", { name: "My profile" }),
		).toBeInTheDocument();
		expect(screen.queryByRole("link", { name: "Account Settings" })).toBeNull();
	});
});

describe("account dropdown after navigating", () => {
	it("closes itself when one of its own links is followed", async () => {
		const setDropdownOpen = vi.fn();
		renderWithProviders(
			<AccountControls
				menu={{ ...menuState(), setDropdownOpen }}
				displayName="Vera Volunteer"
				initials="VV"
				isAdmin={false}
				onSignOut={() => {}}
				onNotificationNavigate={() => {}}
			/>,
			{ auth: { isAuthenticated: true, roles: ["user"] } },
		);

		await userEvent.click(screen.getByRole("link", { name: "My profile" }));

		expect(setDropdownOpen).toHaveBeenCalledWith(false);
	});
});
