import { describe, expect, it } from "vitest";
import {
	getCapacityFromTimeSlots,
	getOpportunityCapacity,
} from "./opportunityCapacity";

describe("getOpportunityCapacity", () => {
	it("reads null as unlimited", () => {
		expect(
			getOpportunityCapacity({
				totalMaxParticipants: null,
				currentParticipantCount: 4,
			}),
		).toEqual({ kind: "unlimited", booked: 4 });
	});

	it("reads undefined as unlimited too, since the generated client omits nulls", () => {
		expect(
			getOpportunityCapacity({
				totalMaxParticipants: undefined,
				currentParticipantCount: 0,
			}),
		).toEqual({ kind: "unlimited", booked: 0 });
	});

	// The hole this module exists to close (#1777): 0 used to fall through both
	// branches of every card's inline check and render nothing at all.
	it("reads 0 on an interest-based opportunity as not applicable, keeping the sign-up count", () => {
		expect(
			getOpportunityCapacity({
				totalMaxParticipants: 0,
				currentParticipantCount: 3,
				participationType: "IndividualContact",
			}),
		).toEqual({ kind: "notApplicable", booked: 3, reason: "interest" });
	});

	it("reads 0 on a slot-based opportunity as not applicable because it has no slots yet", () => {
		expect(
			getOpportunityCapacity({
				totalMaxParticipants: 0,
				currentParticipantCount: 0,
				participationType: "ScheduledSlots",
			}),
		).toEqual({ kind: "notApplicable", booked: 0, reason: "noSlots" });
	});

	it("falls back to the no-slots reason when no participation type is known", () => {
		expect(
			getOpportunityCapacity({
				totalMaxParticipants: 0,
				currentParticipantCount: 0,
			}),
		).toEqual({ kind: "notApplicable", booked: 0, reason: "noSlots" });
	});

	it("reads a positive total as a cap and reports the remaining places", () => {
		expect(
			getOpportunityCapacity({
				totalMaxParticipants: 20,
				currentParticipantCount: 1,
			}),
		).toEqual({
			kind: "capped",
			booked: 1,
			max: 20,
			spotsLeft: 19,
			isFull: false,
		});
	});

	it("is full once the cap is reached", () => {
		expect(
			getOpportunityCapacity({
				totalMaxParticipants: 5,
				currentParticipantCount: 5,
			}),
		).toEqual({
			kind: "capped",
			booked: 5,
			max: 5,
			spotsLeft: 0,
			isFull: true,
		});
	});

	// An over-booked opportunity is possible (a cap lowered after sign-ups);
	// "-2 spots left" is not something a card should ever say.
	it("never reports negative remaining places when a cap is over-subscribed", () => {
		expect(
			getOpportunityCapacity({
				totalMaxParticipants: 5,
				currentParticipantCount: 7,
			}),
		).toEqual({
			kind: "capped",
			booked: 7,
			max: 5,
			spotsLeft: 0,
			isFull: true,
		});
	});

	it("clamps a nonsensical negative sign-up count", () => {
		expect(
			getOpportunityCapacity({
				totalMaxParticipants: null,
				currentParticipantCount: -1,
			}),
		).toEqual({ kind: "unlimited", booked: 0 });
	});
});

describe("getCapacityFromTimeSlots", () => {
	it("folds slots the same way the SQL projection does: any uncapped slot means unlimited", () => {
		expect(
			getCapacityFromTimeSlots(
				[
					{ maxParticipants: 10, bookedCount: 2 },
					{ maxParticipants: null, bookedCount: 1 },
				],
				3,
			),
		).toEqual({ kind: "unlimited", booked: 3 });
	});

	it("sums the caps when every slot is capped", () => {
		expect(
			getCapacityFromTimeSlots(
				[
					{ maxParticipants: 10, bookedCount: 2 },
					{ maxParticipants: 10, bookedCount: 3 },
				],
				5,
			),
		).toEqual({
			kind: "capped",
			booked: 5,
			max: 20,
			spotsLeft: 15,
			isFull: false,
		});
	});

	// The detail page's equivalent of the summary's 0 - and the reason the
	// detail page and the card that linked to it now agree.
	it("reads an empty slot list as not applicable", () => {
		expect(getCapacityFromTimeSlots([], 0, "IndividualContact")).toEqual({
			kind: "notApplicable",
			booked: 0,
			reason: "interest",
		});
	});

	// An interest-based opportunity's sign-ups carry no time slot, so summing
	// slot bookings would report a busy opportunity as having nobody.
	it("keeps the opportunity's own sign-up count when there are no slots to sum", () => {
		expect(getCapacityFromTimeSlots([], 5, "IndividualContact")).toEqual({
			kind: "notApplicable",
			booked: 5,
			reason: "interest",
		});
	});

	it("matches getOpportunityCapacity for the same underlying data", () => {
		const fromSlots = getCapacityFromTimeSlots(
			[{ maxParticipants: 20, bookedCount: 1 }],
			1,
		);
		const fromSummary = getOpportunityCapacity({
			totalMaxParticipants: 20,
			currentParticipantCount: 1,
		});

		expect(fromSlots).toEqual(fromSummary);
	});
});
