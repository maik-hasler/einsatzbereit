import { describe, it, expect } from "vitest";
import type { VolunteerOpportunitySummary } from "../client/api-client";
import {
	filterCheckInOpportunities,
	isQrCheckIn,
	pickCheckInOpportunity,
} from "./quickCheckIn";

function makeOpportunity(
	overrides: Partial<VolunteerOpportunitySummary>,
): VolunteerOpportunitySummary {
	return {
		id: "opp-1",
		titleDe: "Opportunity",
		titleEn: undefined,
		descriptionDe: undefined,
		descriptionEn: undefined,
		organizationId: "org-1",
		organizationName: "Org",
		street: undefined,
		houseNumber: undefined,
		zipCode: undefined,
		city: undefined,
		latitude: undefined,
		longitude: undefined,
		isRemote: true,
		occurrence: "OneTime",
		participationType: "ScheduledSlots",
		checkInMethod: "QRCode",
		category: undefined,
		tags: [],
		createdOn: new Date("2026-01-01"),
		validUntil: undefined,
		nextTimeSlotStart: undefined,
		nextTimeSlotEnd: undefined,
		totalMaxParticipants: undefined,
		currentParticipantCount: 0,
		status: "Published",
		bannerImageUrl: undefined,
		...overrides,
	};
}

describe("filterCheckInOpportunities", () => {
	it("keeps every method an organizer can act on, not just QR", () => {
		const opportunities = [
			makeOpportunity({ id: "qr", checkInMethod: "QRCode" }),
			makeOpportunity({ id: "pin", checkInMethod: "PINCode" }),
			makeOpportunity({ id: "manual", checkInMethod: "Manual" }),
		];

		expect(filterCheckInOpportunities(opportunities)).toEqual(opportunities);
	});

	// "None" volunteers are marked checked in automatically once the event
	// ends, so there is nothing for the widget to offer.
	it("drops opportunities with no check-in step at all", () => {
		const none = makeOpportunity({ id: "none", checkInMethod: "None" });
		const qr = makeOpportunity({ id: "qr", checkInMethod: "QRCode" });

		expect(filterCheckInOpportunities([none, qr])).toEqual([qr]);
	});

	it("preserves the order it was given", () => {
		const pin = makeOpportunity({ id: "pin", checkInMethod: "PINCode" });
		const qr = makeOpportunity({ id: "qr", checkInMethod: "QRCode" });

		expect(filterCheckInOpportunities([pin, qr]).map((o) => o.id)).toEqual([
			"pin",
			"qr",
		]);
	});

	it("returns an empty array for an empty input", () => {
		expect(filterCheckInOpportunities([])).toEqual([]);
	});
});

describe("isQrCheckIn", () => {
	it("is true only for QR code check-in", () => {
		for (const method of ["QRCode", "PINCode", "Manual", "None"] as const) {
			expect({
				method,
				scannable: isQrCheckIn(makeOpportunity({ checkInMethod: method })),
			}).toEqual({ method, scannable: method === "QRCode" });
		}
	});

	it("is false when nothing is selected yet", () => {
		expect(isQrCheckIn(undefined)).toBe(false);
	});
});

describe("pickCheckInOpportunity", () => {
	const NOW = Date.UTC(2026, 8, 14, 12, 0, 0);
	const HOUR = 60 * 60 * 1000;

	function scheduled(
		id: string,
		startOffsetHours: number,
		lengthHours = 2,
	): VolunteerOpportunitySummary {
		const start = NOW + startOffsetHours * HOUR;
		return makeOpportunity({
			id,
			nextTimeSlotStart: new Date(start) as unknown as Date,
			nextTimeSlotEnd: new Date(start + lengthHours * HOUR) as unknown as Date,
		});
	}

	it("returns nothing for an empty list", () => {
		expect(pickCheckInOpportunity([], NOW)).toBeUndefined();
	});

	// The whole point: an organizer at the door of the Saturday market stall
	// should not have to notice that the tile picked the Christmas party.
	it("prefers the occurrence that is running right now", () => {
		const picked = pickCheckInOpportunity(
			[
				scheduled("later", 3),
				scheduled("running", -1, 3),
				scheduled("soon", 1),
			],
			NOW,
		);

		expect(picked?.id).toBe("running");
	});

	it("falls back to the occurrence starting soonest", () => {
		const picked = pickCheckInOpportunity(
			[scheduled("in-five", 5), scheduled("in-one", 1)],
			NOW,
		);

		expect(picked?.id).toBe("in-one");
	});

	it("prefers anything scheduled over an opportunity with no slots at all", () => {
		const picked = pickCheckInOpportunity(
			[makeOpportunity({ id: "unscheduled" }), scheduled("in-a-week", 24 * 7)],
			NOW,
		);

		expect(picked?.id).toBe("in-a-week");
	});

	it("prefers an upcoming occurrence over one that has already finished", () => {
		const picked = pickCheckInOpportunity(
			[scheduled("finished", -30), scheduled("ahead", 48)],
			NOW,
		);

		expect(picked?.id).toBe("ahead");
	});

	it("takes the most recently finished one when nothing is ahead", () => {
		const picked = pickCheckInOpportunity(
			[scheduled("last-week", -24 * 7), scheduled("yesterday", -24)],
			NOW,
		);

		expect(picked?.id).toBe("yesterday");
	});

	// A slot with a start but no end still has to rank; it is treated as an
	// instant rather than dropped, so the tile never falls back to list order.
	it("ranks an opportunity whose slot has no end time", () => {
		const openEnded = makeOpportunity({
			id: "open-ended",
			nextTimeSlotStart: new Date(NOW + HOUR) as unknown as Date,
			nextTimeSlotEnd: undefined,
		});

		expect(
			pickCheckInOpportunity([makeOpportunity({ id: "none" }), openEnded], NOW)
				?.id,
		).toBe("open-ended");
	});

	it("still returns something when nothing has a schedule", () => {
		const picked = pickCheckInOpportunity(
			[makeOpportunity({ id: "a" }), makeOpportunity({ id: "b" })],
			NOW,
		);

		expect(picked?.id).toBe("a");
	});
});
