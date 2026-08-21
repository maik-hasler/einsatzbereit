import { describe, it } from "vitest";
import RouteState from "./RouteState";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

describe("RouteState a11y", () => {
	const variants = ["notFound", "forbidden", "offline", "error"] as const;

	for (const variant of variants) {
		it(`has no violations in the ${variant} state`, async () => {
			renderWithProviders(
				<RouteState
					variant={variant}
					title="Organization not found"
					message="We could not find what you were looking for."
				/>,
			);
			await expectNoA11yViolations();
		});

		it(`has no violations in the ${variant} state with a retry and an escape link`, async () => {
			renderWithProviders(
				<RouteState
					variant={variant}
					title="Something went wrong"
					message="The dashboard could not be loaded."
					onRetry={() => {}}
					action={{ label: "Back to the home page", to: "/" }}
				/>,
			);
			await expectNoA11yViolations();
		});
	}

	it("has no violations in the inline variant, which omits the page heading", async () => {
		renderWithProviders(
			<RouteState
				inline
				variant="offline"
				title="You are offline"
				message="Opportunities will load again once you reconnect."
				onRetry={() => {}}
			/>,
		);
		await expectNoA11yViolations();
	});
});
