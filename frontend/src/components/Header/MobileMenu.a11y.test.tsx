import { describe, it, expect } from "vitest";
import { createRef } from "react";
import { screen } from "@testing-library/react";
import MobileMenu from "./MobileMenu";
import { renderWithProviders } from "../../test/render";
import { expectNoA11yViolations } from "../../test/a11y";

/**
 * Replaces `MobileMenu_Open_AsOlaf_HasNoSeriousA11yViolations`, which needed a
 * login, an organization and a 390px viewport to reach a panel that is
 * entirely prop-driven.
 */
describe("MobileMenu a11y", () => {
	const base = {
		isTransparent: false,
		avatarUrl: null,
		initials: "OO",
		displayName: "Olaf Organizer",
		isAdmin: false,
		activeOrg: null,
		triggerRef: createRef<HTMLButtonElement>(),
		onClose: () => {},
		onSignIn: () => {},
		onRegister: () => {},
		onSignOut: () => {},
	};

	it("has no violations for a signed-out visitor", async () => {
		renderWithProviders(<MobileMenu {...base} isLoggedIn={false} />);
		await expectNoA11yViolations();
	});

	it("has no violations for a signed-in organizer with an organization", async () => {
		renderWithProviders(
			<MobileMenu
				{...base}
				isLoggedIn
				activeOrg={{
					id: "org-1",
					name: "Freiwillige Feuerwehr",
					logoUrl: undefined,
				}}
			/>,
		);

		// The panel is the widest state this component has - the org entry and
		// its four sub-links only exist here - so pin that the scan below is
		// actually looking at it.
		expect(screen.getByRole("dialog")).toHaveAccessibleName();
		expect(
			screen.getByRole("link", { name: /Freiwillige Feuerwehr/ }),
		).toBeInTheDocument();
		await expectNoA11yViolations();
	});

	it("has no violations for a platform admin", async () => {
		renderWithProviders(<MobileMenu {...base} isLoggedIn isAdmin />);
		await expectNoA11yViolations();
	});

	it("has no violations over the transparent landing-page header", async () => {
		renderWithProviders(<MobileMenu {...base} isLoggedIn isTransparent />);
		await expectNoA11yViolations();
	});
});
