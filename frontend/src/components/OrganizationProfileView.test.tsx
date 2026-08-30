import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import OrganizationProfileView from "./OrganizationProfileView";
import { renderWithProviders } from "../test/render";

function renderView(
	props: Partial<Parameters<typeof OrganizationProfileView>[0]> = {},
) {
	return renderWithProviders(
		<OrganizationProfileView name="Tafel Bremen" {...props}>
			<p>Current needs</p>
		</OrganizationProfileView>,
	);
}

describe("OrganizationProfileView", () => {
	// Gating the second column on "does this organization have contact details"
	// made the main column - and the heading and empty state inside it - jump
	// between 688px and 1024px from one profile to the next (#2331).
	it("reserves the sidebar column whether or not there are contact details", () => {
		const { container: withContact } = renderView({
			contactEmail: "kontakt@tafel.example",
		});
		const withContactGrid = withContact.querySelector(
			"[data-content-wrapper] > div",
		);

		const { container: without } = renderView();
		const withoutGrid = without.querySelector("[data-content-wrapper] > div");

		expect(withContactGrid?.className).toContain(
			"lg:grid-cols-[minmax(0,1fr)_18rem]",
		);
		expect(withoutGrid?.className).toBe(withContactGrid?.className);
	});

	it("renders the contact card only when there is something to put in it", () => {
		renderView({ contactEmail: "kontakt@tafel.example" });

		expect(
			screen.getByRole("link", { name: "kontakt@tafel.example" }),
		).toBeInTheDocument();
	});

	it("leaves the reserved column empty when there are no contact details", () => {
		const { container } = renderView();

		expect(container.querySelector("aside")).toBeNull();
	});

	// Stacked after the main column, the contact card landed under the whole
	// opportunity list and the report link, at the bottom of a ~3,400px mobile
	// page - below the report-abuse link (#2331).
	it("puts contact details above the content on a single-column viewport", () => {
		const { container } = renderView({ contactPhone: "+49 421 1234" });

		const aside = container.querySelector("aside");
		const main = container.querySelector("[data-content-wrapper] > div > div");
		expect(aside).not.toBeNull();
		expect(
			(aside as Node).compareDocumentPosition(main as Node) &
				Node.DOCUMENT_POSITION_FOLLOWING,
		).toBeTruthy();
		// Placed back into the right-hand column from lg up, so the DOM order
		// above is not undone by a CSS-only reorder.
		expect(aside?.className).toContain("lg:col-start-2");
		expect(main?.className).toContain("lg:col-start-1");
	});

	it("keeps the description ahead of the contact card", () => {
		const { container } = renderView({
			description: "Wir verteilen Lebensmittel.",
			contactPhone: "+49 421 1234",
		});

		const description = screen.getByText("Wir verteilen Lebensmittel.");
		const aside = container.querySelector("aside");
		expect(aside).not.toBeNull();
		expect(
			description.compareDocumentPosition(aside as Node) &
				Node.DOCUMENT_POSITION_FOLLOWING,
		).toBeTruthy();
		expect(description.className).toContain("mb-6");
	});

	it("keeps the stacked layout inline instead of in a sidebar", () => {
		const { container } = renderView({
			layout: "stacked",
			contactEmail: "kontakt@tafel.example",
		});

		expect(container.querySelector("aside")).toBeNull();
		expect(
			screen.getByRole("link", { name: "kontakt@tafel.example" }),
		).toBeInTheDocument();
	});
});
