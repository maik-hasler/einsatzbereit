import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import SubNavRail from "./SubNavRail";
import ProfileSubNav from "./ProfileSubNav";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

/**
 * The account and administration areas' shared left rail, reached by
 * `ProfileSettingsPage_*`, `ProfileOverviewPage_*`, `AdministrationPage_*`
 * and `MyEngagementsPage_*` alike.
 */
describe("SubNavRail a11y", () => {
	const items = [
		{ key: "overview", href: "/profile", label: "Overview" },
		{ key: "settings", href: "/profile/settings", label: "Settings" },
		{ key: "signups", href: "/my-signups", label: "My activity" },
	];

	it("has no violations", async () => {
		renderWithProviders(
			<SubNavRail items={items} active="settings" ariaLabel="Account areas" />,
		);
		await expectNoA11yViolations();
	});

	it("marks the current entry with aria-current, not colour alone", async () => {
		renderWithProviders(
			<SubNavRail items={items} active="settings" ariaLabel="Account areas" />,
		);
		expect(screen.getByRole("link", { name: "Settings" })).toHaveAttribute(
			"aria-current",
			"page",
		);
		expect(screen.getByRole("link", { name: "Overview" })).not.toHaveAttribute(
			"aria-current",
		);
	});

	it("has no violations for the profile area's preset rail", async () => {
		renderWithProviders(<ProfileSubNav active="profile" />);
		await expectNoA11yViolations();
		expect(screen.getByRole("navigation")).toHaveAccessibleName();
	});
});
