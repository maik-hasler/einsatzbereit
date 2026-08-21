import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import ErrorBanner from "./ErrorBanner";
import SuccessBanner from "./SuccessBanner";
import { renderWithProviders } from "../test/render";

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
		const { rerender } = renderWithProviders(<SuccessBanner message={null} />);

		const banner = screen.getByRole("status");
		expect(banner).toHaveAttribute("aria-live", "polite");
		expect(banner).toHaveTextContent("");

		rerender(<SuccessBanner message="Profile saved." />);
		expect(screen.getByRole("status")).toHaveTextContent("Profile saved.");
	});
});
