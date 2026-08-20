import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import DocumentOutline from "./DocumentOutline";
import DocumentSection from "./DocumentSection";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

/**
 * The structure behind `PrivacyPolicyPage_*`, `ImprintPage_*`,
 * `TermsOfUsePage_*`, `ContactPage_*` and `HelpPage_*` - five near-identical
 * page scans over five static documents built from the same two components.
 * One page smoke stays in Playwright to cover the layout as a whole; the
 * outline/section pairing is checked here.
 */
const entries = [
	{ id: "scope", label: "Scope" },
	{ id: "data-we-collect", label: "Data we collect" },
	{ id: "your-rights", label: "Your rights" },
];

describe("legal document structure a11y", () => {
	it("has no violations for an outline beside its sections", async () => {
		renderWithProviders(
			<div>
				<h1>Privacy policy</h1>
				<DocumentOutline entries={entries} label="On this page" />
				{entries.map((entry, index) => (
					<DocumentSection
						key={entry.id}
						id={entry.id}
						number={index + 1}
						title={entry.label}
					>
						<p>Section body copy.</p>
					</DocumentSection>
				))}
			</div>,
		);
		await expectNoA11yViolations();
	});

	it("names the outline landmark and points every entry at a real section", async () => {
		const { container } = renderWithProviders(
			<div>
				<DocumentOutline entries={entries} label="On this page" />
				{entries.map((entry) => (
					<DocumentSection key={entry.id} id={entry.id} title={entry.label}>
						<p>Section body copy.</p>
					</DocumentSection>
				))}
			</div>,
		);

		expect(
			screen.getByRole("navigation", { name: "On this page" }),
		).toBeInTheDocument();

		for (const entry of entries) {
			const link = screen.getByRole("link", { name: entry.label });
			expect(link).toHaveAttribute("href", `#${entry.id}`);
			expect(container.querySelector(`#${entry.id}`)).not.toBeNull();
		}
	});

	it("has no violations for a section rendered without a clause number", async () => {
		renderWithProviders(
			<DocumentSection id="contact" title="Contact">
				<p>Reach us at hello@example.test.</p>
			</DocumentSection>,
		);
		await expectNoA11yViolations();
	});
});
