import { describe, it, expect, vi } from "vitest";
import { screen, within } from "@testing-library/react";
import NotificationItem from "./NotificationItem";
import type { NotificationSummary } from "../../client/api-client";
import { renderWithProviders } from "../../test/render";

/**
 * `NotificationUnreadMarkerTests`, moved down in #2148 wave 13. Remaining
 * inventory: #2159.
 *
 * #1786: unread was signalled by a dot's colour and the row's font weight
 * alone, both of which are invisible to a screen reader - an unread row read
 * exactly like a read one. The marker is an `sr-only` span *inside* the row's
 * own button, so it joins that button's accessible name rather than
 * announcing separately.
 *
 * Filed against `NotificationItem` rather than `NotificationDropdown`, which
 * is where the markup actually lives; the dropdown only maps over rows. The
 * E2E original signed two users in and drove a real sign-up to produce one
 * read and one unread row, both of which are a prop here.
 */
const notification = (isRead: boolean): NotificationSummary =>
	({
		id: isRead ? "read-1" : "unread-1",
		kind: "EngagementConfirmed",
		relatedTitle: "Deutscher Einsatz",
		relatedId: "22222222-2222-2222-2222-222222222222",
		isRead,
		createdOn: new Date(Date.UTC(2026, 7, 10, 9, 0)),
	}) as unknown as NotificationSummary;

function renderRow(isRead: boolean) {
	return renderWithProviders(
		<ul>
			<NotificationItem
				notification={notification(isRead)}
				onSelect={vi.fn()}
				onMarkUnread={vi.fn()}
				onDelete={vi.fn()}
			/>
		</ul>,
		{ auth: { isAuthenticated: true } },
	);
}

const TEXT = "Your sign-up for Deutscher Einsatz was confirmed";

/**
 * The row's own button. Queried structurally rather than by name: the delete
 * and mark-as-unread controls carry the same notification text in their
 * labels, so a name match is ambiguous here.
 */
function rowButton(): HTMLElement {
	const button = document.querySelector<HTMLElement>("li > button");
	expect(button).not.toBeNull();
	return button as HTMLElement;
}

describe("NotificationItem unread marker", () => {
	it("puts 'Unread' into an unread row's own accessible name", () => {
		renderRow(false);

		const row = rowButton();
		expect(row).toHaveAccessibleName(new RegExp(TEXT));
		expect(row).toHaveAccessibleName(/Unread/);
		// The dot itself stays decorative - it is the same fact, and announcing
		// it twice is worse than once.
		expect(
			within(row).getByText("Unread").previousElementSibling,
		).toHaveAttribute("aria-hidden", "true");
	});

	it("says nothing of the sort on a read row", () => {
		renderRow(true);

		const row = rowButton();
		expect(row).toHaveAccessibleName(new RegExp(TEXT));
		expect(row).not.toHaveAccessibleName(/Unread/);
		// A read row is the one that offers mark-as-unread, which is how a
		// screen-reader user acts on the state the marker announces.
		expect(
			screen.getByRole("button", { name: `Mark as unread: ${TEXT}` }),
		).toBeInTheDocument();
	});

	it("offers no mark-as-unread on a row that is already unread", () => {
		renderRow(false);

		expect(
			screen.queryByRole("button", { name: /^Mark as unread/ }),
		).toBeNull();
		expect(
			screen.getByRole("button", { name: `Delete: ${TEXT}` }),
		).toBeInTheDocument();
	});
});
