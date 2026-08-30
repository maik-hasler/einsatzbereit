import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import SignUpModal from "./SignUpModal";
import type { TimeSlotDetail } from "../client/api-client";
import { renderWithProviders } from "../test/render";
import { formatDateTimeRange } from "../lib/format";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

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

function open(
	timeSlots: TimeSlotDetail[],
	lng: "de" | "en" = "en",
	participationType = "ScheduledSlots",
	extraProps: {
		engagedTimeSlotIds?: string[];
		preselectedTimeSlotId?: string;
	} = {},
) {
	return renderWithProviders(
		<SignUpModal
			opportunityId="opp-1"
			organizationId="org-1"
			participationType={participationType}
			timeSlots={timeSlots}
			onClose={() => {}}
			onSuccess={() => {}}
			{...extraProps}
		/>,
		{ lng, auth: { isAuthenticated: true } },
	);
}

beforeEach(() => {
	api.__reset();
});

describe("SignUpModal time-slot preselection (#657)", () => {
	it("preselects the only available slot", () => {
		const only = slot("only", 9);
		open([only]);

		const trigger = screen.getByRole("combobox");
		expect(trigger).not.toHaveTextContent("Please select");
		expect(trigger).toHaveTextContent(
			formatDateTimeRange(
				only.startDateTime as unknown as string,
				only.endDateTime as unknown as string,
				"en",
			),
		);
	});

	it("leaves the dropdown empty when there is a real choice to make", () => {
		open([slot("a", 9), slot("b", 13)]);

		expect(screen.getByRole("combobox")).toHaveTextContent("Please select");
	});

	it("still leaves it empty when the only other slot is full", () => {
		open([slot("open", 9), slot("full", 13, 4)]);

		expect(screen.getByRole("combobox")).not.toHaveTextContent("Please select");
	});
});

describe("SignUpModal vocabulary in German (#1775)", () => {
	it("keeps one verb from trigger to submit, never reusing 'Anmelden'", () => {
		open([], "de", "IndividualContact");

		const dialog = screen.getByRole("dialog");
		expect(dialog).toHaveAccessibleName("Interesse bekunden");
		expect(
			within(dialog).getByRole("button", { name: "Interesse bekunden" }),
		).toBeInTheDocument();
		expect(
			within(dialog).queryByRole("button", { name: "Anmelden" }),
		).toBeNull();
	});
});

describe("SignUpModal message field", () => {
	const openInterest = (lng: "de" | "en" = "en") =>
		open([], lng, "IndividualContact");

	it("labels the field and marks it required, for both kinds of user", async () => {
		openInterest();

		const field = await screen.findByLabelText(/Message/);
		expect(field).toHaveAttribute("aria-required", "true");
		expect(field.id).toBe("sign-up-message");

		const label = document.querySelector('label[for="sign-up-message"]');
		expect(label?.textContent).toContain("*");
		expect(label?.querySelector('[aria-hidden="true"]')).not.toBeNull();
	});

	it("rejects an empty message inline, in the reader's own language", async () => {
		openInterest("de");

		await userEvent.click(
			screen.getByRole("button", { name: "Interesse bekunden" }),
		);

		const error = await screen.findByRole("alert");
		expect(error.id).toBe("sign-up-message-error");
		expect(error.textContent?.trim()).not.toBe("");
		expect(screen.getByLabelText(/Nachricht/)).toHaveAttribute(
			"aria-describedby",
			"sign-up-message-error",
		);
		expect(api.createEngagement).not.toHaveBeenCalled();
	});
});

describe("SignUpModal duplicate sign-up conflict", () => {
	it("states the specific reason rather than a generic unknown error", async () => {
		open([], "en", "IndividualContact");
		api.createEngagement.mockRejectedValue({
			status: 409,
			errorCode: "Engagement.AlreadySignedUp",
		});

		await userEvent.type(
			screen.getByLabelText(/Message/),
			"Please let me help.",
		);
		await userEvent.click(
			screen.getByRole("button", { name: "Express interest" }),
		);

		const error = await screen.findByRole("alert");
		expect(error).toHaveTextContent(
			"You are already signed up for this opportunity.",
		);
		expect(error).not.toHaveTextContent("Unknown error");
	});
});

describe("SignUpModal slot picker", () => {
	it("states how full each slot already is", async () => {
		open([slot("slot-a", 9, 0), slot("slot-b", 14, 3)]);

		await userEvent.click(await screen.findByRole("combobox"));

		const options = screen.getAllByRole("option");
		expect(options).toHaveLength(2);
		expect(options[0]).toHaveTextContent("4 spots left");
		expect(options[1]).toHaveTextContent("1 spot left");
	});
});

describe("SignUpModal slots the volunteer already holds (#2323)", () => {
	it("disables them instead of offering an option that can only 409", async () => {
		open([slot("held", 9), slot("free", 14)], "en", "ScheduledSlots", {
			engagedTimeSlotIds: ["held"],
		});

		await userEvent.click(await screen.findByRole("combobox"));

		const options = screen.getAllByRole("option");
		expect(options[0]).toHaveTextContent("Already signed up");
		expect(options[0]).toHaveAttribute("aria-disabled", "true");
		expect(options[1]).not.toHaveAttribute("aria-disabled", "true");
	});

	it("does not preselect one as the sole remaining choice", () => {
		open([slot("held", 9), slot("free", 14)], "en", "ScheduledSlots", {
			engagedTimeSlotIds: ["held"],
		});

		// One free slot left, so it is preselected - the held one must not count
		// towards that "only one option" shortcut in either direction.
		expect(screen.getByRole("combobox")).not.toHaveTextContent("Please select");
	});

	it("falls back to the picker when the deep-linked slot is one the volunteer already holds", () => {
		open([slot("held", 9), slot("free", 14)], "en", "ScheduledSlots", {
			engagedTimeSlotIds: ["held"],
			preselectedTimeSlotId: "held",
		});

		expect(screen.queryByTestId("sign-up-confirmed-slot")).toBeNull();
		expect(screen.getByRole("combobox")).toBeInTheDocument();
	});
});
