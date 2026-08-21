import { describe, it, expect } from "vitest";
import { screen, within } from "@testing-library/react";
import TermsOfUsePage from "./TermsOfUsePage";
import Footer from "../components/Footer";
import { renderWithProviders } from "../test/render";

describe("TermsOfUsePage", () => {
	it("shows the core clauses in English", () => {
		renderWithProviders(<TermsOfUsePage />);

		expect(
			screen.getByRole("heading", { name: "Terms of Use", level: 1 }),
		).toBeInTheDocument();
		expect(
			screen.getByRole("heading", { name: /Our role as a platform/ }),
		).toBeInTheDocument();
		expect(
			screen.getByRole("heading", { name: /Suspension and termination/ }),
		).toBeInTheDocument();
		expect(screen.getByText(/at your own risk/)).toBeInTheDocument();
	});

	it("shows the core clauses in German", () => {
		renderWithProviders(<TermsOfUsePage />, { lng: "de" });

		expect(
			screen.getByRole("heading", { name: "Nutzungsbedingungen", level: 1 }),
		).toBeInTheDocument();
		expect(
			screen.getByRole("heading", { name: /Unsere Rolle als Plattform/ }),
		).toBeInTheDocument();
		expect(screen.getByText(/auf eigenes Risiko/)).toBeInTheDocument();
	});

	it("carries neither an action bar nor an in-band Home link", () => {
		const { container } = renderWithProviders(<TermsOfUsePage />);

		expect(container.querySelector("nav[aria-label='Breadcrumb']")).toBeNull();
		expect(screen.queryByRole("link", { name: "Home" })).toBeNull();
	});

	it("cross-links to contact, privacy policy and imprint", () => {
		const { container } = renderWithProviders(<TermsOfUsePage />);

		for (const href of ["/contact", "/privacy-policy", "/imprint"]) {
			expect(container.querySelector(`a[href='${href}']`)).not.toBeNull();
		}
	});

	it("does not describe a verification badge that does not exist", () => {
		const { unmount } = renderWithProviders(<TermsOfUsePage />);
		expect(
			screen.getByRole("heading", {
				name: /Organizations and volunteer opportunities/,
			}),
		).toBeInTheDocument();
		expect(screen.queryByText(/verification badge/)).toBeNull();
		unmount();

		renderWithProviders(<TermsOfUsePage />, { lng: "de" });
		expect(
			screen.getByRole("heading", { name: /Organisationen und Einsätze/ }),
		).toBeInTheDocument();
		expect(screen.queryByText(/Verifizierungs-Badge/)).toBeNull();
	});

	it("is reachable from the footer's legal links", () => {
		renderWithProviders(<Footer />);

		const footer = screen.getByRole("contentinfo");
		const link = within(footer).getByRole("link", { name: "Terms of Use" });
		expect(link).toHaveAttribute("href", "/terms-of-use");
	});
});
