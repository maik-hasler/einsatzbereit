import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import SignUpModal from "./SignUpModal";
import type { TimeSlotDetail } from "../client/api-client";
import { renderWithProviders } from "../test/render";

/**
 * Was `SignUpModalPreselectTests` (#657) and the modal case of
 * `SignUpVocabularyTests` (#1775), moved down in #2148 wave 2. Both seeded a
 * whole organization and opportunity over raw HTTP, signed a volunteer in and
 * navigated to the detail page to reach a dialog whose behaviour is decided
 * entirely by the `timeSlots` prop.
 */
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
) {
	return renderWithProviders(
		<SignUpModal
			opportunityId="opp-1"
			organizationId="org-1"
			participationType={participationType}
			timeSlots={timeSlots}
			onClose={() => {}}
			onSuccess={() => {}}
		/>,
		{ lng, auth: { isAuthenticated: true } },
	);
}

beforeEach(() => {
	api.__reset();
});

describe("SignUpModal time-slot preselection (#657)", () => {
	it("preselects the only available slot", () => {
		// The dropdown always initialized empty, even when there was nothing
		// else to pick - an avoidable extra click before submitting.
		open([slot("only", 9)]);

		const trigger = screen.getByRole("combobox");
		expect(trigger).not.toHaveTextContent("Please select");
		// The English locale renders the slot as "27 Aug 2026, 09:00-13:00" -
		// assert on the time range, which is locale-stable, rather than on a
		// particular date format.
		expect(trigger).toHaveTextContent("09:00-13:00");
	});

	it("leaves the dropdown empty when there is a real choice to make", () => {
		open([slot("a", 9), slot("b", 13)]);

		expect(screen.getByRole("combobox")).toHaveTextContent("Please select");
	});

	it("still leaves it empty when the only other slot is full", () => {
		// "Available" means not full - a single open slot beside a full one is
		// still the only thing that can be picked.
		open([slot("open", 9), slot("full", 13, 4)]);

		expect(screen.getByRole("combobox")).not.toHaveTextContent("Please select");
	});
});

describe("SignUpModal vocabulary in German (#1775)", () => {
	it("keeps one verb from trigger to submit, never reusing 'Anmelden'", () => {
		// "Anmelden" used to be both nav.signIn (authenticate) and
		// signUp.submit (commit to a shift) - most visibly on the opportunity
		// detail page, where "Melde dich an, um dich fuer diesen Einsatz
		// anzumelden." sat directly above an "Anmelden" button.
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

/**
 * `SignUpModalMessageFieldTests` and `CheckInAndSlotTests`' slot-count case,
 * moved down in #2148 wave 13. Remaining inventory: #2159.
 */
describe("SignUpModal message field", () => {
	const openInterest = (lng: "de" | "en" = "en") =>
		open([], lng, "IndividualContact");

	it("labels the field and marks it required, for both kinds of user", async () => {
		openInterest();

		const field = await screen.findByLabelText(/Message/);
		// Both halves matter: the visible asterisk is what a sighted user reads,
		// `aria-required` is what everyone else does. RequiredMark renders the
		// asterisk aria-hidden precisely so the two do not double up.
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
		// The control has to point at the message, not just render it nearby.
		expect(screen.getByLabelText(/Nachricht/)).toHaveAttribute(
			"aria-describedby",
			"sign-up-message-error",
		);
		// Rejected client-side: nothing was sent.
		expect(api.createEngagement).not.toHaveBeenCalled();
	});
});

describe("SignUpModal slot picker", () => {
	it("states how full each slot already is", async () => {
		// Which slot to take is the only decision this dialog asks for, and
		// remaining capacity is the thing that decides it - a bare list of times
		// makes the volunteer pick blind.
		open([slot("slot-a", 9, 0), slot("slot-b", 14, 3)]);

		await userEvent.click(await screen.findByRole("combobox"));

		const options = screen.getAllByRole("option");
		expect(options).toHaveLength(2);
		expect(options[0]).toHaveTextContent("4 spots left");
		expect(options[1]).toHaveTextContent("1 spot left");
	});
});
