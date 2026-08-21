import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import LanguageSelector from "./LanguageSelector";
import { renderWithProviders } from "../../test/render";
import { expectNoA11yViolations } from "../../test/a11y";

describe("LanguageSelector a11y", () => {
	it("has no violations while collapsed, on both header backgrounds", async () => {
		renderWithProviders(
			<div>
				<LanguageSelector />
				<LanguageSelector transparent />
			</div>,
		);
		await expectNoA11yViolations();
	});

	it("has no violations with the language list open", async () => {
		renderWithProviders(<LanguageSelector />);
		await userEvent.click(screen.getAllByRole("button")[0]);
		await expectNoA11yViolations();
	});

	it("announces itself as a disclosure over a labelled list", async () => {
		renderWithProviders(<LanguageSelector />);
		const trigger = screen.getAllByRole("button")[0];
		expect(trigger).toHaveAttribute("aria-expanded", "false");

		await userEvent.click(trigger);
		expect(trigger).toHaveAttribute("aria-expanded", "true");
		expect(screen.queryByRole("menu")).toBeNull();
		expect(screen.queryByRole("listbox")).toBeNull();
		expect(screen.getByRole("list")).toHaveAccessibleName();
	});
});
