import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { screen } from "@testing-library/react";

const OPERATOR = {
	OPERATOR_NAME: "Musterverein Rettungsdienst e.V.",
	OPERATOR_ADDRESS: "Musterstraße 1\n12345 Musterstadt, Germany",
	OPERATOR_EMAIL: "legal@musterverein.example",
	OPERATOR_SITE_URL: "https://musterverein.example",
};

beforeEach(() => {
	vi.resetModules();
	delete window.__APP_CONFIG__;
});

afterEach(() => {
	delete window.__APP_CONFIG__;
});

async function renderImprint(lng?: "de" | "en") {
	const [{ default: ImprintPage }, { renderWithProviders }] = await Promise.all(
		[import("./ImprintPage"), import("../test/render")],
	);
	return renderWithProviders(<ImprintPage />, lng ? { lng } : undefined);
}

describe("ImprintPage", () => {
	it("publishes the configured operator's address under the current statutory references", async () => {
		window.__APP_CONFIG__ = OPERATOR;
		await renderImprint();

		expect(
			screen.getByRole("heading", { name: "Information according to § 5 DDG" }),
		).toBeInTheDocument();
		expect(
			screen.getByRole("heading", {
				name: "Responsible for content according to § 18 para. 2 MStV",
			}),
		).toBeInTheDocument();
		expect(screen.getAllByText(/Musterstraße 1/)).toHaveLength(2);
		expect(screen.getAllByText(/12345 Musterstadt, Germany/)).toHaveLength(2);

		expect(screen.queryByText(/Address available on request/)).toBeNull();
		expect(screen.queryByText(/§ 5 TMG/)).toBeNull();
		expect(screen.queryByText(/§ 55/)).toBeNull();
	});

	it("publishes the same address and references in German", async () => {
		window.__APP_CONFIG__ = OPERATOR;
		await renderImprint("de");

		expect(
			screen.getByRole("heading", { name: "Angaben gemäß § 5 DDG" }),
		).toBeInTheDocument();
		expect(
			screen.getByRole("heading", {
				name: "Verantwortlich für den Inhalt nach § 18 Abs. 2 MStV",
			}),
		).toBeInTheDocument();
		expect(screen.getAllByText(/Musterstraße 1/)).toHaveLength(2);
		expect(screen.getAllByText(/12345 Musterstadt, Germany/)).toHaveLength(2);

		expect(screen.queryByText(/Adresse auf Anfrage/)).toBeNull();
		expect(screen.queryByText(/§ 5 TMG/)).toBeNull();
		expect(screen.queryByText(/§ 55 Abs. 2 RStV/)).toBeNull();
	});

	it("reaches the configured operator through a mailto: link, with no SLA promise", async () => {
		window.__APP_CONFIG__ = OPERATOR;
		await renderImprint();

		expect(
			screen.getByRole("link", { name: OPERATOR.OPERATOR_EMAIL }),
		).toHaveAttribute("href", `mailto:${OPERATOR.OPERATOR_EMAIL}`);
		expect(screen.queryByText(/maikhslr/)).toBeNull();
		expect(screen.queryByText(/24 hours/)).toBeNull();
	});

	it("shows a not-configured notice instead of anyone's real details when the operator hasn't set up their legal identity", async () => {
		await renderImprint();

		expect(screen.getAllByRole("status").length).toBe(3);
		expect(
			screen.getAllByText(
				"This deployment's operator hasn't configured their legal contact details yet.",
			),
		).toHaveLength(3);
		expect(screen.queryByRole("link", { name: /@/ })).toBeNull();
		expect(screen.queryByText(/maikhslr/)).toBeNull();
		expect(screen.queryByText(/Ammerländer/)).toBeNull();
		expect(screen.queryByText(/Oldenburg/)).toBeNull();
	});
});
