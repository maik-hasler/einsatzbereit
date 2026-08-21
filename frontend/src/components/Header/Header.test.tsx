import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import Header from "./Header";
import { renderWithProviders } from "../../test/render";

/**
 * Was `Header_Anonymous_ShowsSignInButton` from `AuthGuardTests` and the
 * desktop case of `AccountConsoleLinkTests` (#1675), moved down in #2148
 * wave 3.
 *
 * The rest of `AuthGuardTests` stays end-to-end on purpose: those are the
 * real login journeys (anonymous redirect to Keycloak, sign-in with valid
 * credentials, return-to-originating-page, and the registration endpoint),
 * which is exactly the coverage #2148 says to keep.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../hooks/useApiClient", () => ({ useApiClient: () => api }));

beforeEach(() => {
	api.__reset();
	api.getOrganizations.mockResolvedValue([]);
	api.getNotifications.mockResolvedValue({ items: [], pageCount: 1 });
});

describe("Header for an anonymous visitor", () => {
	it("offers a way to sign in", () => {
		renderWithProviders(<Header />);

		expect(
			screen.getAllByRole("button", { name: "Sign in" })[0],
		).toBeInTheDocument();
		expect(screen.queryByRole("button", { name: "User menu" })).toBeNull();
	});
});

/**
 * `HeaderPrimaryNavTests`, `HeaderOrganizationEntryTests`, the nav-bar half of
 * `AvatarAndLogoDisplayTests` and `NotificationTests`, moved down in #2148
 * wave 12. Remaining inventory: #2159.
 *
 * The header's whole job here is deciding which destinations exist for a given
 * viewer: `buildPrimaryNav(activeOrg)` swaps the "for organizations" pitch for
 * the member's own organization, and `navOrg = orgSwitcher ? null : activeOrg`
 * withholds it inside the org app, where the switcher beside it already names
 * the same organization (#1785). Both are pure functions of two props.
 *
 * The E2E originals signed a user in and loaded a page per viewer state. Where
 * a case asserted the *destination* page as well (its `<h1>`, its search
 * input), that half belongs to the destination page's own test - here the
 * link's `href` is what the header is responsible for.
 */
const ORG = {
	id: "77777777-7777-7777-7777-777777777777",
	name: "Freiwillige Feuerwehr Kiel",
};

const signedIn = { isAuthenticated: true, name: "Vera Volunteer" };

describe("Header primary navigation", () => {
	it("offers the opportunity list to an anonymous visitor", () => {
		const { container } = renderWithProviders(<Header />);

		const link = container.ownerDocument.querySelector(
			'[data-testid="nav-findOpportunities"]',
		);
		expect(link).toHaveAttribute("href", "/opportunities");
	});

	it("still carries the primary destinations once signed in", async () => {
		const { container } = renderWithProviders(<Header />, { auth: signedIn });

		await screen.findByRole("button", { name: "User menu" });
		for (const key of ["findOpportunities", "help"]) {
			expect(
				container.ownerDocument.querySelector(`[data-testid="nav-${key}"]`),
			).toBeInTheDocument();
		}
	});

	it("carries the way home in the nav", () => {
		// Every subpage used to restate "back to the home page" inside its own
		// title band - the one place a visitor does not look for site
		// navigation. It is a nav destination, so it lives in the nav.
		const { container } = renderWithProviders(<Header />, {
			route: "/opportunities",
		});

		expect(
			container.ownerDocument.querySelector('[data-testid="nav-home"]'),
		).toHaveAttribute("href", "/");
	});
});

describe("Header organization entry", () => {
	beforeEach(() => {
		api.getOrganizations.mockResolvedValue([ORG]);
	});

	it("offers a member their organization as a top-level destination", async () => {
		const { container } = renderWithProviders(<Header />, { auth: signedIn });

		const entry = await screen.findByTestId("nav-organization");
		expect(entry).toHaveAttribute("href", `/app/${ORG.id}/dashboard`);
		// It replaces the pitch rather than sitting beside it.
		expect(
			container.ownerDocument.querySelector(
				'[data-testid="nav-forOrganizations"]',
			),
		).toBeNull();
	});

	it("keeps the pitch for a signed-in non-member", async () => {
		api.getOrganizations.mockResolvedValue([]);

		const { container } = renderWithProviders(<Header />, { auth: signedIn });

		await screen.findByRole("button", { name: "User menu" });
		expect(
			container.ownerDocument.querySelector(
				'[data-testid="nav-forOrganizations"]',
			),
		).toBeInTheDocument();
		expect(screen.queryByTestId("nav-organization")).toBeNull();
	});

	it("withholds the organization inside the org app, where the switcher names it", async () => {
		// #1785: `navOrg = orgSwitcher ? null : activeOrg`. A second copy of the
		// name in the nav would only repeat the switcher beside it.
		const { container } = renderWithProviders(
			<Header
				orgSwitcher={{ currentOrgId: ORG.id, currentTab: "dashboard" }}
			/>,
			{ auth: signedIn },
		);

		expect(
			await screen.findByTestId("org-switcher-current-name"),
		).toHaveTextContent(/Feuerwehr/);
		expect(screen.queryByTestId("nav-organization")).toBeNull();
		expect(
			container.ownerDocument.querySelector(
				'[data-testid="nav-forOrganizations"]',
			),
		).toBeInTheDocument();
	});

	it("keeps the account menu to personal items only", async () => {
		// The organization is a nav destination now, so the account menu must
		// not carry a second route into it.
		const { container } = renderWithProviders(<Header />, { auth: signedIn });

		await userEvent.click(
			await screen.findByRole("button", { name: "User menu" }),
		);

		expect(
			screen.getByRole("link", { name: "My profile" }),
		).toBeInTheDocument();
		expect(screen.queryByRole("button", { name: "Organization" })).toBeNull();
		expect(
			container.ownerDocument.querySelectorAll("a[href*='/dashboard/']"),
		).toHaveLength(0);
	});
});

describe("Header notifications", () => {
	it("offers the bell once authenticated, and opens its panel", async () => {
		renderWithProviders(<Header />, { auth: signedIn });

		const bell = await screen.findByTestId("notification-bell");
		expect(screen.queryByTestId("notification-panel")).toBeNull();

		await userEvent.click(bell);

		expect(await screen.findByTestId("notification-panel")).toBeVisible();
	});
});

describe("Header avatar", () => {
	it("shows an uploaded avatar in place of the initials", async () => {
		// useAccountMenu fetches the avatar itself rather than reading it off the
		// token, so this is getUserProfile's answer, not an auth claim.
		api.getUserProfile.mockResolvedValue({
			firstName: "Vera",
			lastName: "Volunteer",
			avatarUrl: "https://storage.example.test/a.png",
		});

		renderWithProviders(<Header />, { auth: signedIn });

		const menu = await screen.findByRole("button", { name: "User menu" });
		// By tag, not by role: the avatar carries alt="" because the button it
		// sits in is already labelled "User menu", so it is presentational and
		// has no img role to query.
		await waitFor(() => expect(menu.querySelector("img")).not.toBeNull());
		expect(menu.querySelector("img")).toHaveAttribute(
			"src",
			"https://storage.example.test/a.png",
		);
	});

	it("falls back to initials when no avatar was uploaded", async () => {
		// The negative half, so the case above cannot pass against a header that
		// always renders an <img>.
		api.getUserProfile.mockResolvedValue({
			firstName: "Vera",
			lastName: "Volunteer",
			avatarUrl: undefined,
		});

		renderWithProviders(<Header />, { auth: signedIn });

		const menu = await screen.findByRole("button", { name: "User menu" });
		expect(menu).toHaveTextContent("VV");
		expect(menu.querySelector("img")).toBeNull();
	});
});
