import { describe, it, expect } from "vitest";
import type { VolunteerOpportunitySummary } from "../client/api-client";
import { filterQrCheckInOpportunities } from "./quickCheckIn";

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

describe("filterQrCheckInOpportunities", () => {
	it("keeps opportunities using QR code check-in", () => {
		const qrOpp = makeOpportunity({ id: "qr", checkInMethod: "QRCode" });

		expect(filterQrCheckInOpportunities([qrOpp])).toEqual([qrOpp]);
	});

	it("drops opportunities using PIN code, manual, or no check-in method", () => {
		const opportunities = [
			makeOpportunity({ id: "pin", checkInMethod: "PINCode" }),
			makeOpportunity({ id: "manual", checkInMethod: "Manual" }),
			makeOpportunity({ id: "none", checkInMethod: "None" }),
		];

		expect(filterQrCheckInOpportunities(opportunities)).toEqual([]);
	});

	it("filters a mixed list down to only the QR ones, preserving order", () => {
		const qr1 = makeOpportunity({ id: "qr1", checkInMethod: "QRCode" });
		const pin = makeOpportunity({ id: "pin", checkInMethod: "PINCode" });
		const qr2 = makeOpportunity({ id: "qr2", checkInMethod: "QRCode" });

		expect(filterQrCheckInOpportunities([qr1, pin, qr2])).toEqual([qr1, qr2]);
	});

	it("returns an empty array for an empty input", () => {
		expect(filterQrCheckInOpportunities([])).toEqual([]);
	});
});
