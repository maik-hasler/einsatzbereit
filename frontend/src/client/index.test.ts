import { describe, it, expect, vi, beforeEach } from "vitest";

const getVolunteerOpportunityDetails = vi.fn();
const deleteNotification = vi.fn();

vi.mock("./generated/sdk.gen", () => ({
	getVolunteerOpportunityDetails: (options: unknown) =>
		getVolunteerOpportunityDetails(options),
	deleteNotification: (options: unknown) => deleteNotification(options),
	notAFunction: "a re-exported constant",
}));

const { api } = await import("./index");

beforeEach(() => {
	vi.clearAllMocks();
});

describe("the API client seam", () => {
	it("passes the options through to the generated operation untouched", async () => {
		getVolunteerOpportunityDetails.mockResolvedValue({ data: {} });
		const options = { path: { opportunityId: "abc" } };

		await api.getVolunteerOpportunityDetails(options);

		expect(getVolunteerOpportunityDetails).toHaveBeenCalledExactlyOnceWith(
			options,
		);
	});

	// The whole reason this seam exists: 151 call sites and 347 test mocks are
	// written against the body, not against { data, request, response }.
	it("resolves to the response body, not the envelope", async () => {
		const body = { id: "abc", title: "Beach cleanup" };
		getVolunteerOpportunityDetails.mockResolvedValue({
			data: body,
			request: new Request("https://api.test/v1/x"),
			response: new Response(),
		});

		await expect(
			api.getVolunteerOpportunityDetails({ path: { opportunityId: "abc" } }),
		).resolves.toBe(body);
	});

	it("resolves to undefined for an operation that returns no content", async () => {
		deleteNotification.mockResolvedValue({ data: undefined });

		await expect(
			api.deleteNotification({ path: { id: "n1" } }),
		).resolves.toBeUndefined();
	});

	// lib/apiError.ts reads `status`, `errorCode`, `detail` and `errors` off the
	// rejected value. Wrapping or re-throwing here would break every catch in
	// the app at once.
	it("rejects with exactly what the generated operation threw", async () => {
		const problem = {
			status: 409,
			errorCode: "Engagement.AlreadySignedUp",
			detail: "internal detail",
		};
		getVolunteerOpportunityDetails.mockRejectedValue(problem);

		await expect(
			api.getVolunteerOpportunityDetails({ path: { opportunityId: "abc" } }),
		).rejects.toBe(problem);
	});

	// Awaiting the client itself, or letting a dev tool probe it, reads symbol
	// and well-known keys off the proxy. None of them is an operation.
	it("is not itself thenable", async () => {
		expect((api as unknown as Record<string, unknown>).then).toBeUndefined();
		await expect(Promise.resolve(api)).resolves.toBe(api);
	});

	// A fresh closure per property access would give every operation a new
	// identity on every render - the shape of bug that ends up in a dependency
	// array and re-runs an effect forever.
	it("hands back the same function every time an operation is read", () => {
		expect(api.getVolunteerOpportunityDetails).toBe(
			api.getVolunteerOpportunityDetails,
		);
	});

	it("exposes nothing for a non-function export", () => {
		expect(
			(api as unknown as Record<string, unknown>).notAFunction,
		).toBeUndefined();
	});
});
