import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import PrivacyPolicyPage from "./PrivacyPolicyPage";
import { renderWithProviders } from "../test/render";

describe("PrivacyPolicyPage", () => {
	it("discloses the OpenStreetMap and Nominatim transfers", () => {
		renderWithProviders(<PrivacyPolicyPage />);

		expect(
			screen.getByRole("heading", {
				name: "Map display and location search (OpenStreetMap, Nominatim)",
			}),
		).toBeInTheDocument();
		expect(
			screen.getByText(
				/legitimate interest \(Art\. 6\(1\)\(f\) GDPR\) in providing functioning map and location-search features/,
			),
		).toBeInTheDocument();

		const osm = screen.getByRole("link", {
			name: "OpenStreetMap Foundation Privacy Policy",
		});
		expect(osm).toHaveAttribute(
			"href",
			"https://wiki.osmfoundation.org/wiki/Privacy_Policy",
		);
		expect(osm).toHaveAttribute("target", "_blank");
		expect(osm).toHaveAttribute("rel", "noopener noreferrer");

		expect(
			screen.getByRole("link", { name: "Nominatim Usage Policy" }),
		).toHaveAttribute(
			"href",
			"https://operations.osmfoundation.org/policies/nominatim/",
		);

		expect(
			screen.queryByText(
				/Your personal data will not be passed on to third parties unless/,
			),
		).toBeNull();
	});

	it("discloses the same transfers in German", () => {
		renderWithProviders(<PrivacyPolicyPage />, { lng: "de" });

		expect(
			screen.getByRole("heading", {
				name: "Kartendarstellung und Ortssuche (OpenStreetMap, Nominatim)",
			}),
		).toBeInTheDocument();
		expect(
			screen.getByText(
				/berechtigtes Interesse \(Art\. 6 Abs\. 1 lit\. f DSGVO\) an einer funktionsfähigen Karten- und Ortssuche/,
			),
		).toBeInTheDocument();
		expect(
			screen.getByRole("link", {
				name: "Datenschutzerklärung der OpenStreetMap Foundation",
			}),
		).toBeInTheDocument();
		expect(
			screen.getByRole("link", { name: "Nutzungsrichtlinie von Nominatim" }),
		).toBeInTheDocument();
		expect(
			screen.queryByText(
				/Eine Übermittlung Ihrer persönlichen Daten an Dritte findet nicht statt,/,
			),
		).toBeNull();
	});

	it("names the responsible party's real address instead of an on-request placeholder", () => {
		renderWithProviders(<PrivacyPolicyPage />);

		expect(screen.getByText(/Ammerländer Heerstraße 76/)).toBeInTheDocument();
		expect(screen.getByText(/26129 Oldenburg, Germany/)).toBeInTheDocument();
		expect(screen.queryByText(/Available on request via email/)).toBeNull();
	});
});
