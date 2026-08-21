import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import CheckInModal from "./CheckInModal";
import { renderWithProviders } from "../test/render";

/**
 * `CheckInModalQrFallbackCodeTests`, moved down in #2148 wave 13. Remaining
 * inventory: #2159.
 *
 * The fallback code exists for when the scan does not work - a phone camera
 * that will not focus, a cracked screen, an organizer without a scanner. It
 * has to be short enough to read aloud and labelled so a screen-reader user
 * landing on it knows what it is (hence the dl/dt/dd, which is what makes the
 * value's accessible name carry its label).
 */
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
		// Short enough to read out loud - the full engagement id is not.
		expect(code.textContent?.trim()).toHaveLength(8);
		expect(code.textContent).not.toBe(ENGAGEMENT_ID);

		// The label is the reason for the dl/dt/dd: a value with no context is
		// useless to anyone landing directly on it.
		expect(
			screen.getByText(
				"If the scan doesn't work, tell the organizer this code:",
			),
		).toBeInTheDocument();
	});

	it("shows nothing of the sort for a PIN-code opportunity", async () => {
		// The fallback belongs to the QR flow; a PIN opportunity has its own
		// input, and a second code beside it would be two things to read out.
		renderModal("PINCode");

		await screen.findByLabelText(/PIN/i);
		expect(screen.queryByTestId("checkin-fallback-code")).toBeNull();
	});
});
