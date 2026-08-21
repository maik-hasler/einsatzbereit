import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import CheckInModal from "./CheckInModal";
import { renderWithProviders } from "../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ENGAGEMENT_ID = "abcd1234-5678-90ab-cdef-1234567890ab";
const OPPORTUNITY_ID = "22222222-2222-2222-2222-222222222222";

beforeEach(() => {
	api.__reset();
});

function renderModal(checkInMethod: string) {
	api.getVolunteerOpportunityDetails.mockResolvedValue({
		id: OPPORTUNITY_ID,
		checkInMethod,
		titleDe: "Deutscher Titel",
		status: "Published",
		participationType: "ScheduledSlots",
		timeSlots: [],
	});
	return renderWithProviders(
		<CheckInModal
			engagementId={ENGAGEMENT_ID}
			opportunityId={OPPORTUNITY_ID}
			onCheckedIn={() => {}}
			onClose={() => {}}
		/>,
		{ auth: { isAuthenticated: true } },
	);
}

describe("CheckInModal QR fallback code", () => {
	it("shows a short code under its own label", async () => {
		renderModal("QRCode");

		const code = await screen.findByTestId("checkin-fallback-code");
		expect(code).toHaveTextContent("abcd1234");
		expect(code.textContent?.trim()).toHaveLength(8);
		expect(code.textContent).not.toBe(ENGAGEMENT_ID);

		expect(
			screen.getByText(
				"If the scan doesn't work, tell the organizer this code:",
			),
		).toBeInTheDocument();
	});

	it("shows nothing of the sort for a PIN-code opportunity", async () => {
		renderModal("PINCode");

		await screen.findByLabelText(/PIN/i);
		expect(screen.queryByTestId("checkin-fallback-code")).toBeNull();
	});
});
