import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import ContactPage from "./ContactPage";
import { renderWithProviders } from "../test/render";

const SUPPORT_EMAIL = "hallo@einsatzbereit.maik-hasler.de";

describe("ContactPage", () => {
	it("reaches support through a role address behind a mailto: link", () => {
		renderWithProviders(<ContactPage />);

		const link = screen.getByTestId("contact-email");
		expect(link).toHaveAttribute("href", `mailto:${SUPPORT_EMAIL}`);
		expect(screen.queryByText(/maikhslr/)).toBeNull();
	});
});
