import { describe, it, expect } from "vitest";
import { createRef } from "react";
import { screen, within } from "@testing-library/react";
import MobileMenu from "./MobileMenu";
import { renderWithProviders } from "../../test/render";

const ORG_ID = "11111111-1111-1111-1111-111111111111";

const base = {
	isTransparent: false,
	authStatus: "signedIn" as const,
	avatarUrl: null,
	initials: "VV",
	displayName: "Vera Volunteer",
	isAdmin: false,
	triggerRef: createRef<HTMLButtonElement>(),
	onClose: () => {},
	onSignIn: () => {},
	onRegister: () => {},
	onSignOut: () => {},
};

function renderMenu(role: string, route = "/") {
	return renderWithProviders(
		<MobileMenu
			{...base}
			activeOrg={{
				id: ORG_ID,
				name: "Lindenauer Nachbarschaftshilfe e.V.",
				logoUrl: undefined,
				role,
			}}
		/>,
		{ auth: { isAuthenticated: true }, route },
	);
}

const sections = () => within(screen.getByTestId("mobile-nav-org-sections"));

describe("MobileMenu organization sections", () => {
	it("links an organizer to every section of their organization", () => {
		renderMenu("Organizer");

		expect(sections().getByRole("link", { name: "Sign-ups" })).toHaveAttribute(
			"href",
			`/app/${ORG_ID}/dashboard/engagements`,
		);
	});

	// This list is the only org navigation left outside the org app, where no
	// organization details response says what the viewer's role is - so the
	// role travels with each organization in the switcher's own list instead
	// (#2316). Without it a member is offered a guaranteed 403 from here.
	it("hides the sign-ups section from a plain member", () => {
		renderMenu("Member");

		expect(sections().queryByRole("link", { name: "Sign-ups" })).toBeNull();
		expect(
			sections().getByRole("link", { name: "Opportunities" }),
		).toBeInTheDocument();
		expect(
			sections().getByRole("link", { name: "Members" }),
		).toBeInTheDocument();
		expect(
			sections().getByRole("link", { name: "Settings" }),
		).toBeInTheDocument();
	});
});

// The drawer renders the same primary items as the desktop nav, and marked
// none of them: below the `lg:` breakpoint the shell offered no "you are
// here" affordance at all, visually or to assistive tech (#2329 F5).
describe("MobileMenu current-page marking", () => {
	it("marks the route the visitor is on, and only that one", () => {
		renderMenu("Organizer", "/opportunities");

		expect(screen.getByTestId("mobile-nav-findOpportunities")).toHaveAttribute(
			"aria-current",
			"page",
		);
		expect(screen.getByTestId("mobile-nav-home")).not.toHaveAttribute(
			"aria-current",
		);
		expect(screen.getByTestId("mobile-nav-organizations")).not.toHaveAttribute(
			"aria-current",
		);
	});

	// Without `end`, "/" prefix-matches every route and the drawer would mark
	// Home on all of them.
	it("does not mark Home from a deeper route", () => {
		renderMenu("Organizer", "/organizations");

		expect(screen.getByTestId("mobile-nav-home")).not.toHaveAttribute(
			"aria-current",
		);
		expect(screen.getByTestId("mobile-nav-organizations")).toHaveAttribute(
			"aria-current",
			"page",
		);
	});

	it("marks the org section the visitor is in, not just the org itself", () => {
		renderMenu("Organizer", `/app/${ORG_ID}/dashboard/engagements`);

		expect(sections().getByRole("link", { name: "Sign-ups" })).toHaveAttribute(
			"aria-current",
			"page",
		);
		expect(screen.getByTestId("mobile-nav-organization")).not.toHaveAttribute(
			"aria-current",
		);
	});
});
