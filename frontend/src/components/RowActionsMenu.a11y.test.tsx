import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import RowActionsMenu from "./RowActionsMenu";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

/**
 * Replaces `OrgOpportunitiesPage_RowActionsMenuOpen_HasNoSeriousA11yViolations`.
 *
 * The disclosure-not-menu convention (frontend/AGENTS.md, "Accessibility")
 * is a claim about roles and names, which is exactly what axe evaluates -
 * there is nothing about it that needs a real browser.
 */
const actions = [
	{ key: "edit", label: "Edit", onClick: () => {} },
	{ key: "unpublish", label: "Unpublish", onClick: () => {}, disabled: true },
	{ key: "delete", label: "Delete", onClick: () => {}, destructive: true },
];

describe("RowActionsMenu a11y", () => {
	it("has no violations while closed", async () => {
		renderWithProviders(
			<RowActionsMenu
				actions={actions}
				label="More actions for Beach cleanup"
			/>,
		);
		await expectNoA11yViolations();
	});

	it("has no violations while open", async () => {
		renderWithProviders(
			<RowActionsMenu
				actions={actions}
				label="More actions for Beach cleanup"
			/>,
		);
		await userEvent.click(
			screen.getByRole("button", { name: "More actions for Beach cleanup" }),
		);
		await expectNoA11yViolations();
	});

	it("announces the open list as a disclosure, not a WAI-ARIA menu", async () => {
		// #1772: claiming role="menu" without the arrow-key model tells a
		// screen-reader user to press keys that do nothing.
		renderWithProviders(
			<RowActionsMenu
				actions={actions}
				label="More actions for Beach cleanup"
			/>,
		);
		const trigger = screen.getByRole("button", {
			name: "More actions for Beach cleanup",
		});
		expect(trigger).toHaveAttribute("aria-expanded", "false");
		expect(trigger).not.toHaveAttribute("aria-haspopup");

		await userEvent.click(trigger);
		expect(trigger).toHaveAttribute("aria-expanded", "true");
		expect(screen.queryByRole("menu")).toBeNull();
		expect(
			screen.getByRole("list", { name: "More actions for Beach cleanup" }),
		).toBeInTheDocument();
	});
});
