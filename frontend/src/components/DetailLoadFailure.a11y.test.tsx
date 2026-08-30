import { describe, it } from "vitest";
import DetailLoadFailure from "./DetailLoadFailure";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

describe("DetailLoadFailure a11y", () => {
	const kinds = ["notFound", "offline", "error"] as const;

	for (const kind of kinds) {
		it(`has no violations in the ${kind} state`, async () => {
			renderWithProviders(
				<DetailLoadFailure
					kind={kind}
					notFoundTitle="Organization not found"
					notFoundMessage="This organization does not exist, or it has been deleted."
					errorMessage="An unexpected error occurred."
					onRetry={() => {}}
					action={{ label: "Organizations", to: "/organizations" }}
				/>,
			);
			await expectNoA11yViolations();
		});
	}

	it("has no violations with a route-specific offline message", async () => {
		renderWithProviders(
			<DetailLoadFailure
				kind="offline"
				notFoundTitle="Opportunity not found"
				notFoundMessage="This opportunity does not exist, or it has been removed."
				errorMessage="An unexpected error occurred."
				offlineMessage="We will load this page again as soon as your connection is back."
				onRetry={() => {}}
				action={{ label: "Find opportunities", to: "/opportunities" }}
			/>,
		);
		await expectNoA11yViolations();
	});
});
