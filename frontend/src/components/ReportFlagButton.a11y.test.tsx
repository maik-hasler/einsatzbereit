import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import ReportFlagButton from "./ReportFlagButton";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

const noop = async () => {};

describe("ReportFlagButton a11y", () => {
	it("has no violations in its resting state", async () => {
		renderWithProviders(
			<ReportFlagButton
				targetLabel="Nachbarschaftshilfe Leipzig"
				ariaLabel="Report organization"
				onReport={noop}
			/>,
		);
		await expectNoA11yViolations();
	});

	// The icon-only button carries its whole accessible name in aria-label, so a missing one
	// leaves a control announced as nothing at all.
	it("names the icon-only control for a screen reader", () => {
		renderWithProviders(
			<ReportFlagButton
				targetLabel="Nachbarschaftshilfe Leipzig"
				ariaLabel="Report organization"
				onReport={noop}
			/>,
		);

		expect(
			screen.getByRole("button", { name: "Report organization" }),
		).toBeInTheDocument();
	});

	it("has no violations with the modal resumed open after sign-in", async () => {
		renderWithProviders(
			<ReportFlagButton
				targetLabel="Gassi-Dienst für Tierheimhunde"
				targetLabelLang="de"
				ariaLabel="Report opportunity"
				onReport={noop}
				autoOpen
			/>,
		);

		await screen.findByRole("heading", { name: "Report content" });
		await expectNoA11yViolations();
	});

	it("routes an anonymous visitor to sign-in without opening the modal", async () => {
		renderWithProviders(
			<ReportFlagButton
				targetLabel="Nachbarschaftshilfe Leipzig"
				ariaLabel="Report organization"
				onReport={noop}
				onRequireSignIn={() => {}}
			/>,
		);

		screen.getByRole("button", { name: "Report organization" }).click();

		expect(
			screen.queryByRole("heading", { name: "Report content" }),
		).toBeNull();
		await expectNoA11yViolations();
	});
});
