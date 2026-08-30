import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { screen } from "@testing-library/react";

const OPERATOR = {
	OPERATOR_NAME: "Musterverein Rettungsdienst e.V.",
	OPERATOR_ADDRESS: "Musterstraße 1, 12345 Musterstadt, Germany",
	OPERATOR_EMAIL: "legal@musterverein.example",
	OPERATOR_SITE_URL: "https://musterverein.example",
};

beforeEach(() => {
	vi.resetModules();
	window.__APP_CONFIG__ = OPERATOR;
});

afterEach(() => {
	delete window.__APP_CONFIG__;
});

async function renderPrivacyPolicy(lng?: "de" | "en") {
	const [{ default: PrivacyPolicyPage }, { renderWithProviders }] =
		await Promise.all([
			import("./PrivacyPolicyPage"),
			import("../test/render"),
		]);
	return renderWithProviders(<PrivacyPolicyPage />, lng ? { lng } : undefined);
}

describe("PrivacyPolicyPage", () => {
	it("discloses the OpenStreetMap and Nominatim transfers", async () => {
		await renderPrivacyPolicy();

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

	it("discloses the same transfers in German", async () => {
		await renderPrivacyPolicy("de");

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

	it("names the configured operator's real address instead of an on-request placeholder", async () => {
		await renderPrivacyPolicy();

		expect(screen.getByText(/Musterstraße 1/)).toBeInTheDocument();
		expect(screen.getByText(/12345 Musterstadt, Germany/)).toBeInTheDocument();
		expect(screen.queryByText(/Available on request via email/)).toBeNull();
	});

	it("makes the controller's email and website reachable, not just readable", async () => {
		await renderPrivacyPolicy();

		expect(
			screen.getByRole("link", { name: OPERATOR.OPERATOR_EMAIL }),
		).toHaveAttribute("href", `mailto:${OPERATOR.OPERATOR_EMAIL}`);
		expect(
			screen.getByRole("link", { name: OPERATOR.OPERATOR_SITE_URL }),
		).toHaveAttribute("href", OPERATOR.OPERATOR_SITE_URL);
	});

	// The ids were authored for an earlier section ordering and never renamed,
	// so every deep link from the outline landed one or more sections short -
	// "your rights" and "cookies" included (#2331).
	it("gives every outline entry a fragment that resolves to its own section", async () => {
		const { container } = await renderPrivacyPolicy();

		const entries = Array.from(
			container.querySelectorAll<HTMLAnchorElement>("nav a[href^='#']"),
		);
		expect(entries).toHaveLength(9);

		for (const entry of entries) {
			const id = entry.getAttribute("href")?.slice(1) ?? "";
			// The first span is the aria-hidden ordinal; the second is the label.
			const label = entry.querySelectorAll("span")[1]?.textContent;
			expect(container.querySelector(`#${id} h2`)?.textContent).toContain(
				label,
			);
		}
	});

	it("anchors the sections a reader is most likely to share", async () => {
		const { container } = await renderPrivacyPolicy();

		expect(container.querySelector("#your-rights h2")?.textContent).toContain(
			"Your rights",
		);
		expect(container.querySelector("#cookies h2")?.textContent).toContain(
			"Cookies and browser storage",
		);
	});

	it("shows a not-configured notice instead of anyone's real details when the operator hasn't set a responsible party", async () => {
		delete window.__APP_CONFIG__;
		await renderPrivacyPolicy();

		expect(screen.getByRole("status")).toHaveTextContent(
			"This deployment's operator hasn't configured a responsible party for data protection yet.",
		);
		expect(screen.queryByText(/maikhslr/)).toBeNull();
		expect(screen.queryByText(/Ammerländer/)).toBeNull();
	});
});
