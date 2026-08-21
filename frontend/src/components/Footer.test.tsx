import { describe, it, expect } from "vitest";
import { screen, within } from "@testing-library/react";
import Footer from "./Footer";
import { renderWithProviders } from "../test/render";

/**
 * `HeaderBreadcrumbSharedImplementationTests`' legal-links case, moved down in
 * #2148 wave 13. Remaining inventory: #2159.
 *
 * The German slugs (/impressum, /datenschutz) were retired in favour of the
 * English ones; App.test.tsx asserts the old ones now 404. This is the other
 * half: the footer - the only place either page is linked from - has to point
 * at the surviving routes, or the pages become unreachable without typing a
 * URL. The E2E loaded a whole page to read two hrefs.
 */
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
});
