import { describe, it, expect, vi } from "vitest";
import type {
	ApiClient,
	PagedListOfVolunteerOpportunitySummary,
	VolunteerOpportunityAvailableDate,
} from "../client";
import {
	fetchVolunteerOpportunities,
	fetchVolunteerOpportunityDateAvailability,
} from "./volunteerOpportunities";

function fakeApi(): { getVolunteerOpportunities: ReturnType<typeof vi.fn> } {
	return {
		getVolunteerOpportunities: vi
			.fn()
			.mockResolvedValue({} as PagedListOfVolunteerOpportunitySummary),
	};
}

function fakeAvailabilityApi(): {
	getVolunteerOpportunityDateAvailability: ReturnType<typeof vi.fn>;
} {
	return {
		getVolunteerOpportunityDateAvailability: vi
			.fn()
			.mockResolvedValue([] as VolunteerOpportunityAvailableDate[]),
	};
}

describe("fetchVolunteerOpportunities", () => {
	it("forwards required options and leaves the rest undefined", async () => {
		const api = fakeApi();
		await fetchVolunteerOpportunities(api as unknown as ApiClient, {
			pageNumber: 1,
			pageSize: 10,
		});

		expect(api.getVolunteerOpportunities).toHaveBeenCalledWith({
			query: {
				PageNumber: 1,
				PageSize: 10,
				Occurrence: undefined,
				ParticipationType: undefined,
				IsRemote: undefined,
				DateFrom: undefined,
				DateTo: undefined,
				CenterLatitude: undefined,
				CenterLongitude: undefined,
				RadiusKm: undefined,
				Categories: undefined,
				Tag: undefined,
				Keyword: undefined,
			},
			signal: undefined,
		});
	});

	it("forwards every option in the query shape the generated client expects", async () => {
		const api = fakeApi();
		const dateFrom = new Date("2024-01-01");
		const dateTo = new Date("2024-02-01");
		const signal = new AbortController().signal;

		await fetchVolunteerOpportunities(
			api as unknown as ApiClient,
			{
				pageNumber: 2,
				pageSize: 20,
				occurrence: "Recurring",
				participationType: "ScheduledSlots",
				isRemote: true,
				dateFrom,
				dateTo,
				centerLatitude: 5,
				centerLongitude: 6,
				radiusKm: 7,
				categories: ["environment"],
				tag: "cleanup",
				keyword: "beach",
			},
			signal,
		);

		expect(api.getVolunteerOpportunities).toHaveBeenCalledWith({
			query: {
				PageNumber: 2,
				PageSize: 20,
				Occurrence: "Recurring",
				ParticipationType: "ScheduledSlots",
				IsRemote: true,
				DateFrom: dateFrom,
				DateTo: dateTo,
				CenterLatitude: 5,
				CenterLongitude: 6,
				RadiusKm: 7,
				Categories: ["environment"],
				Tag: "cleanup",
				Keyword: "beach",
			},
			signal,
		});
	});

	it("returns whatever the underlying API call resolves with", async () => {
		const api = fakeApi();
		const page = {
			items: [],
			totalCount: 0,
		} as unknown as PagedListOfVolunteerOpportunitySummary;
		api.getVolunteerOpportunities.mockResolvedValue(page);

		const result = await fetchVolunteerOpportunities(
			api as unknown as ApiClient,
			{ pageNumber: 1, pageSize: 10 },
		);

		expect(result).toBe(page);
	});
});

describe("fetchVolunteerOpportunityDateAvailability", () => {
	it("forwards the window and leaves every filter (including the unused timezone slot) undefined", async () => {
		const api = fakeAvailabilityApi();
		const from = new Date("2026-08-01T00:00:00");
		const to = new Date("2026-08-31T23:59:59.999");

		await fetchVolunteerOpportunityDateAvailability(
			api as unknown as ApiClient,
			{ from, to },
		);

		// UtcOffsetMinutes is the generated client's legacy timezone param -
		// always undefined now that the server derives the caller's zone from
		// the X-Timezone header instead (see volunteerOpportunities.ts, #2203).
		expect(api.getVolunteerOpportunityDateAvailability).toHaveBeenCalledWith({
			query: {
				From: from,
				To: to,
				UtcOffsetMinutes: undefined,
				Occurrence: undefined,
				ParticipationType: undefined,
				IsRemote: undefined,
				CenterLatitude: undefined,
				CenterLongitude: undefined,
				RadiusKm: undefined,
				Categories: undefined,
				Tag: undefined,
				Keyword: undefined,
			},
			signal: undefined,
		});
	});

	it("forwards every option in the query shape the generated client expects", async () => {
		const api = fakeAvailabilityApi();
		const from = new Date("2026-08-01T00:00:00");
		const to = new Date("2026-08-31T23:59:59.999");
		const signal = new AbortController().signal;

		await fetchVolunteerOpportunityDateAvailability(
			api as unknown as ApiClient,
			{
				from,
				to,
				occurrence: "Recurring",
				participationType: "ScheduledSlots",
				isRemote: false,
				centerLatitude: 5,
				centerLongitude: 6,
				radiusKm: 7,
				categories: ["Environment"],
				tag: "cleanup",
				keyword: "beach",
			},
			signal,
		);

		expect(api.getVolunteerOpportunityDateAvailability).toHaveBeenCalledWith({
			query: {
				From: from,
				To: to,
				UtcOffsetMinutes: undefined,
				Occurrence: "Recurring",
				ParticipationType: "ScheduledSlots",
				IsRemote: false,
				CenterLatitude: 5,
				CenterLongitude: 6,
				RadiusKm: 7,
				Categories: ["Environment"],
				Tag: "cleanup",
				Keyword: "beach",
			},
			signal,
		});
	});

	it("returns whatever the underlying API call resolves with", async () => {
		const api = fakeAvailabilityApi();
		const days: VolunteerOpportunityAvailableDate[] = [
			{ date: "2026-08-13", opportunityCount: 2 },
		];
		api.getVolunteerOpportunityDateAvailability.mockResolvedValue(days);

		const result = await fetchVolunteerOpportunityDateAvailability(
			api as unknown as ApiClient,
			{
				from: new Date("2026-08-01T00:00:00"),
				to: new Date("2026-08-31T23:59:59.999"),
			},
		);

		expect(result).toBe(days);
	});
});
