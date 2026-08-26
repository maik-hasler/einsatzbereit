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
		actionUrl: "/app/org-1/dashboard/opportunities/opp-1/engagements",
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

function rowLink(): HTMLElement {
	const link = document.querySelector<HTMLElement>("li > a");
	expect(link).not.toBeNull();
	return link as HTMLElement;
}

describe("NotificationItem unread marker", () => {
	it("puts 'Unread' into an unread row's own accessible name", () => {
		renderRow(false);

		const row = rowLink();
		expect(row).toHaveAccessibleName(new RegExp(TEXT));
		expect(row).toHaveAccessibleName(/Unread/);
		expect(
			within(row).getByText("Unread").previousElementSibling,
		).toHaveAttribute("aria-hidden", "true");
	});

	it("says nothing of the sort on a read row", () => {
		renderRow(true);

		const row = rowLink();
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

describe("NotificationItem navigation", () => {
	it("is a real link to the notification's subject", () => {
		renderRow(false);

		expect(rowLink()).toHaveAttribute(
			"href",
			"/app/org-1/dashboard/opportunities/opp-1/engagements",
		);
	});

	it("falls back to /my-signups when the notification carries no actionUrl", () => {
		renderWithProviders(
			<ul>
				<NotificationItem
					notification={{ ...notification(false), actionUrl: undefined }}
					onSelect={vi.fn()}
					onMarkUnread={vi.fn()}
					onDelete={vi.fn()}
				/>
			</ul>,
			{ auth: { isAuthenticated: true } },
		);

		expect(rowLink()).toHaveAttribute("href", "/my-signups");
	});

	it("marks a German-only title with lang, while the surrounding English sentence carries none", () => {
		renderRow(false);

		expect(within(rowLink()).getByText("Deutscher Einsatz")).toHaveAttribute(
			"lang",
			"de",
		);
	});
});
