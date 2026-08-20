import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import PrivacyPolicyPage from "./PrivacyPolicyPage";
import { renderWithProviders } from "../test/render";

/**
 * Was `PrivacyPolicyDisclosureTests` (#1340) plus the privacy-policy case of
 * `ImprintLegalComplianceTests` (#1339), moved down in #2148 wave 2.
 *
 * The page's own axe scan stays in `AccessibilityTests.cs` - it is the
 * surviving smoke for the static-legal-page layout, where contrast and
 * landmark structure are actually evaluable.
 */
describe("PrivacyPolicyPage", () => {
	it("discloses the OpenStreetMap and Nominatim transfers", () => {
		// #1340: the policy claimed "no data is passed on to third parties"
		// while the app sends every visitor's IP to OSM's tile servers and the
		// typed city filter to the public Nominatim geocoder.
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

		// The contradiction itself: the data-sharing section must no longer
		// make an unqualified "not passed on to third parties" claim.
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
