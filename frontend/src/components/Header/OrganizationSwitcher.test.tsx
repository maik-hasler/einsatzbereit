import { describe, it, expect } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useLocation } from "react-router";
import OrganizationSwitcher from "./OrganizationSwitcher";
import { renderWithProviders } from "../../test/render";

const ORG_A = "11111111-1111-1111-1111-111111111111";
const ORG_B = "22222222-2222-2222-2222-222222222222";

function LocationProbe() {
	const location = useLocation();
	return <span data-testid="location-probe">{location.pathname}</span>;
}

function renderSwitcher(currentTab: string) {
	return renderWithProviders(
		<>
			<OrganizationSwitcher
				currentOrgId={ORG_A}
				currentTab={currentTab}
				orgs={[
					{ id: ORG_A, name: "Freiwillige Feuerwehr Kiel", logoUrl: undefined },
					{ id: ORG_B, name: "Foerderverein Hamburg", logoUrl: undefined },
				]}
				loading={false}
				error={null}
			/>
			<LocationProbe />
		</>,
		{
			route: `/app/${ORG_A}/dashboard/${currentTab}`,
			auth: { isAuthenticated: true },
		},
	);
}

async function openAndPick(currentTab: string, name: string) {
	renderSwitcher(currentTab);
	await userEvent.click(
		screen.getByRole("button", { name: "Switch organization" }),
	);
	const list = screen.getByRole("list");
	await userEvent.click(within(list).getByRole("button", { name }));
	return screen.getByTestId("location-probe").textContent;
}

describe("OrganizationSwitcher", () => {
	it.each(["members", "opportunities", "settings"])(
		"lands on the same %s tab in the organization that was picked",
		async (tab) => {
			expect(await openAndPick(tab, "Foerderverein Hamburg")).toBe(
				`/app/${ORG_B}/dashboard/${tab}`,
			);
		},
	);

	it("stays put when the current organization is picked again", async () => {
		expect(await openAndPick("members", "Freiwillige Feuerwehr Kiel")).toBe(
			`/app/${ORG_A}/dashboard/members`,
		);
	});
});
