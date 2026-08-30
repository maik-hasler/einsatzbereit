import { describe, it, expect } from "vitest";
import userEvent from "@testing-library/user-event";
import { useLocation } from "react-router";
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
});

function LocationProbe() {
	const location = useLocation();
	return (
		<span data-testid="location">{location.pathname + location.hash}</span>
	);
}

describe("Footer's landing-page fragment link", () => {
	it("navigates client-side from another route instead of reloading the document", async () => {
		renderWithProviders(
			<>
				<Footer />
				<LocationProbe />
			</>,
			{ route: "/help" },
		);

		const footer = screen.getByRole("contentinfo");
		await userEvent.click(
			within(footer).getByRole("link", { name: "Create an organization" }),
		);

		expect(screen.getByTestId("location")).toHaveTextContent(
			"/#for-organizations",
		);
	});
});
