import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import DangerZonePanel from "./DangerZonePanel";
import { renderWithProviders } from "../test/render";

describe("DangerZonePanel", () => {
	it("confines the error colour to the heading, leaving the explanation as body copy", () => {
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
