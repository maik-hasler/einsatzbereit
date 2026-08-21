import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import CalendarWidget from "./CalendarWidget";
import { renderWithProviders } from "../../../test/render";

/**
 * The calendar cases from `OrgDashboardWidgetsTests` and `AccessibilityTests`,
 * moved down in #2148 wave 13. Remaining inventory: #2159.
 *
 * All four are about what react-big-calendar is *configured* with - the
 * `culture` prop, the `messages` overrides, the `formats` overrides, and the
 * accessible name on a day cell - which shows up as rendered text and ARIA
 * attributes, not as layout. jsdom renders the calendar's DOM perfectly well;
 * what it cannot do is measure it, and none of these measure anything.
 *
 * The originals seeded a real organization, opportunity and time slot over
 * four sequential API calls, signed in through Keycloak and then drove the
 * header's language menu. `renderWithProviders` takes `lng` directly, and the
 * whole seed is one mocked `getOrganizationCalendarEvents` payload.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";

/**
 * One opportunity with one slot, dated relative to now so the widget's
 * "open on a range that actually has events" logic (#983/#2045) behaves the
 * same way on every run rather than only in the month this file was written.
 */
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

/**
 * The toolbar view buttons are plain buttons carrying the localized label -
 * but only once the first fetch resolves. Until then the widget renders a
 * skeleton with no toolbar at all, so every one of these has to wait for the
 * calendar itself before it can click anything.
 */
async function switchTo(label: string) {
	await waitFor(() =>
		expect(document.querySelector(".rbc-calendar")).not.toBeNull(),
	);
	await userEvent.click(screen.getByRole("button", { name: label }));
}

describe("CalendarWidget in German", () => {
	it("renders German weekday labels in month view", async () => {
		// The assertion is that the `culture` prop actually reaches
		// react-big-calendar - without it the localizer falls back to English
		// weekday names regardless of the app's language.
		renderCalendar("de");

		await switchTo("Monat");

		await waitFor(() =>
			expect(document.querySelector(".rbc-header")).not.toBeNull(),
		);
		const headers = Array.from(
			document.querySelectorAll(".rbc-month-view .rbc-header"),
		).map((h) => h.textContent?.trim() ?? "");
		// Substring rather than equality: date-fns renders these as "Mo", "Di"
		// or the longer forms depending on width, and the language is what is
		// under test, not the abbreviation length.
		expect(headers.join(" ")).toMatch(/\b(Mo|Montag)\b/);
		expect(headers.join(" ")).toMatch(/\b(Mi|Mittwoch)\b/);
		// And explicitly not the English fallback this regressed to.
		expect(headers.join(" ")).not.toMatch(/\b(Mon|Wed)\b/);
	});

	it("renders German column headers in agenda view", async () => {
		// A different mechanism from the weekday labels above: these three come
		// from the `messages` override object, not from the localizer's culture.
		renderCalendar("de");

		await switchTo("Agenda");

		// Scoped to the agenda view rather than to a `table` role: rbc splits
		// agenda into a header table and a separately scrolling body table, so
		// "the table" is ambiguous and the body half carries no headers at all.
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
		// #1959: react-big-calendar's own dayRangeHeaderFormat renders the range
		// through date-fns' locale-default 'P' token, which for en-GB is a bare
		// numeric DD/MM/YYYY - the one register this product deliberately does
		// not use. `calendarFormats` routes both range labels through the shared
		// `formatDate` instead.
		renderCalendar("en");

		await switchTo("Week");

		const label = await waitFor(() => {
			const el = document.querySelector(".rbc-toolbar-label");
			expect(el?.textContent?.trim()).toBeTruthy();
			return el as HTMLElement;
		});

		const text = label.textContent?.trim() ?? "";
		// Two dates joined by a hyphen, each carrying a named month - which is
		// exactly what the ambiguous all-numeric default does not produce.
		expect(text).toMatch(
			/\d{1,2}\s+\p{L}+\s+\d{4}\s*-\s*\d{1,2}\s+\p{L}+\s+\d{4}/u,
		);
		expect(text).not.toMatch(/\d{1,2}\/\d{1,2}\/\d{4}/);
	});
});

describe("CalendarWidget day cells", () => {
	it("gives each month-view day cell an accessible name beyond its bare digit", async () => {
		// The visible label is a number, which on its own tells a screen-reader
		// user nothing about which date they are on. `formatFullDate` supplies
		// the spelled-out name (see frontend/AGENTS.md's Date Formatting table -
		// it exists for exactly this, and is not a visible register).
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
