import { describe, it, expect } from "vitest";
import type { VolunteerOpportunitySummary } from "../client/api-client";
import { filterCheckInOpportunities, isQrCheckIn } from "./quickCheckIn";

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
