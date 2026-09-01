import { describe, it, expect } from "vitest";
import type { OrganizationCalendarEventDto } from "../client/api-client";
import { selectUpcomingSlots, MAX_UPCOMING_SLOTS } from "./upcomingSlots";

const NOW = Date.UTC(2026, 8, 14, 12, 0, 0);
const HOUR = 60 * 60 * 1000;

// The generated DTO types these as Date, but they arrive over the wire as ISO
// strings and every reader treats them that way - so the fixtures are strings
// and the cast happens once, here, rather than at nine call sites.
function slot(id: string, startOffsetHours: number, lengthHours = 2) {
	const start = NOW + startOffsetHours * HOUR;
	return {
		timeSlotId: id,
		startDateTime: new Date(start).toISOString(),
		endDateTime: new Date(start + lengthHours * HOUR).toISOString(),
		maxParticipants: 6,
		bookedCount: 2,
	};
}

function event(
	overrides: Record<string, unknown> = {},
): OrganizationCalendarEventDto {
	return {
		opportunityId: "opp-1",
		titleDe: "Blutspendetermin begleiten",
		titleEn: "Support a blood donation drive",
		color: undefined,
		status: "Published",
		timeSlots: [],
		...overrides,
	} as unknown as OrganizationCalendarEventDto;
}

function select(
	events: OrganizationCalendarEventDto[],
	lang = "en",
	limit?: number,
) {
	return selectUpcomingSlots(events, NOW, "Unnamed draft", lang, limit);
}

describe("selectUpcomingSlots", () => {
	it("flattens every occurrence of a repeating opportunity into its own row", () => {
		const result = select([
			event({ timeSlots: [slot("a", 24), slot("b", 48), slot("c", 72)] }),
		]);

		expect(result.map((s) => s.id)).toEqual(["a", "b", "c"]);
		expect(new Set(result.map((s) => s.opportunityId))).toEqual(
			new Set(["opp-1"]),
		);
	});

	it("orders across opportunities by when each occurrence starts", () => {
		const result = select([
			event({ opportunityId: "late", timeSlots: [slot("late-1", 50)] }),
			event({ opportunityId: "soon", timeSlots: [slot("soon-1", 5)] }),
		]);

		expect(result.map((s) => s.id)).toEqual(["soon-1", "late-1"]);
	});

	// The organizer is most likely to open the board during the shift, which is
	// exactly when a start-time filter would have dropped it off the list.
	it("keeps an occurrence that is running right now", () => {
		const result = select([event({ timeSlots: [slot("running", -1, 3)] })]);

		expect(result.map((s) => s.id)).toEqual(["running"]);
	});

	it("drops occurrences that have already finished", () => {
		const result = select([
			event({ timeSlots: [slot("over", -10, 2), slot("next", 10)] }),
		]);

		expect(result.map((s) => s.id)).toEqual(["next"]);
	});

	it("drops occurrences whose timestamps do not parse", () => {
		const result = select([
			event({
				timeSlots: [
					{
						timeSlotId: "broken",
						startDateTime: "not a date",
						endDateTime: "not a date either",
						maxParticipants: 4,
						bookedCount: 0,
					},
					slot("fine", 6),
				],
			}),
		]);

		expect(result.map((s) => s.id)).toEqual(["fine"]);
	});

	// Nobody can sign up to a draft, so an empty one is not a shift short of
	// people - and sorted purely by time it looked like the most urgent row on
	// the board.
	it("leaves out occurrences of anything that is not published", () => {
		const timeSlots = [slot("a", 3)];

		expect(select([event({ status: "Draft", timeSlots })])).toEqual([]);
		expect(select([event({ status: "Cancelled", timeSlots })])).toEqual([]);
		expect(select([event({ status: "Unpublished", timeSlots })])).toEqual([]);
		expect(select([event({ status: "Published", timeSlots })])).toHaveLength(1);
	});

	it("caps the list at the tile's row budget", () => {
		const timeSlots = Array.from({ length: MAX_UPCOMING_SLOTS + 4 }, (_, i) =>
			slot(`s-${i}`, i + 1),
		);

		expect(select([event({ timeSlots })])).toHaveLength(MAX_UPCOMING_SLOTS);
		expect(select([event({ timeSlots })], "en", 2)).toHaveLength(2);
	});

	it("takes the title from the requested locale and reports which one it used", () => {
		const [english] = select([event({ timeSlots: [slot("a", 3)] })], "en");
		const [german] = select([event({ timeSlots: [slot("a", 3)] })], "de");

		expect(english).toMatchObject({
			title: "Support a blood donation drive",
			titleLang: "en",
		});
		expect(german).toMatchObject({
			title: "Blutspendetermin begleiten",
			titleLang: "de",
		});
	});

	it("falls back to the draft placeholder when the title is blank", () => {
		const [only] = select([
			event({ titleDe: "", titleEn: "", timeSlots: [slot("a", 3)] }),
		]);

		expect(only.title).toBe("Unnamed draft");
	});

	it("carries an uncapped occurrence's places through as null", () => {
		const [only] = select([
			event({
				timeSlots: [{ ...slot("a", 3), maxParticipants: undefined }],
			}),
		]);

		expect(only.maxParticipants).toBeNull();
		expect(only.bookedCount).toBe(2);
	});
});
