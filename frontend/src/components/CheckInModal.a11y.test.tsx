import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import CheckInModal from "./CheckInModal";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

const { api } = vi.hoisted(() => ({
	api: {
		getVolunteerOpportunityDetails: vi.fn(),
		checkInWithPin: vi.fn(),
	},
}));

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

function details(checkInMethod: string) {
	return {
		id: "opp-1",
		checkInMethod,
		participationType: "ScheduledSlots",
	};
}

beforeEach(() => {
	vi.clearAllMocks();
});

describe("CheckInModal a11y", () => {
	function open() {
		return renderWithProviders(
			<CheckInModal
				engagementId="8d3f5b21-0000-0000-0000-000000000000"
				opportunityId="opp-1"
				onCheckedIn={() => {}}
				onClose={() => {}}
			/>,
		);
	}

	it("has no violations while the opportunity is still loading", async () => {
		api.getVolunteerOpportunityDetails.mockReturnValue(new Promise(() => {}));
		open();
		await expectNoA11yViolations();
	});

	it("has no violations when the opportunity could not be loaded", async () => {
		api.getVolunteerOpportunityDetails.mockRejectedValue(new Error("boom"));
		open();
		await screen.findByRole("alert");
		await expectNoA11yViolations();
	});

	it("has no violations showing the QR code and its fallback code", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue(details("QRCode"));
		open();
		await screen.findByTestId("checkin-fallback-code");
		await expectNoA11yViolations();
	});

	it("gives the fallback code an accessible name through its dl/dt/dd pairing", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue(details("QRCode"));
		open();
		const code = await screen.findByTestId("checkin-fallback-code");
		expect(code.tagName).toBe("DD");
		expect(code.closest("dl")?.querySelector("dt")).not.toBeNull();
	});

	it("has no violations showing the PIN form", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue(details("PINCode"));
		open();
		await screen.findByLabelText(/pin/i);
		await expectNoA11yViolations();
	});

	it("has no violations when the entered PIN was rejected", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue(details("PINCode"));
		api.checkInWithPin.mockRejectedValue(new Error("nope"));
		open();

		await userEvent.type(await screen.findByLabelText(/pin/i), "123456");
		await userEvent.click(screen.getByRole("button", { name: "Submit" }));

		await screen.findByRole("alert");
		await expectNoA11yViolations();
	});

	it("has no violations after a successful check-in", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue(details("PINCode"));
		api.checkInWithPin.mockResolvedValue(undefined);
		open();

		await userEvent.type(await screen.findByLabelText(/pin/i), "123456");
		await userEvent.click(screen.getByRole("button", { name: "Submit" }));

		await waitFor(() =>
			expect(screen.getByRole("status")).not.toBeEmptyDOMElement(),
		);
		await expectNoA11yViolations();
	});

	it("has no violations with the check-in window notice shown (#2323)", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue(details("PINCode"));
		const start = new Date(Date.now() + 24 * 60 * 60 * 1000);
		renderWithProviders(
			<CheckInModal
				engagementId="8d3f5b21-0000-0000-0000-000000000000"
				opportunityId="opp-1"
				timeSlotStartDateTime={start}
				timeSlotEndDateTime={new Date(start.getTime() + 2 * 60 * 60 * 1000)}
				onCheckedIn={() => {}}
				onClose={() => {}}
			/>,
		);

		await screen.findByTestId("checkin-window-notice");
		await expectNoA11yViolations();
	});

	it("has no violations for the manual and no-check-in instructions", async () => {
		for (const method of ["Manual", "None"]) {
			api.getVolunteerOpportunityDetails.mockResolvedValue(details(method));
			const { unmount } = open();
			await screen.findByRole("dialog");
			await expectNoA11yViolations();
			unmount();
		}
	});
});
