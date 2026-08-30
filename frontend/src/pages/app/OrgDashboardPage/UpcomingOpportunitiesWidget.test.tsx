import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import UpcomingOpportunitiesWidget from "./UpcomingOpportunitiesWidget";
import { renderWithProviders } from "../../../test/render";
import { formatDateTime } from "../../../lib/format";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";
const NEXT_START = "2026-09-14T10:30:00.000Z";

const opportunity = {
	id: "22222222-2222-2222-2222-222222222222",
	titleDe: "Blutspendetermin begleiten",
	titleEn: "Support a blood donation drive",
	organizationId: ORG_ID,
	occurrence: "OneTime",
	participationType: "ScheduledSlots",
	status: "Published",
	isRemote: false,
	createdOn: new Date(Date.UTC(2026, 7, 1)),
	nextTimeSlotStart: NEXT_START,
	totalMaxParticipants: 25,
	currentParticipantCount: 3,
};

beforeEach(() => {
	api.__reset();
	api.getOrganizationOpportunities.mockResolvedValue({ items: [opportunity] });
});

function renderWidget() {
	return renderWithProviders(
		<UpcomingOpportunitiesWidget
			organizationId={ORG_ID}
			refreshKey={0}
			isOrganizer
			onOpportunityCreated={() => {}}
		/>,
		{ auth: { isAuthenticated: true } },
	);
}

describe("UpcomingOpportunitiesWidget", () => {
	// Every widget is handed size "compact" on a phone, whatever the tile's own
	// width, so gating this line on that dropped the date and the sign-up count
	// from every row on every phone and left a list of bare titles (#2321).
	it("shows the date and the sign-up count beside every title", async () => {
		renderWidget();

		expect(
			await screen.findByText("Support a blood donation drive"),
		).toBeInTheDocument();

		// Computed the same way the widget computes it, so the expectation does not
		// hardcode a formatted string that depends on the host timezone.
		expect(
			screen.getByText(formatDateTime(NEXT_START, "en")),
		).toBeInTheDocument();
		expect(screen.getByText("3/25 sign-ups total")).toBeInTheDocument();
	});
});
