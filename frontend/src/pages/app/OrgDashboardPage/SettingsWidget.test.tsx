import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import SettingsWidget from "./SettingsWidget";
import { renderWithProviders } from "../../../test/render";
import type { OrganizationDetailsResponse } from "../../../client/api-client";
import type { WidgetSize } from "./widgetCatalog";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";

const org = {
	id: ORG_ID,
	name: "Freiwillige Feuerwehr Kiel",
	createdOn: new Date(Date.UTC(2026, 0, 1)),
	members: [
		{ userId: "u-1", username: "olaf", role: "Organizer" },
		{ userId: "u-2", username: "vera", role: "Member" },
	],
	requestingUserRole: "Organizer",
	membersUnavailable: false,
} as unknown as OrganizationDetailsResponse;

const SHORT: WidgetSize = { width: "compact", height: "short" };

function invitation(
	id: string,
	status: string,
	expiresInDays: number,
): Record<string, unknown> {
	return {
		id,
		inviteeId: `invitee-${id}`,
		inviteeName: `Invitee ${id}`,
		intendedRole: "Member",
		status,
		createdOn: new Date(),
		expiresOn: new Date(Date.now() + expiresInDays * 24 * 60 * 60 * 1000),
	};
}

let nextRefreshKey = 0;

function renderWidget(isOrganizer = true) {
	return renderWithProviders(
		<SettingsWidget
			org={org}
			refreshKey={++nextRefreshKey}
			size={SHORT}
			isOrganizer={isOrganizer}
		/>,
		{ auth: { isAuthenticated: true } },
	);
}

beforeEach(() => {
	api.__reset();
	api.getOrgInvitations.mockResolvedValue([]);
});

describe("SettingsWidget", () => {
	it("leads with how many people are on the team", async () => {
		renderWidget();

		expect(
			await screen.findByTestId("team-widget-member-count"),
		).toHaveTextContent("2");
		expect(screen.getByText("members")).toBeInTheDocument();
	});

	// The endpoint returns the organization's whole invitation history, so
	// counting rows reported people who declined months ago, and people whose
	// invitation has since lapsed, as still deciding.
	it("counts only invitations that are genuinely still unanswered", async () => {
		api.getOrgInvitations.mockResolvedValue([
			invitation("a", "Pending", 5),
			invitation("b", "Pending", 9),
			invitation("c", "Declined", 5),
			invitation("d", "Accepted", 5),
			invitation("e", "Expired", -1),
			invitation("f", "Pending", -2),
		]);

		renderWidget();

		expect(
			await screen.findByTestId("team-widget-invitations"),
		).toHaveTextContent("2 invitations still unanswered");
	});

	it("says nothing at all when no invitation is outstanding", async () => {
		api.getOrgInvitations.mockResolvedValue([
			invitation("a", "Declined", 5),
			invitation("b", "Expired", -3),
		]);

		renderWidget();

		await screen.findByTestId("team-widget-member-count");
		expect(screen.queryByTestId("team-widget-invitations")).toBeNull();
	});

	// The invitations endpoint is organizer-only; asking it for a plain member
	// could only ever 403.
	it("never asks for invitations as a plain member", async () => {
		renderWidget(false);

		await screen.findByTestId("team-widget-member-count");
		expect(api.getOrgInvitations).not.toHaveBeenCalled();
	});
});
