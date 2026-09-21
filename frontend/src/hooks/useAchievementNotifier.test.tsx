import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { useAchievementNotifier } from "./useAchievementNotifier";
import { renderWithProviders } from "../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

function Probe() {
	useAchievementNotifier();
	return <span data-testid="probe" />;
}

const badge = (id: string, name: string) => ({ id, key: undefined, name });

beforeEach(() => {
	api.__reset();
	localStorage.clear();
});

const render = () =>
	renderWithProviders(<Probe />, { auth: { isAuthenticated: true } });

describe("useAchievementNotifier on a fresh browser", () => {
	it("does not re-announce achievements the user already had", async () => {
		api.getMyAchievements.mockResolvedValue([
			badge("aaaa0001-0000-0000-0000-000000000001", "First sign-up"),
			badge("aaaa0002-0000-0000-0000-000000000002", "Five confirmations"),
		]);

		render();

		await waitFor(() => expect(api.getMyAchievements).toHaveBeenCalled());
		expect(screen.queryByRole("alert")).toBeNull();
		expect(document.body.textContent).not.toMatch(/New badge unlocked/);
	});

	it("announces one earned after that first poll", async () => {
		api.getMyAchievements.mockResolvedValue([
			badge("aaaa0001-0000-0000-0000-000000000001", "First sign-up"),
		]);

		const { unmount } = render();
		// Wait for the first poll to be *recorded*, not merely requested: the
		// precondition for this test is that the browser already knows about the
		// badge the user had. Waiting on the request alone let the unmount land
		// between the response and the write, which seeds nothing and makes the
		// second render announce both badges as new.
		await waitFor(() => expect(localStorage.length).toBeGreaterThan(0));
		unmount();

		api.getMyAchievements.mockResolvedValue([
			badge("aaaa0001-0000-0000-0000-000000000001", "First sign-up"),
			badge("aaaa0002-0000-0000-0000-000000000002", "Five confirmations"),
		]);

		render();

		expect(
			await screen.findByText(/New badge unlocked: Five confirmations/),
		).toBeInTheDocument();
	});
});
