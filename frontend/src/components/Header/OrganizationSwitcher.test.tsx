import { describe, it, expect } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useLocation } from "react-router";
import OrganizationSwitcher from "./OrganizationSwitcher";
import { renderWithProviders } from "../../test/render";

/**
 * `NavigationTests`' org-switch case, moved down in #2148 wave 13. Remaining
 * inventory: #2159.
 *
 * Switching organizations keeps you on the tab you were already reading -
 * an organizer comparing two orgs' members should not be dropped back on a
 * dashboard each time. That is `orgTabPath(org.id, currentTab)`, and both
 * arguments are props.
 *
 * The E2E carried a `Skip.When(rowCount < 2)`, because it could only switch if
 * the signed-in test user happened to belong to two organizations - so it
 * silently did nothing on most runs. A two-org prop list removes the skip.
 */
const ORG_A = "11111111-1111-1111-1111-111111111111";
const ORG_B = "22222222-2222-2222-2222-222222222222";

/** Reads back where a navigation landed, without a real browser URL bar. */
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
		// A no-op navigation would remount the tab and throw away whatever the
		// organizer had scrolled to or typed.
		expect(await openAndPick("members", "Freiwillige Feuerwehr Kiel")).toBe(
			`/app/${ORG_A}/dashboard/members`,
		);
	});
});
