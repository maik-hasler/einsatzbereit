import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ProfileOverviewPage from "./index";
import { inputClass, labelClass } from "../../lib/formClasses";
import { renderWithProviders } from "../../test/render";

/**
 * Was the profile half of `SharedFormClassesTests` (#536/#1104/#1109),
 * moved down in #2148 wave 2.
 */
// Top-level await on vi.hoisted: the factory is hoisted above every import in
// this file, so it cannot reference an imported createApiMock and has to pull
// it in itself. See src/test/apiMock.ts for why pages use this instead of
// listing each endpoint by hand.
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../hooks/useApiClient", () => ({ useApiClient: () => api }));

beforeEach(() => {
	api.__reset();
	api.getUserProfile.mockResolvedValue({
		firstName: "Vera",
		lastName: "Volunteer",
		bio: "Ich helfe gern.",
		skills: [],
		languages: [],
		phone: undefined,
		preferredContact: undefined,
		preferredLanguage: undefined,
		avatarUrl: undefined,
	});
	api.getMyStreaks.mockResolvedValue({
		loginStreak: 0,
		activityStreak: 0,
		confirmedEngagements: 0,
	});
	api.getMyAchievements.mockResolvedValue([]);
	api.getBadgeCatalog.mockResolvedValue([]);
});

describe("ProfileOverviewPage edit form", () => {
	it("uses the shared input and label classes rather than page-local copies", async () => {
		// #536: this page and OrgSettingsPage each defined their own local
		// inputClass constant; #1109: each also had its own Field helper with a
		// hardcoded label style that diverged from labelClass on the very same
		// form. Compared against the imported constants, so a deliberate change
		// to the shared recipe does not have to be re-typed here.
		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		// Fields render read-only until "Edit" - the header button once the
		// profile has data, or the empty-state CTA while it does not (#2066);
		// both carry the same testid.
		const edit = await screen.findByTestId("profile-edit");
		await userEvent.click(edit);

		await waitFor(() =>
			expect(document.querySelector("#first-name")).not.toBeNull(),
		);
		expect(document.querySelector("#first-name")).toHaveAttribute(
			"class",
			inputClass,
		);
		expect(document.querySelector("label[for='first-name']")).toHaveAttribute(
			"class",
			labelClass,
		);
	});
});

describe("ProfileOverviewPage save feedback", () => {
	it("keeps the success region mounted and empty until a save succeeds", async () => {
		// #972: a role="status" node inserted into the DOM already populated
		// does not reliably announce, so this one is mounted from the start and
		// written into on save rather than rendered on demand.
		api.updateUserProfile.mockResolvedValue(undefined);
		renderWithProviders(<ProfileOverviewPage />, {
			auth: { isAuthenticated: true },
		});

		const banner = await screen.findByRole("status");
		expect(banner).toHaveAttribute("aria-live", "polite");
		expect(banner).toHaveTextContent("");

		await userEvent.click(await screen.findByTestId("profile-edit"));
		await userEvent.click(
			await screen.findByRole("button", { name: /^Save$/ }),
		);

		await waitFor(() =>
			expect(screen.getByRole("status")).toHaveTextContent("Profile saved."),
		);
	});
});
