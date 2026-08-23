import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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

function renderModal(checkInMethod: string, lng: "de" | "en" = "en") {
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
		{ lng, auth: { isAuthenticated: true } },
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

describe("CheckInModal opportunity gone by the time it's opened", () => {
	it("shows a friendly error instead of spinning forever", async () => {
		api.getVolunteerOpportunityDetails.mockRejectedValue({ status: 404 });
		renderWithProviders(
			<CheckInModal
				engagementId={ENGAGEMENT_ID}
				opportunityId={OPPORTUNITY_ID}
				onCheckedIn={() => {}}
				onClose={() => {}}
			/>,
			{ auth: { isAuthenticated: true } },
		);

		expect(
			await screen.findByText("This opportunity is no longer available."),
		).toBeInTheDocument();
		expect(screen.queryByRole("status", { name: /loading/i })).toBeNull();
	});
});

describe("CheckInModal localized PIN error", () => {
	it("shows the German translation, not the raw English server text", async () => {
		renderModal("PINCode", "de");
		// No errorCode on the rejection: this exercises the modal's own
		// fallback copy, not an apiError.* lookup - the fallback is what a
		// raw, un-translated server string would otherwise leak as.
		api.checkInWithPin.mockRejectedValue({ status: 400 });

		await userEvent.type(await screen.findByLabelText(/PIN/i), "000000");
		await userEvent.click(screen.getByRole("button", { name: "Bestätigen" }));

		expect(
			await screen.findByText("Falsche PIN. Bitte erneut versuchen."),
		).toBeInTheDocument();
		expect(screen.queryByText("Invalid PIN. Please try again.")).toBeNull();
	});
});
