import { describe, it, expect } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import LanguageSelector from "./LanguageSelector";
import { renderWithProviders } from "../../test/render";

/**
 * `NavigationTests`' disclosure-semantics case, moved down in #2148 wave 13.
 * Remaining inventory: #2159.
 *
 * #1772: this used to advertise `aria-haspopup="listbox"` and wrap each button
 * in an `<li role="option">` - an axe `nested-interactive` violation on every
 * page (the header carries it everywhere), promising a keyboard model the
 * component never implemented. It is a disclosure: a trigger with
 * `aria-expanded` opening a labelled list of plain buttons, the active one
 * marked with `aria-current`.
 *
 * The existing `.a11y.test.tsx` beside this file scans the same component with
 * axe. What that cannot say is *which* attributes are present - a component
 * that dropped `aria-expanded` entirely is perfectly valid ARIA and still
 * broken. These are the named assertions.
 */
const renderSelector = (lng: "de" | "en" = "en") =>
	renderWithProviders(<LanguageSelector />, { lng });

describe("LanguageSelector disclosure semantics", () => {
	it("announces itself as a disclosure, never as a listbox or menu", async () => {
		renderSelector();

		const trigger = screen.getByTestId("language-selector-trigger");
		expect(trigger).toHaveAttribute("aria-expanded", "false");
		expect(trigger).not.toHaveAttribute("aria-haspopup");

		await userEvent.click(trigger);

		expect(trigger).toHaveAttribute("aria-expanded", "true");

		const menu = screen.getByTestId("language-selector-menu");
		expect(menu.tagName).toBe("UL");
		expect(menu).toHaveAccessibleName("Switch language");
		// Neither of the roles the old markup claimed.
		expect(menu).not.toHaveAttribute("role");
		expect(menu.querySelectorAll('[role="option"]')).toHaveLength(0);
		expect(menu.querySelectorAll("[aria-selected]")).toHaveLength(0);
	});

	it("marks the active language with aria-current, not aria-selected", async () => {
		renderSelector("de");

		await userEvent.click(screen.getByTestId("language-selector-trigger"));

		const menu = screen.getByTestId("language-selector-menu");
		const current = within(menu).getByRole("button", { name: "Deutsch" });
		expect(current).toHaveAttribute("aria-current", "true");
		expect(
			within(menu).getByRole("button", { name: "English" }),
		).not.toHaveAttribute("aria-current");
	});

	it("leads its accessible name with the code it visibly shows", () => {
		// #2072, a WCAG 2.5.3 Label-in-Name fix: the name used to replace the
		// visible "EN"/"DE" rather than extend it, so a speech-input user saying
		// "click DE" matched nothing.
		renderSelector("de");

		const trigger = screen.getByTestId("language-selector-trigger");
		expect(trigger).toHaveTextContent("DE");
		const name = trigger.getAttribute("aria-label") ?? "";
		expect(name.startsWith("DE")).toBe(true);
		expect(name).toContain("Deutsch");
	});
});
