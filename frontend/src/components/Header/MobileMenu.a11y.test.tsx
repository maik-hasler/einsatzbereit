import { describe, it, expect } from "vitest";
import { createRef } from "react";
import { screen } from "@testing-library/react";
import MobileMenu from "./MobileMenu";
import { renderWithProviders } from "../../test/render";
import { expectNoA11yViolations } from "../../test/a11y";

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

	it("no longer links out to Keycloak's own account console (#1675)", () => {
		renderWithProviders(<MobileMenu {...base} isLoggedIn />);

		expect(
			screen.getByRole("link", { name: "My profile" }),
		).toBeInTheDocument();
		expect(screen.queryByRole("link", { name: "Account Settings" })).toBeNull();
	});
});
