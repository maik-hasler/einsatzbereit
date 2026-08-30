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

function renderMenu(role: string) {
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
		{ auth: { isAuthenticated: true } },
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
