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

function orgs(targetRole: string) {
	return [
		{
			id: ORG_A,
			name: "Freiwillige Feuerwehr Kiel",
			logoUrl: undefined,
			role: "Organizer",
		},
		{
			id: ORG_B,
			name: "Foerderverein Hamburg",
			logoUrl: undefined,
			role: targetRole,
		},
	];
}

function renderSwitcher(currentTab: string, targetRole: string) {
	return renderWithProviders(
		<>
			<OrganizationSwitcher
				currentOrgId={ORG_A}
				currentTab={currentTab}
				orgs={orgs(targetRole)}
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

async function openAndPick(
	currentTab: string,
	name: string,
	targetRole = "Organizer",
) {
	renderSwitcher(currentTab, targetRole);
	await userEvent.click(
		screen.getByRole("button", {
			name: "Switch organization, currently Freiwillige Feuerwehr Kiel",
		}),
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

	// The tab the switcher carries over has to exist for your role in the
	// organization you land in - a plain member has no sign-ups tab there
	// (#2316), so carrying it over would drop them straight onto a 403.
	it("carries the sign-ups tab over to an organization you organize", async () => {
		expect(await openAndPick("engagements", "Foerderverein Hamburg")).toBe(
			`/app/${ORG_B}/dashboard/engagements`,
		);
	});

	it("lands on the dashboard when you are only a member of the organization picked", async () => {
		expect(
			await openAndPick("engagements", "Foerderverein Hamburg", "Member"),
		).toBe(`/app/${ORG_B}/dashboard`);
	});

	it("stays put when the current organization is picked again", async () => {
		expect(await openAndPick("members", "Freiwillige Feuerwehr Kiel")).toBe(
			`/app/${ORG_A}/dashboard/members`,
		);
	});

	it("falls back to the plain trigger label when the organizations failed to load", () => {
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

		expect(
			screen.getByRole("button", { name: "Switch organization" }),
		).toBeInTheDocument();
	});

	it("names the trigger after the select placeholder when no organization is resolved yet", () => {
		renderWithProviders(
			<OrganizationSwitcher
				currentOrgId={ORG_A}
				currentTab="dashboard"
				orgs={[]}
				loading={false}
				error={null}
			/>,
			{ auth: { isAuthenticated: true } },
		);

		expect(
			screen.getByRole("button", { name: "Select organization" }),
		).toBeInTheDocument();
	});
});

// The name is split into a truncating head and an intact tail so a legal
// suffix survives ("... Nachbarschaftshilfe" + " e.V."). Both are flex items,
// and a leading space at the start of a flex item is collapsed away under
// normal white-space processing - which ran the two halves together into
// "Nachbarschaftshilfee.V." on screen while the DOM text still looked right
// (#2329 F2). jsdom has no layout, so the guard is the DOM text plus the
// white-space mode the browser will apply to it.
describe("OrganizationSwitcher name split", () => {
	it("keeps the separator between the head and the trailing token", () => {
		renderWithProviders(
			<OrganizationSwitcher
				currentOrgId={ORG_A}
				currentTab="dashboard"
				orgs={[
					{
						id: ORG_A,
						name: "Lindenauer Nachbarschaftshilfe e.V.",
						logoUrl: undefined,
						role: "Organizer",
					},
				]}
				loading={false}
				error={null}
			/>,
		);

		const head = screen.getByTestId("org-switcher-current-name-head");
		const tail = screen.getByTestId("org-switcher-current-name-tail");

		expect(head.textContent).toBe("Lindenauer Nachbarschaftshilfe");
		expect(tail.textContent).toBe(" e.V.");
		expect(`${head.textContent}${tail.textContent}`).toBe(
			"Lindenauer Nachbarschaftshilfe e.V.",
		);
		// `whitespace-nowrap` collapses that leading space; `whitespace-pre`
		// preserves it and still refuses to wrap.
		expect(tail.className).toContain("whitespace-pre");
		expect(tail.className).not.toContain("whitespace-nowrap");
	});
});
