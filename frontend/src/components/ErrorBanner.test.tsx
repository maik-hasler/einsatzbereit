import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import ErrorBanner from "./ErrorBanner";
import SuccessBanner from "./SuccessBanner";
import { renderWithProviders } from "../test/render";

/**
 * Was two of `LiveRegionTests`' four cases (#972), moved down in #2148 wave 2.
 * Both reached a shared banner through a whole page - an unknown-opportunity
 * URL for the error one, a profile save for the success one - to assert the
 * component's own role/aria-live pair.
 */
describe("inline banners as live regions", () => {
	it("announces an error assertively", () => {
		renderWithProviders(
			<ErrorBanner message="This opportunity was not found." />,
		);

		const banner = screen.getByRole("alert");
		expect(banner).toHaveAttribute("aria-live", "assertive");
		expect(banner).toHaveTextContent("This opportunity was not found.");
	});

	it("stays mounted and empty until there is a success to announce", () => {
		// The half of #972 that is easy to get wrong: a role="status" node
		// inserted into the DOM already populated does not reliably announce,
		// so callers keep this mounted across the no-success/success toggle
		// rather than rendering it conditionally.
		const { rerender } = renderWithProviders(<SuccessBanner message={null} />);

		const banner = screen.getByRole("status");
		expect(banner).toHaveAttribute("aria-live", "polite");
		expect(banner).toHaveTextContent("");

		rerender(<SuccessBanner message="Profile saved." />);
		expect(screen.getByRole("status")).toHaveTextContent("Profile saved.");
	});
});
