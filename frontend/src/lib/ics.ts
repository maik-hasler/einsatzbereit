/**
 * Minimal RFC 5545 event builder for the calendar entries the app can offer
 * before a sign-up exists. A signed-in volunteer gets the server-rendered
 * feed at /v1/engagements/{id}/calendar instead - that one carries the
 * engagement and stays live - but the public opportunity page has no
 * engagement to point at, so it builds the event locally (#2330).
 */

interface IcsEvent {
	uid: string;
	title: string;
	description?: string | undefined;
	location?: string | undefined;
	url?: string | undefined;
	start: Date;
	end: Date;
}

/**
 * Commas, semicolons and backslashes separate values inside a property, and a
 * literal newline ends the line, so all four have to travel escaped.
 */
function escapeText(value: string): string {
	return value
		.replace(/\\/g, "\\\\")
		.replace(/;/g, "\\;")
		.replace(/,/g, "\\,")
		.replace(/\r?\n/g, "\\n");
}

function toIcsDateTime(date: Date): string {
	return date
		.toISOString()
		.replace(/[-:]/g, "")
		.replace(/\.\d{3}Z$/, "");
}

/**
 * Content lines are capped at 75 octets and continued with a leading space.
 * The cap counts UTF-8 octets rather than characters, so an umlaut costs two
 * - and a fold may never land inside one.
 */
function foldLine(line: string): string {
	const encoder = new TextEncoder();
	if (encoder.encode(line).length <= 75) return line;

	const folded: string[] = [];
	let current = "";
	let currentBytes = 0;
	// Split by code point, not by UTF-16 unit, so a surrogate pair stays whole.
	for (const char of line) {
		const charBytes = encoder.encode(char).length;
		// Continuation lines carry a leading space, which counts toward the 75.
		const limit = folded.length === 0 ? 75 : 74;
		if (currentBytes + charBytes > limit) {
			folded.push(current);
			current = "";
			currentBytes = 0;
		}
		current += char;
		currentBytes += charBytes;
	}
	folded.push(current);

	return folded.map((part, i) => (i === 0 ? part : ` ${part}`)).join("\r\n");
}

export function buildIcsEvent({
	uid,
	title,
	description,
	location,
	url,
	start,
	end,
}: IcsEvent): string {
	const lines = [
		"BEGIN:VCALENDAR",
		"VERSION:2.0",
		"PRODID:-//Einsatzbereit//Volunteer Opportunity//EN",
		"CALSCALE:GREGORIAN",
		"BEGIN:VEVENT",
		`UID:${uid}`,
		`DTSTAMP:${toIcsDateTime(new Date())}Z`,
		`DTSTART:${toIcsDateTime(start)}Z`,
		`DTEND:${toIcsDateTime(end)}Z`,
		`SUMMARY:${escapeText(title)}`,
		...(description ? [`DESCRIPTION:${escapeText(description)}`] : []),
		...(location ? [`LOCATION:${escapeText(location)}`] : []),
		...(url ? [`URL:${escapeText(url)}`] : []),
		"END:VEVENT",
		"END:VCALENDAR",
	];

	return `${lines.map(foldLine).join("\r\n")}\r\n`;
}

/**
 * A data: URL rather than a blob: one - nothing has to be revoked, so the
 * link can be rendered straight into the menu without an effect to clean up.
 */
export function toIcsDataUrl(ics: string): string {
	return `data:text/calendar;charset=utf-8,${encodeURIComponent(ics)}`;
}
