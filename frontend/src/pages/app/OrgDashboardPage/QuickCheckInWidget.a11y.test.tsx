import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import QuickCheckInWidget from "./QuickCheckInWidget";
import { renderWithProviders } from "../../../test/render";
import { expectNoA11yViolations } from "../../../test/a11y";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";

const opportunity = (
	id: string,
	titleDe: string,
	checkInMethod: "QRCode" | "PINCode" | "Manual" | "None",
) => ({
	id,
	titleDe,
	titleEn: undefined,
	checkInMethod,
	organizationId: ORG_ID,
	occurrence: "OneTime",
	participationType: "ScheduledSlots",
	status: "Published",
	isRemote: true,
	createdOn: new Date(Date.UTC(2026, 7, 1)),
	totalMaxParticipants: 5,
	currentParticipantCount: 0,
});

beforeEach(() => {
	api.__reset();
});

// useSharedOrgFetch dedupes on `opportunities:{org}:{refreshKey}` in a
// module-level map, so every case gets its own refreshKey - otherwise the
// pending promise the loading case parks there is what all the later ones
// subscribe to, and they never leave the skeleton.
let nextRefreshKey = 0;

function renderWidget() {
	return renderWithProviders(
		<QuickCheckInWidget
			organizationId={ORG_ID}
			refreshKey={++nextRefreshKey}
			onOpportunityCreated={() => {}}
		/>,
		{ auth: { isAuthenticated: true } },
	);
}

// The dashboard's page-level scans never reach this widget: QuickCheckIn is
// not in DEFAULT_LAYOUT, so nothing renders it unless a saved layout puts it
// there. Every state it has is therefore covered here or nowhere.
describe("QuickCheckInWidget a11y", () => {
	it("has no violations while the opportunity list is loading", async () => {
		api.getOrganizationOpportunities.mockReturnValue(new Promise(() => {}));

		renderWidget();

		await expectNoA11yViolations();
	});

	it("has no violations with a QR opportunity selected (dropdown plus scanner)", async () => {
		api.getOrganizationOpportunities.mockResolvedValue({
			items: [
				opportunity(
					"aaaaaaaa-0000-0000-0000-000000000001",
					"Scan me",
					"QRCode",
				),
			],
			pageCount: 1,
			totalCount: 1,
		});

		renderWidget();

		await screen.findByTestId("quick-checkin-scan-btn");
		await expectNoA11yViolations();
	});

	it("has no violations with a PIN opportunity selected (dropdown plus link)", async () => {
		api.getOrganizationOpportunities.mockResolvedValue({
			items: [
				opportunity(
					"aaaaaaaa-0000-0000-0000-000000000002",
					"Type a PIN",
					"PINCode",
				),
			],
			pageCount: 1,
			totalCount: 1,
		});

		renderWidget();

		await screen.findByTestId("quick-checkin-open-btn");
		await expectNoA11yViolations();
	});

	// The occurrence the action will actually check people into, spelled out
	// under the picker - an extra node in the tree, not just a class.
	it("has no violations with the selected occurrence's time shown", async () => {
		const start = new Date(Date.now() + 60 * 60 * 1000);
		api.getOrganizationOpportunities.mockResolvedValue({
			items: [
				{
					...opportunity(
						"aaaaaaaa-0000-0000-0000-000000000003",
						"Starting soon",
						"PINCode",
					),
					nextTimeSlotStart: start.toISOString(),
					nextTimeSlotEnd: new Date(
						start.getTime() + 2 * 60 * 60 * 1000,
					).toISOString(),
				},
			],
			pageCount: 1,
			totalCount: 1,
		});

		renderWidget();

		await screen.findByTestId("quick-checkin-selected-when");
		await expectNoA11yViolations();
	});

	it("has no violations in the empty state", async () => {
		api.getOrganizationOpportunities.mockResolvedValue({
			items: [
				opportunity(
					"aaaaaaaa-0000-0000-0000-000000000004",
					"Nothing to check",
					"None",
				),
			],
			pageCount: 1,
			totalCount: 1,
		});

		renderWidget();

		await screen.findByText(
			"No published opportunities with check-in enabled yet.",
		);
		await expectNoA11yViolations();
	});

	it("has no violations when the list fails to load", async () => {
		api.getOrganizationOpportunities.mockRejectedValue(new Error("boom"));

		renderWidget();

		await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
		await expectNoA11yViolations();
	});
});
