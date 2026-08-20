import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import DangerZonePanel from "./DangerZonePanel";
import { renderWithProviders } from "../test/render";

/**
 * Was the shared half of `DangerZonePanelTests` (#1792), moved down in #2148
 * wave 2. The Playwright original signed in twice - once as vera for
 * /profile/settings and once as olaf for the org settings page - to assert
 * the same two class attributes on the same component both times. Asserting
 * them once on the component is the whole of it; the two call sites are
 * covered by their own page tests alongside this one.
 */
describe("DangerZonePanel", () => {
	it("confines the error colour to the heading, leaving the explanation as body copy", () => {
		// #1792: the panel used to be headed "Danger zone" ("Gefahrenzone", a
		// calque of GitHub's label that reads like a translation artefact) and
		// set its whole explanation in text-red-700, spending the error colour
		// on prose instead of the signal.
		renderWithProviders(
			<DangerZonePanel
				title="Delete organization"
				description="This cannot be undone."
				actionLabel="Delete organization"
				onAction={() => {}}
			/>,
		);

		const heading = screen.getByRole("heading", {
			name: "Delete organization",
		});
		expect(heading).toHaveClass("text-red-800");

		const description = heading.nextElementSibling;
		expect(description?.tagName).toBe("P");
		expect(description).toHaveClass("text-gray-600");
		expect(description?.className).not.toMatch(/text-red/);
	});

	it("keeps the destructive action reachable and disables it on request", () => {
		const { rerender } = renderWithProviders(
			<DangerZonePanel
				title="Delete account"
				description="This cannot be undone."
				actionLabel="Delete my account"
				onAction={() => {}}
			/>,
		);
		expect(
			screen.getByRole("button", { name: "Delete my account" }),
		).toBeEnabled();

		rerender(
			<DangerZonePanel
				title="Delete account"
				description="This cannot be undone."
				actionLabel="Delete my account"
				disabled
				onAction={() => {}}
			/>,
		);
		expect(
			screen.getByRole("button", { name: "Delete my account" }),
		).toBeDisabled();
	});
});
