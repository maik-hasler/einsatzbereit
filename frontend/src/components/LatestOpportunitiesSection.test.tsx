import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, within } from "@testing-library/react";
import LatestOpportunitiesSection from "./LatestOpportunitiesSection";
import { renderWithProviders } from "../test/render";

/**
 * Was `HomePageOfflineTests` (#2065 finding 1), moved down in #2148 wave 2.
 *
 * The Playwright original pinned `navigator.onLine` through an init script
 * and aborted the list request at the route level - it could not use
 * `Context.SetOfflineAsync`, because this suite blocks service workers and a
 * genuinely offline document navigation could not have loaded the app shell
 * at all. Both halves of that setup are a rejected promise and one property
 * here.
 *
 * What stays end-to-end is `ErrorBoundaryOfflineTests`, where the failure is
 * a lazy route chunk's `import()` losing the network - a real browser
 * behaviour with no jsdom equivalent.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

function setOnline(value: boolean) {
	Object.defineProperty(navigator, "onLine", {
		configurable: true,
		get: () => value,
	});
}

beforeEach(() => {
	api.__reset();
	setOnline(true);
});

afterEach(() => {
	setOnline(true);
});

describe("LatestOpportunitiesSection while offline", () => {
	it("says it is offline instead of removing itself", async () => {
		// #2065: the section removed itself on any failure, offline included -
		// so a visitor reloading the landing page with no connection saw the
		// hero promise "find an opportunity that fits you" and then nothing
		// backing it up at all.
		setOnline(false);
		api.getVolunteerOpportunities.mockRejectedValue(new Error("network"));

		renderWithProviders(<LatestOpportunitiesSection />);

		const offline = await screen.findByTestId("landing-latest-offline");
		expect(offline).toHaveTextContent("You are offline");

		// The section's own heading and its way out both stay - only the grid
		// of cards is replaced.
		expect(
			screen.getByRole("heading", { name: "These opportunities need people" }),
		).toBeInTheDocument();

		// #2065 added a manual retry alongside the notice: the fallback for a
		// connection that comes back without the browser ever firing `online`.
		expect(
			within(offline).getByRole("button", { name: "Try again" }),
		).toBeInTheDocument();
	});

	it("still removes itself for a generic server error", async () => {
		// Deliberately left alone by #2065 - only the offline branch changed. A
		// real HTTP status keeps navigator.onLine true and gives useLoadMore's
		// errorIsOffline a false reading, so the section does not argue against
		// the hero above it.
		api.getVolunteerOpportunities.mockRejectedValue(
			Object.assign(new Error("server"), { status: 500 }),
		);

		renderWithProviders(<LatestOpportunitiesSection />);

		await vi.waitFor(() =>
			expect(api.getVolunteerOpportunities).toHaveBeenCalled(),
		);
		await vi.waitFor(() =>
			expect(
				screen.queryByRole("heading", {
					name: "These opportunities need people",
				}),
			).toBeNull(),
		);
		expect(screen.queryByTestId("landing-latest-offline")).toBeNull();
	});
});
