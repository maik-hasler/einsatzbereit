import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "../test/render";

const ENGAGEMENT_ID = "aaaaaaaa-0000-0000-0000-000000000001";

beforeEach(() => {
	vi.resetModules();
	window.__APP_CONFIG__ = { API_URL: "https://api.example.test" };
});

afterEach(() => {
	delete window.__APP_CONFIG__;
});

async function renderMenu() {
	const { default: AddToCalendarMenu } = await import("./AddToCalendarMenu");
	return renderWithProviders(
		<AddToCalendarMenu
			engagementId={ENGAGEMENT_ID}
			title="Erste Hilfe Kurs"
			start={new Date(Date.UTC(2027, 0, 14, 9, 0))}
			end={new Date(Date.UTC(2027, 0, 14, 12, 0))}
		/>,
	);
}

describe("AddToCalendarMenu links", () => {
	it("points Google Calendar at a prefilled event carrying the title", async () => {
		await renderMenu();
		await userEvent.click(
			screen.getByRole("button", { name: /Add to calendar/i }),
		);

		const href = screen
			.getByRole("link", { name: "Google Calendar" })
			.getAttribute("href");
		expect(href).toContain("calendar.google.com");
		expect(href).toContain(
			encodeURIComponent("Erste Hilfe Kurs").replaceAll("%20", "+"),
		);
	});

	it("points Apple Calendar at the backend's webcal feed for this engagement", async () => {
		await renderMenu();
		await userEvent.click(
			screen.getByRole("button", { name: /Add to calendar/i }),
		);

		const href = screen
			.getByRole("link", { name: "Apple Calendar" })
			.getAttribute("href");
		expect(href).toBe(
			`webcal://api.example.test/v1/engagements/${ENGAGEMENT_ID}/calendar`,
		);
	});

	it("downloads the .ics straight from the backend's calendar endpoint", async () => {
		await renderMenu();
		await userEvent.click(
			screen.getByRole("button", { name: /Add to calendar/i }),
		);

		const href = screen
			.getByRole("link", { name: "Download .ics" })
			.getAttribute("href");
		expect(href).toBe(
			`https://api.example.test/v1/engagements/${ENGAGEMENT_ID}/calendar`,
		);
	});
});
