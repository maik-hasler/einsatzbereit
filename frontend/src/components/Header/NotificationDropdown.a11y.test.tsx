import { describe, it, expect, vi } from "vitest";
import { createRef } from "react";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import NotificationDropdown from "./NotificationDropdown";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import type { NotificationSummary } from "../../client/api-client";
import { renderWithProviders } from "../../test/render";
import { expectNoA11yViolations } from "../../test/a11y";

/**
 * Replaces `NotificationDropdown_Open_HasNoSeriousA11yViolations`, which had
 * to sign in, create the notification-producing state, and open the bell.
 * Every state that scan could reach is a field on `AccountMenuState`.
 */
function notification(
	id: string,
	isRead: boolean,
	kind = "EngagementConfirmed",
): NotificationSummary {
	return {
		id,
		kind,
		relatedTitle: "Beach cleanup",
		actionUrl: "/my-signups",
		isRead,
		createdOn: new Date(Date.UTC(2026, 7, 19, 8, 0)),
	};
}

function menuState(
	overrides: Partial<AccountMenuState> = {},
): AccountMenuState {
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
		notifOpen: true,
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
		...overrides,
	} as AccountMenuState;
}

function open(menu: AccountMenuState) {
	return renderWithProviders(
		<NotificationDropdown
			menu={menu}
			containerRef={createRef<HTMLDivElement>()}
			onNavigate={() => {}}
			onClose={() => {}}
		/>,
		{ auth: { isAuthenticated: true } },
	);
}

describe("NotificationDropdown a11y", () => {
	it("has no violations while collapsed", async () => {
		open(menuState({ notifOpen: false, unreadCount: 3 }));
		await expectNoA11yViolations();
	});

	it("has no violations while the panel is loading", async () => {
		open(menuState({ notifLoading: true }));
		await expectNoA11yViolations();
	});

	it("has no violations with an empty panel", async () => {
		open(menuState());
		await expectNoA11yViolations();
	});

	it("has no violations with read and unread notifications and a load-more row", async () => {
		open(
			menuState({
				notifications: [
					notification("n-1", false),
					notification("n-2", true, "OpportunityCancelled"),
					notification("n-3", true),
				],
				unreadCount: 1,
				notifHasMore: true,
			}),
		);
		await expectNoA11yViolations();
	});

	it("has no violations when the notifications could not be loaded", async () => {
		open(menuState({ notifError: "Notifications could not be loaded." }));
		await expectNoA11yViolations();
	});

	it("has no violations with the clear-read confirmation dialog open", async () => {
		open(
			menuState({
				notifications: [notification("n-1", true), notification("n-2", true)],
			}),
		);
		await userEvent.click(screen.getByRole("button", { name: "Clear read" }));

		expect(screen.getByRole("dialog")).toBeInTheDocument();
		await expectNoA11yViolations();
	});

	it("announces the unread count rather than relying on the badge alone", async () => {
		// The badge caps its *visible* text at "9+", so the count only reaches a
		// screen-reader user through the bell's accessible name.
		open(menuState({ notifOpen: false, unreadCount: 12 }));
		expect(
			screen.getByRole("button", { name: "Notifications, 12 unread" }),
		).toBeInTheDocument();
	});
});
