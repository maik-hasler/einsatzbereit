import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import CalendarWidget from "./CalendarWidget";
import { renderWithProviders } from "../../../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";

function calendarEvents() {
	const start = new Date();
	start.setDate(start.getDate() + 2);
	start.setHours(9, 0, 0, 0);
	const end = new Date(start);
	end.setHours(11, 0, 0, 0);
	return [
		{
			opportunityId: "22222222-2222-2222-2222-222222222222",
			titleDe: "Deutscher Einsatz",
			titleEn: "English shift",
			color: undefined,
			timeSlots: [
				{
					timeSlotId: "33333333-3333-3333-3333-333333333333",
					startDateTime: start,
					endDateTime: end,
					bookedCount: 0,
					maxParticipants: 5,
				},
			],
		},
	];
}

beforeEach(() => {
	api.__reset();
	api.getOrganizationCalendarEvents.mockResolvedValue(calendarEvents());
});

function renderCalendar(lng: "de" | "en" = "en") {
	return renderWithProviders(
		<CalendarWidget
			organizationId={ORG_ID}
			refreshKey={0}
			size="full"
			isOrganizer
		/>,
		{ lng, auth: { isAuthenticated: true } },
	);
}

async function switchTo(label: string) {
	await waitFor(() =>
		expect(document.querySelector(".rbc-calendar")).not.toBeNull(),
	);
	await userEvent.click(screen.getByRole("button", { name: label }));
}

describe("CalendarWidget in German", () => {
	it("renders German weekday labels in month view", async () => {
		renderCalendar("de");

		await switchTo("Monat");

		await waitFor(() =>
			expect(document.querySelector(".rbc-header")).not.toBeNull(),
		);
		const headers = Array.from(
			document.querySelectorAll(".rbc-month-view .rbc-header"),
		).map((h) => h.textContent?.trim() ?? "");
		expect(headers.join(" ")).toMatch(/\b(Mo|Montag)\b/);
		expect(headers.join(" ")).toMatch(/\b(Mi|Mittwoch)\b/);
		expect(headers.join(" ")).not.toMatch(/\b(Mon|Wed)\b/);
	});

	it("renders German column headers in agenda view", async () => {
		renderCalendar("de");

		await switchTo("Agenda");

		const agenda = await waitFor(() => {
			const el = document.querySelector<HTMLElement>(".rbc-agenda-view");
			expect(el).not.toBeNull();
			return el as HTMLElement;
		});

		const headers = Array.from(agenda.querySelectorAll("th")).map(
			(th) => th.textContent?.trim() ?? "",
		);
		expect(headers).toContain("Datum");
		expect(headers).toContain("Uhrzeit");
		expect(headers).toContain("Termin");
	});
});

describe("CalendarWidget toolbar date range", () => {
	it("uses the shared date format rather than an ambiguous DD/MM/YYYY", async () => {
		renderCalendar("en");

		await switchTo("Week");

		const label = await waitFor(() => {
			const el = document.querySelector(".rbc-toolbar-label");
			expect(el?.textContent?.trim()).toBeTruthy();
			return el as HTMLElement;
		});

		const text = label.textContent?.trim() ?? "";
		// Day and month order follows the visitor's own English locale
		// (#2328), so the assertion is about the month being spelled out at
		// both ends of the range - not about which side of it the day sits.
		const spelledOutDate = String.raw`(\d{1,2}\s+\p{L}+|\p{L}+\s+\d{1,2},?)\s+\d{4}`;
		expect(text).toMatch(
			new RegExp(`${spelledOutDate}\\s*-\\s*${spelledOutDate}`, "u"),
		);
		expect(text).not.toMatch(/\d{1,2}\/\d{1,2}\/\d{4}/);
	});
});

describe("CalendarWidget day cells", () => {
	it("gives each month-view day cell an accessible name beyond its bare digit", async () => {
		renderCalendar("en");

		await switchTo("Month");

		const today = await waitFor(() => {
			const el = document.querySelector<HTMLElement>(
				".rbc-current .rbc-button-link",
			);
			expect(el).not.toBeNull();
			return el as HTMLElement;
		});

		const label = today.getAttribute("aria-label") ?? "";
		const visible = today.textContent?.trim() ?? "";
		expect(label).not.toBe("");
		expect(label).not.toBe(visible);
		expect(label.length).toBeGreaterThan(visible.length);
	});
});
