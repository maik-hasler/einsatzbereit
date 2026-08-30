import { describe, it, expect } from "vitest";
import { buildIcsEvent, toIcsDataUrl } from "./ics";

const BASE = {
	uid: "opportunity-1-slot-2@einsatzbereit",
	title: "Erste Hilfe Kurs",
	start: new Date(Date.UTC(2027, 0, 14, 9, 0)),
	end: new Date(Date.UTC(2027, 0, 14, 12, 0)),
};

function contentLines(ics: string): string[] {
	// Unfold first: a continuation line carries a leading space and belongs to
	// the line before it.
	return ics.replace(/\r\n /g, "").split("\r\n").filter(Boolean);
}

describe("buildIcsEvent", () => {
	it("wraps one VEVENT carrying the uid, summary and both timestamps", () => {
		const lines = contentLines(buildIcsEvent(BASE));

		expect(lines[0]).toBe("BEGIN:VCALENDAR");
		expect(lines.at(-1)).toBe("END:VCALENDAR");
		expect(lines).toContain("UID:opportunity-1-slot-2@einsatzbereit");
		expect(lines).toContain("SUMMARY:Erste Hilfe Kurs");
		expect(lines).toContain("DTSTART:20270114T090000Z");
		expect(lines).toContain("DTEND:20270114T120000Z");
	});

	it("leaves out the optional properties it was given no value for", () => {
		const ics = buildIcsEvent(BASE);

		expect(ics).not.toContain("DESCRIPTION:");
		expect(ics).not.toContain("LOCATION:");
		expect(ics).not.toContain("URL:");
	});

	it("escapes the separators a description may legitimately contain", () => {
		const lines = contentLines(
			buildIcsEvent({
				...BASE,
				title: "Kurs",
				description: "Bring: Wasser, Schuhe; alles \\ mit\nund Zeit",
			}),
		);

		expect(lines).toContain(
			"DESCRIPTION:Bring: Wasser\\, Schuhe\\; alles \\\\ mit\\nund Zeit",
		);
	});

	it("folds a line past 75 octets and unfolds back to the original value", () => {
		const title = "Ü".repeat(80);
		const ics = buildIcsEvent({ ...BASE, title });

		expect(
			ics
				.split("\r\n")
				.every((line) => new TextEncoder().encode(line).length <= 75),
		).toBe(true);
		expect(contentLines(ics)).toContain(`SUMMARY:${title}`);
	});

	it("ends every line with CRLF, as the format requires", () => {
		const ics = buildIcsEvent(BASE);

		expect(ics.endsWith("\r\n")).toBe(true);
		expect(
			ics.split("\n").every((part) => part === "" || part.endsWith("\r")),
		).toBe(true);
	});
});

describe("toIcsDataUrl", () => {
	it("produces a calendar data URL that decodes back to the event", () => {
		const ics = buildIcsEvent(BASE);
		const url = toIcsDataUrl(ics);

		expect(url.startsWith("data:text/calendar;charset=utf-8,")).toBe(true);
		expect(
			decodeURIComponent(url.slice("data:text/calendar;charset=utf-8,".length)),
		).toBe(ics);
	});
});
