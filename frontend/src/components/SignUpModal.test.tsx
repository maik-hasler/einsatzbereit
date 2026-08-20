import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, within } from "@testing-library/react";
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
