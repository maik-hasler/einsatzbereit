import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import OrganizationSwitcher from "./OrganizationSwitcher";
import { renderWithProviders } from "../../test/render";
import { expectNoA11yViolations } from "../../test/a11y";

const ORG_A = "11111111-1111-1111-1111-111111111111";
const ORG_B = "22222222-2222-2222-2222-222222222222";

const ORGS = [
	{ id: ORG_A, name: "Freiwillige Feuerwehr Kiel", logoUrl: undefined },
	{ id: ORG_B, name: "Foerderverein Hamburg", logoUrl: undefined },
];

function renderSwitcher() {
	return renderWithProviders(
		<OrganizationSwitcher
			currentOrgId={ORG_A}
			currentTab="dashboard"
			orgs={ORGS}
			loading={false}
			error={null}
		/>,
		{ route: `/app/${ORG_A}/dashboard`, auth: { isAuthenticated: true } },
	);
}

describe("OrganizationSwitcher a11y", () => {
	it("has no violations while collapsed", async () => {
		renderSwitcher();
		await expectNoA11yViolations();
	});

	it("has no violations in the error state", async () => {
		renderWithProviders(
			<OrganizationSwitcher
				currentOrgId={ORG_A}
				currentTab="dashboard"
				orgs={[]}
				loading={false}
				error="Couldn't load your organizations."
			/>,
			{ auth: { isAuthenticated: true } },
		);
		await expectNoA11yViolations();
	});

	it("has no violations with the switcher open", async () => {
		renderSwitcher();
		await userEvent.click(screen.getByRole("button"));
		await expectNoA11yViolations();
	});

	it("names the trigger after the active organization and marks it as current in the list", async () => {
		renderSwitcher();
		const trigger = screen.getByRole("button", {
			name: "Switch organization, currently Freiwillige Feuerwehr Kiel",
		});
		expect(trigger).toHaveAttribute("aria-expanded", "false");

		await userEvent.click(trigger);
		expect(trigger).toHaveAttribute("aria-expanded", "true");
		expect(screen.queryByRole("menu")).toBeNull();
		expect(screen.queryByRole("listbox")).toBeNull();
		expect(screen.getByRole("list")).toHaveAccessibleName(
			"Switch organization",
		);

		expect(
			screen.getByRole("button", { name: "Freiwillige Feuerwehr Kiel" }),
		).toHaveAttribute("aria-current", "page");
		expect(
			screen.getByRole("button", { name: "Foerderverein Hamburg" }),
		).not.toHaveAttribute("aria-current");
	});
});
