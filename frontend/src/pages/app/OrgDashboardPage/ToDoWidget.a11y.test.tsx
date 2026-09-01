import { describe, it, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ToDoWidget from "./ToDoWidget";
import { renderWithProviders } from "../../../test/render";
import { expectNoA11yViolations } from "../../../test/a11y";
import type { WidgetSize } from "./widgetCatalog";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";

const LIST: WidgetSize = { width: "medium", height: "short" };
const STRIP: WidgetSize = { width: "medium", height: "strip" };

// The shared-fetch cache is keyed per organization and refresh key in a
// module-level map, so every case gets its own refreshKey - otherwise the
// pending promise the loading case parks there is what all the later ones
// subscribe to, and they never leave the skeleton.
let nextRefreshKey = 0;

function pending(id: string, volunteerName: string) {
	const start = new Date(Date.now() + 3 * 24 * 60 * 60 * 1000);
	return {
		id,
		opportunityId: "44444444-4444-4444-4444-444444444444",
		opportunityTitle: "Blutspendetermin begleiten",
		opportunityTitleEn: "Support a blood donation drive",
		volunteerName,
		status: "Pending",
		isCheckedIn: false,
		hasFeedback: false,
		createdOn: new Date(),
		timeSlotStartDateTime: start,
		timeSlotEndDateTime: new Date(start.getTime() + 2 * 60 * 60 * 1000),
	};
}

function renderWidget(size: WidgetSize = LIST, isOrganizer = true) {
	return renderWithProviders(
		<ToDoWidget
			organizationId={ORG_ID}
			refreshKey={++nextRefreshKey}
			size={size}
			isOrganizer={isOrganizer}
		/>,
		{ auth: { isAuthenticated: true } },
	);
}

beforeEach(() => {
	api.__reset();
	api.getOrganizationDashboard.mockResolvedValue({
		pendingEngagements: 3,
		confirmedEngagementsTotal: 0,
		distinctVolunteersTotal: 0,
		signUpsLast30Days: 0,
		signUpsPrevious30Days: 0,
	});
});

describe("ToDoWidget a11y", () => {
	it("has no violations while the queue is loading", async () => {
		api.getOrganizationEngagements.mockReturnValue(new Promise(() => {}));

		renderWidget();

		await expectNoA11yViolations();
	});

	it("has no violations with rows and their two verdicts on screen", async () => {
		api.getOrganizationEngagements.mockResolvedValue({
			items: [pending("e-1", "Vera Volunteer"), pending("e-2", "Ali Helper")],
			currentPage: 1,
			pageCount: 2,
			totalItems: 7,
		});

		renderWidget();

		await screen.findByTestId("todo-widget-confirm-e-1");
		await expectNoA11yViolations();
	});

	it("has no violations with the decline dialog open over the queue", async () => {
		api.getOrganizationEngagements.mockResolvedValue({
			items: [pending("e-1", "Vera Volunteer")],
			currentPage: 1,
			pageCount: 1,
			totalItems: 1,
		});

		renderWidget();

		await userEvent.click(await screen.findByTestId("todo-widget-decline-e-1"));

		await screen.findByRole("dialog");
		await expectNoA11yViolations();
	});

	it("has no violations in the nothing-waiting state", async () => {
		api.getOrganizationEngagements.mockResolvedValue({
			items: [],
			currentPage: 1,
			pageCount: 0,
			totalItems: 0,
		});

		renderWidget();

		await screen.findByTestId("todo-widget-resolved");
		await expectNoA11yViolations();
	});

	it("has no violations when the queue fails to load", async () => {
		api.getOrganizationEngagements.mockRejectedValue(new Error("boom"));

		renderWidget();

		await screen.findByRole("status");
		await expectNoA11yViolations();
	});

	// One grid row and a plain member both fall back to the count, which is a
	// different tree, not a different class.
	it("has no violations as the count-only fallback", async () => {
		renderWidget(STRIP);

		await screen.findByTestId("todo-widget-stat-pending");
		await expectNoA11yViolations();
	});
});
