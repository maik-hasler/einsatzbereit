import { describe, it, expect } from "vitest";
import { screen, within } from "@testing-library/react";
import Footer from "./Footer";
import { renderWithProviders } from "../test/render";

describe("Footer legal links", () => {
	it("points at the English slugs, not the retired German ones", () => {
		renderWithProviders(<Footer />);

		const footer = screen.getByRole("contentinfo");
		const hrefs = within(footer)
			.getAllByRole("link")
			.map((a) => a.getAttribute("href"));

		expect(hrefs).toContain("/imprint");
		expect(hrefs).toContain("/privacy-policy");
		expect(hrefs).not.toContain("/impressum");
		expect(hrefs).not.toContain("/datenschutz");
	});

	it("no longer offers a direct link into organization creation", () => {
		renderWithProviders(<Footer />);

		const footer = screen.getByRole("contentinfo");
		expect(
			within(footer).queryByRole("link", { name: "Create an organization" }),
		).toBeNull();
	});
});
