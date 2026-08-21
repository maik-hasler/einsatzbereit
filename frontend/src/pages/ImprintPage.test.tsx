import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import ImprintPage from "./ImprintPage";
import { renderWithProviders } from "../test/render";

const SUPPORT_EMAIL = "hallo@einsatzbereit.maik-hasler.de";

describe("ImprintPage", () => {
	it("publishes a real postal address under the current statutory references", () => {
		renderWithProviders(<ImprintPage />);

		expect(
			screen.getByRole("heading", { name: "Information according to § 5 DDG" }),
		).toBeInTheDocument();
		expect(
			screen.getByRole("heading", {
				name: "Responsible for content according to § 18 para. 2 MStV",
			}),
		).toBeInTheDocument();
		expect(screen.getAllByText(/Ammerländer Heerstraße 76/)).toHaveLength(2);
		expect(screen.getAllByText(/26129 Oldenburg, Germany/)).toHaveLength(2);

		expect(screen.queryByText(/Address available on request/)).toBeNull();
		expect(screen.queryByText(/§ 5 TMG/)).toBeNull();
		expect(screen.queryByText(/§ 55/)).toBeNull();
	});

	it("publishes the same address and references in German", () => {
		renderWithProviders(<ImprintPage />, { lng: "de" });

		expect(
			screen.getByRole("heading", { name: "Angaben gemäß § 5 DDG" }),
		).toBeInTheDocument();
		expect(
			screen.getByRole("heading", {
				name: "Verantwortlich für den Inhalt nach § 18 Abs. 2 MStV",
			}),
		).toBeInTheDocument();
		expect(screen.getAllByText(/Ammerländer Heerstraße 76/)).toHaveLength(2);
		expect(screen.getAllByText(/26129 Oldenburg/)).toHaveLength(2);

		expect(screen.queryByText(/Adresse auf Anfrage/)).toBeNull();
		expect(screen.queryByText(/§ 5 TMG/)).toBeNull();
		expect(screen.queryByText(/§ 55 Abs. 2 RStV/)).toBeNull();
	});

	it("reaches support through a role address behind mailto:, with no SLA promise", () => {
		renderWithProviders(<ImprintPage />);

		expect(screen.getByRole("link", { name: SUPPORT_EMAIL })).toHaveAttribute(
			"href",
			`mailto:${SUPPORT_EMAIL}`,
		);
		expect(screen.queryByText(/maikhslr/)).toBeNull();
		expect(screen.queryByText(/24 hours/)).toBeNull();
	});
});
