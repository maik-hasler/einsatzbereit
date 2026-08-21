import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import SignUpModal from "./SignUpModal";
import type { TimeSlotDetail } from "../client/api-client";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

const { api } = vi.hoisted(() => ({
	api: { createEngagement: vi.fn() },
}));

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

function slot(id: string, hour: number, booked = 0): TimeSlotDetail {
	return {
		id,
		startDateTime: new Date(Date.UTC(2026, 7, 27, hour, 0)),
		endDateTime: new Date(Date.UTC(2026, 7, 27, hour + 4, 0)),
		maxParticipants: 4,
		bookedCount: booked,
		seriesId: undefined,
		recurrenceFrequency: undefined,
		recurrenceCount: undefined,
	};
}

const timeSlots = [
	slot("slot-a", 9),
	slot("slot-b", 13),
	slot("slot-c", 17, 4),
];

function open(props: Partial<Parameters<typeof SignUpModal>[0]> = {}) {
	return renderWithProviders(
		<SignUpModal
			opportunityId="opp-1"
			organizationId="org-1"
			participationType="ScheduledSlots"
			timeSlots={timeSlots}
			onClose={() => {}}
			onSuccess={() => {}}
			{...props}
		/>,
		{ auth: { isAuthenticated: true } },
	);
}

beforeEach(() => {
	vi.clearAllMocks();
});

describe("SignUpModal a11y", () => {
	it("has no violations with the time-slot picker closed", async () => {
		open();
		await expectNoA11yViolations();
	});

	it("has no violations with the time-slot listbox open", async () => {
		open();
		await userEvent.click(screen.getByRole("combobox"));
		expect(screen.getByRole("listbox")).toBeInTheDocument();
		await expectNoA11yViolations();
	});

	it("has no violations when one slot was already confirmed by a row click", async () => {
		open({ preselectedTimeSlotId: "slot-a" });
		expect(screen.queryByRole("combobox")).toBeNull();
		await expectNoA11yViolations();
	});

	it("has no violations for an individual opportunity's required message field", async () => {
		open({ participationType: "Individual", timeSlots: [] });
		await expectNoA11yViolations();
	});

	it("has no violations once inline validation has rejected the message field", async () => {
		open({ participationType: "Individual", timeSlots: [] });
		await userEvent.click(
			screen.getByRole("button", { name: "Express interest" }),
		);

		const field = screen.getByRole("textbox");
		expect(field).toHaveAttribute("aria-invalid", "true");
		expect(field).toHaveAccessibleDescription(
			screen.getByRole("alert").textContent ?? "",
		);
		await expectNoA11yViolations();
	});
});
