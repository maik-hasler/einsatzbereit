import { describe, it, expect } from "vitest";
import type { VolunteerOpportunitySummary } from "../client/api-client";
import {
	MAX_UPCOMING_ITEMS,
	selectUpcomingOpportunities,
} from "./upcomingOpportunities";

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
		createdOn: "2026-01-01T00:00:00Z" as unknown as Date,
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

function upcoming(id: string, startIso: string): VolunteerOpportunitySummary {
	return makeOpportunity({
		id,
		nextTimeSlotStart: startIso as unknown as Date,
	});
}

describe("selectUpcomingOpportunities", () => {
	it("keeps the wire value as a string the caller can format directly", () => {
		const items = selectUpcomingOpportunities(
			[upcoming("a", "2026-08-15T09:00:00Z")],
			"Untitled draft",
			"de",
		);

		expect(items).toHaveLength(1);
		expect(items[0].nextStart).toBe("2026-08-15T09:00:00Z");
		expect(items[0].nextStartMs).toBe(
			new Date("2026-08-15T09:00:00Z").getTime(),
		);
	});

	it("drops opportunities with no upcoming slot", () => {
		const items = selectUpcomingOpportunities(
			[
				upcoming("has-slot", "2026-08-15T09:00:00Z"),
				makeOpportunity({ id: "interest-based" }),
			],
			"Untitled draft",
			"de",
		);

		expect(items.map((i) => i.id)).toEqual(["has-slot"]);
	});

	it("drops an unparseable start rather than rendering an invalid date", () => {
		const items = selectUpcomingOpportunities(
			[upcoming("broken", "not-a-date")],
			"Untitled draft",
			"de",
		);

		expect(items).toEqual([]);
	});

	it("orders by soonest slot first", () => {
		const items = selectUpcomingOpportunities(
			[
				upcoming("later", "2026-08-20T09:00:00Z"),
				upcoming("soonest", "2026-08-13T09:00:00Z"),
				upcoming("middle", "2026-08-15T09:00:00Z"),
			],
			"Untitled draft",
			"de",
		);

		expect(items.map((i) => i.id)).toEqual(["soonest", "middle", "later"]);
	});

	it("caps the list at MAX_UPCOMING_ITEMS", () => {
		const opportunities = Array.from(
			{ length: MAX_UPCOMING_ITEMS + 3 },
			(_, i) =>
				upcoming(
					`opp-${i}`,
					`2026-08-${String(10 + i).padStart(2, "0")}T09:00:00Z`,
				),
		);

		expect(
			selectUpcomingOpportunities(opportunities, "Untitled draft", "de"),
		).toHaveLength(MAX_UPCOMING_ITEMS);
	});

	it("falls back to the caller's title for an untitled draft", () => {
		const items = selectUpcomingOpportunities(
			[
				makeOpportunity({
					titleDe: "",
					nextTimeSlotStart: "2026-08-15T09:00:00Z" as unknown as Date,
				}),
			],
			"Untitled draft",
			"de",
		);

		expect(items[0].title).toBe("Untitled draft");
	});

	it("carries capacity through, with null meaning unlimited", () => {
		const items = selectUpcomingOpportunities(
			[
				makeOpportunity({
					id: "capped",
					nextTimeSlotStart: "2026-08-15T09:00:00Z" as unknown as Date,
					currentParticipantCount: 3,
					totalMaxParticipants: 10,
				}),
				makeOpportunity({
					id: "unlimited",
					nextTimeSlotStart: "2026-08-16T09:00:00Z" as unknown as Date,
					currentParticipantCount: 2,
					totalMaxParticipants: undefined,
				}),
			],
			"Untitled draft",
			"de",
		);

		expect(items.map((i) => [i.id, i.bookedCount, i.maxParticipants])).toEqual([
			["capped", 3, 10],
			["unlimited", 2, null],
		]);
	});

	it("carries the participation type through", () => {
		const items = selectUpcomingOpportunities(
			[
				makeOpportunity({
					id: "slots",
					nextTimeSlotStart: "2026-08-15T09:00:00Z" as unknown as Date,
					participationType: "ScheduledSlots",
				}),
			],
			"Untitled draft",
			"de",
		);

		expect(items[0].participationType).toBe("ScheduledSlots");
	});

	it("returns an empty array for an empty input", () => {
		expect(selectUpcomingOpportunities([], "Untitled draft", "de")).toEqual([]);
	});

	it("prefers the English title when the viewer's language is English", () => {
		const items = selectUpcomingOpportunities(
			[
				makeOpportunity({
					titleDe: "Deutscher Titel",
					titleEn: "English Title",
					nextTimeSlotStart: "2026-08-15T09:00:00Z" as unknown as Date,
				}),
			],
			"Untitled draft",
			"en",
		);

		expect(items[0].title).toBe("English Title");
	});

	it("falls back to the German title when English wasn't provided", () => {
		const items = selectUpcomingOpportunities(
			[
				makeOpportunity({
					titleDe: "Deutscher Titel",
					titleEn: undefined,
					nextTimeSlotStart: "2026-08-15T09:00:00Z" as unknown as Date,
				}),
			],
			"Untitled draft",
			"en",
		);

		expect(items[0].title).toBe("Deutscher Titel");
	});
});
