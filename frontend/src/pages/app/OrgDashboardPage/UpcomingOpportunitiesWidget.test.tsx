import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import UpcomingOpportunitiesWidget from "./UpcomingOpportunitiesWidget";
import { renderWithProviders } from "../../../test/render";
import { formatDateTimeRange } from "../../../lib/format";
import type { WidgetSize } from "./widgetCatalog";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";
const OPPORTUNITY_ID = "22222222-2222-2222-2222-222222222222";

// Anchored a few days out from whenever the suite runs, so the slots are always
// still ahead of "now" and the widget's own upcoming filter cannot quietly
// empty the list on a fixed date that has since passed.
const IN_TWO_DAYS = new Date(Date.now() + 2 * 24 * 60 * 60 * 1000);
const IN_THREE_DAYS = new Date(Date.now() + 3 * 24 * 60 * 60 * 1000);

function slot(id: string, start: Date, bookedCount: number) {
	return {
		timeSlotId: id,
		startDateTime: start.toISOString(),
		endDateTime: new Date(start.getTime() + 2 * 60 * 60 * 1000).toISOString(),
		maxParticipants: 6,
		bookedCount,
	};
}

const calendarEvent = {
	opportunityId: OPPORTUNITY_ID,
	titleDe: "Blutspendetermin begleiten",
	titleEn: "Support a blood donation drive",
	color: undefined,
	timeSlots: [slot("slot-a", IN_TWO_DAYS, 3), slot("slot-b", IN_THREE_DAYS, 0)],
};

const WIDE: WidgetSize = { width: "medium", height: "short" };

beforeEach(() => {
	api.__reset();
	api.getOrganizationCalendarEvents.mockResolvedValue([calendarEvent]);
});

function renderWidget(size: WidgetSize = WIDE) {
	return renderWithProviders(
		<UpcomingOpportunitiesWidget
			organizationId={ORG_ID}
			refreshKey={0}
			size={size}
			isOrganizer
			onOpportunityCreated={() => {}}
		/>,
		{ auth: { isAuthenticated: true } },
	);
}

describe("UpcomingOpportunitiesWidget", () => {
	// Every widget is handed a "compact" width on a phone, whatever the tile's
	// own width, so gating these lines on that dropped the date and the sign-up
	// count from every row on every phone and left a list of bare titles (#2321).
	it("shows the time and the places filled beside every occurrence", async () => {
		renderWidget();

		expect(
			await screen.findAllByText("Support a blood donation drive"),
		).toHaveLength(2);

		// Computed the same way the widget computes it, so the expectation does not
		// hardcode a formatted string that depends on the host timezone.
		expect(
			screen.getByText(
				formatDateTimeRange(
					IN_TWO_DAYS.toISOString(),
					new Date(IN_TWO_DAYS.getTime() + 2 * 60 * 60 * 1000).toISOString(),
					"en",
				),
			),
		).toBeInTheDocument();
		expect(screen.getByText("3/6 places")).toBeInTheDocument();
		expect(screen.getByText("0/6 places")).toBeInTheDocument();
	});

	// One opportunity that repeats is several mornings to staff, and rolling them
	// into a single row hid every one but the first.
	it("lists each occurrence of a repeating opportunity as its own row", async () => {
		renderWidget();

		expect(await screen.findByTestId("upcoming-slot-slot-a")).toBeVisible();
		expect(screen.getByTestId("upcoming-slot-slot-b")).toBeVisible();
	});

	it("leaves out occurrences that have already finished", async () => {
		const ended = new Date(Date.now() - 5 * 60 * 60 * 1000);
		api.getOrganizationCalendarEvents.mockResolvedValue([
			{ ...calendarEvent, timeSlots: [slot("slot-past", ended, 2)] },
		]);

		renderWidget();

		expect(
			await screen.findByText("Nothing scheduled in the next 30 days."),
		).toBeInTheDocument();
		expect(screen.queryByTestId("upcoming-slot-slot-past")).toBeNull();
	});
});
