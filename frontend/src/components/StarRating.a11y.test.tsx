import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import StarRating from "./StarRating";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

describe("StarRating accessibility", () => {
	it("exposes the score as one image with a spoken name, not five nameless glyphs", async () => {
		renderWithProviders(<StarRating rating={3} />);

		expect(
			screen.getByRole("img", { name: "3 out of 5 stars" }),
		).toBeInTheDocument();
		await expectNoA11yViolations();
	});

	it("has no violations at the smaller size either", async () => {
		renderWithProviders(<StarRating rating={5} size="sm" />);

		await expectNoA11yViolations();
	});
});
