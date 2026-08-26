import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { screen } from "@testing-library/react";

const SUPPORT_EMAIL = "legal@musterverein.example";

beforeEach(() => {
	vi.resetModules();
	delete window.__APP_CONFIG__;
});

afterEach(() => {
	delete window.__APP_CONFIG__;
});

async function renderContact() {
	const [{ default: ContactPage }, { renderWithProviders }] = await Promise.all(
		[import("./ContactPage"), import("../test/render")],
	);
	return renderWithProviders(<ContactPage />);
}

describe("ContactPage", () => {
	it("reaches support through a role address behind a mailto: link", async () => {
		window.__APP_CONFIG__ = { OPERATOR_EMAIL: SUPPORT_EMAIL };
		await renderContact();

		const link = screen.getByTestId("contact-email");
		expect(link).toHaveAttribute("href", `mailto:${SUPPORT_EMAIL}`);
		expect(screen.queryByText(/maikhslr/)).toBeNull();
	});

	it("omits the direct email affordance when the operator hasn't configured one", async () => {
		await renderContact();

		expect(screen.queryByTestId("contact-email")).toBeNull();
	});
});
