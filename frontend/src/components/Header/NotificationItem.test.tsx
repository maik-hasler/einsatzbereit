import { describe, it, expect, vi } from "vitest";
import { screen, within } from "@testing-library/react";
import NotificationItem from "./NotificationItem";
import type { NotificationSummary } from "../../client/api-client";
import { renderWithProviders } from "../../test/render";

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
		expect(
			within(row).getByText("Unread").previousElementSibling,
		).toHaveAttribute("aria-hidden", "true");
	});

	it("says nothing of the sort on a read row", () => {
		renderRow(true);

		const row = rowButton();
		expect(row).toHaveAccessibleName(new RegExp(TEXT));
		expect(row).not.toHaveAccessibleName(/Unread/);
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
