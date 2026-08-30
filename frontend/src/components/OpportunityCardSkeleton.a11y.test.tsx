import { describe, it } from "vitest";
import OpportunityCardSkeleton from "./OpportunityCardSkeleton";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

describe("OpportunityCardSkeleton a11y", () => {
	it("has no violations without a media band", async () => {
		renderWithProviders(<OpportunityCardSkeleton />);
		await expectNoA11yViolations();
	});

	it("has no violations with one", async () => {
		renderWithProviders(<OpportunityCardSkeleton withMedia />);
		await expectNoA11yViolations();
	});
});
