import { describe, it, expect } from "vitest";
import { screen, within } from "@testing-library/react";
import TermsOfUsePage from "./TermsOfUsePage";
import Footer from "../components/Footer";
import { renderWithProviders } from "../test/render";

/**
 * Was `TermsOfUsePageTests` in the Playwright suite (#2148 wave 2). Every
 * assertion here is about rendered copy and links on a page that makes no
 * API call and depends on no browser layout, so it cost an Aspire boot and a
 * page load to learn something a render answers.
 *
 * The one case that did not move is
 * `KeycloakRegistrationForm_RequiresAcceptingTermsOfUse` - that drives the
 * real Keycloak registration form and stays end-to-end.
 */
describe("TermsOfUsePage", () => {
	it("shows the core clauses in English", () => {
		renderWithProviders(<TermsOfUsePage />);

		expect(
			screen.getByRole("heading", { name: "Terms of Use", level: 1 }),
		).toBeInTheDocument();
		// Substring matching throughout: since #1755 each clause heading
		// carries its own number ("2 Our role as a platform"), because legal
		// text is cited by clause. Matching the title alone keeps these
		// assertions from breaking when a section is inserted above one.
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
		// #1755 replaced this page's breadcrumb action bar with a Home link in
		// the title band; that link is gone in turn, since the header nav now
		// carries "Home" on every page. The cross-page guard for the header
		// side of that lives in HeaderBreadcrumbSharedImplementationTests.
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
		// #1665: section4Body claimed organizations get a badge confirming we
		// reviewed their identity. No such feature exists anywhere in the
		// product; pinned in both locales so it cannot come back before it does.
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
