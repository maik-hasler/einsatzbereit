import { describe, it, expect, vi } from "vitest";
import type {
	EinsatzbereitApi,
	PagedListOfVolunteerOpportunitySummary,
} from "../client/api-client";
import { fetchVolunteerOpportunities } from "./volunteerOpportunities";

function fakeApi(): { getVolunteerOpportunities: ReturnType<typeof vi.fn> } {
	return {
		getVolunteerOpportunities: vi
			.fn()
			.mockResolvedValue({} as PagedListOfVolunteerOpportunitySummary),
	};
}

describe("fetchVolunteerOpportunities", () => {
	it("forwards required options and leaves the rest undefined", async () => {
		const api = fakeApi();
		await fetchVolunteerOpportunities(api as unknown as EinsatzbereitApi, {
			pageNumber: 1,
			pageSize: 10,
		});

		expect(api.getVolunteerOpportunities).toHaveBeenCalledWith(
			1,
			10,
			undefined,
			undefined,
			undefined,
			undefined,
			undefined,
			undefined,
			undefined,
			undefined,
			undefined,
			undefined,
			undefined,
			undefined,
			undefined,
			undefined,
			undefined,
			undefined,
			undefined,
		);
	});

	it("forwards every option in the positional order the generated client expects", async () => {
		const api = fakeApi();
		const dateFrom = new Date("2024-01-01");
		const dateTo = new Date("2024-02-01");
		const signal = new AbortController().signal;

		await fetchVolunteerOpportunities(
			api as unknown as EinsatzbereitApi,
			{
				pageNumber: 2,
				pageSize: 20,
				city: "Berlin",
				occurrence: "Recurring",
				participationType: "Waitlist",
				isRemote: true,
				dateFrom,
				dateTo,
				north: 1,
				south: 2,
				east: 3,
				west: 4,
				centerLatitude: 5,
				centerLongitude: 6,
				radiusKm: 7,
				categories: ["environment"],
				tag: "cleanup",
				search: "food bank",
			},
			signal,
		);

		expect(api.getVolunteerOpportunities).toHaveBeenCalledWith(
			2,
			20,
			"Berlin",
			"Recurring",
			"Waitlist",
			true,
			dateFrom,
			dateTo,
			1,
			2,
			3,
			4,
			5,
			6,
			7,
			["environment"],
			"cleanup",
			"food bank",
			signal,
		);
	});

	it("returns whatever the underlying API call resolves with", async () => {
		const api = fakeApi();
		const page = {
			items: [],
			totalCount: 0,
		} as unknown as PagedListOfVolunteerOpportunitySummary;
		api.getVolunteerOpportunities.mockResolvedValue(page);

		const result = await fetchVolunteerOpportunities(
			api as unknown as EinsatzbereitApi,
			{ pageNumber: 1, pageSize: 10 },
		);

		expect(result).toBe(page);
	});
});
