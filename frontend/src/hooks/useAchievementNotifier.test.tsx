import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { useAchievementNotifier } from "./useAchievementNotifier";
import { renderWithProviders } from "../test/render";

/**
 * `AchievementsTests`' seeding case, moved down in #2148 wave 13. Remaining
 * inventory: #2159.
 *
 * The first successful poll on a device seeds whatever the user already has as
 * already-seen, rather than announcing all of it as newly unlocked - otherwise
 * anyone opening the app in a new browser got a burst of toasts for badges
 * they earned months ago. The E2E's long body was all HTTP seeding to
 * guarantee olaf owned a badge; here that is the mocked response.
 *
 * The seen-set lives in `localStorage`, keyed per user id (#1236), so these
 * clear it between cases the way a fresh browser context did.
 */
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
		// The absence has to be checked after the poll has actually resolved, or
		// it passes trivially. `findAllByRole` would throw; a settled poll plus a
		// query is the honest form.
		expect(screen.queryByRole("alert")).toBeNull();
		expect(document.body.textContent).not.toMatch(/New badge unlocked/);
	});

	it("announces one earned after that first poll", async () => {
		// The branch that makes the case above more than "this hook never
		// toasts": once seeded, a genuinely new badge does announce.
		api.getMyAchievements.mockResolvedValue([
			badge("aaaa0001-0000-0000-0000-000000000001", "First sign-up"),
		]);

		const { unmount } = render();
		await waitFor(() => expect(api.getMyAchievements).toHaveBeenCalled());
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
