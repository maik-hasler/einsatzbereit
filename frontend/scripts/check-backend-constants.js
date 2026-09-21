#!/usr/bin/env node

// Two constants exist twice, once on each side of the API, and neither side is
// served to the other:
//
//   lib/engagementTiming.ts CHECK_IN_WINDOW_{BEFORE,AFTER}_HOURS
//     <- Domain/VolunteerOpportunities/TimeSlot.cs CheckInWindow{Before,After}
//   lib/timezone.ts CANONICAL_TIME_ZONE
//     <- Application/Common/Time/CanonicalTimeZone.cs Id
//
// Both frontend files already say in a comment which backend member they
// mirror. A comment is not a check: it stays true-looking after the value it
// describes changes. This reads both sides and compares them.
//
// The two halves fail differently, which is why both are here. The check-in
// window is advisory - Engagement.cs re-checks it server-side, so a drifted
// copy mislabels an affordance but cannot widen the real guard. The timezone
// has no such backstop: it drives every date the product renders and every
// slot an organizer authors, so a drift there silently shifts wall-clock times
// with nothing to catch it.

import { readFileSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(__dirname, "../..");

let ok = true;
function fail(message) {
	console.error(message);
	ok = false;
}

function read(relativePath) {
	return readFileSync(join(repoRoot, relativePath), "utf8");
}

/**
 * Reads one capture group out of `source`, failing loudly when the pattern no
 * longer matches. A check that silently finds nothing is worse than no check.
 */
function extract(source, file, pattern, what) {
	const match = pattern.exec(source);
	if (!match) {
		fail(
			`Could not read ${what} from ${file}. It was most likely renamed or reshaped - ` +
				"update the pattern in scripts/check-backend-constants.js to match, rather than " +
				"leaving this check silently passing on a file it can no longer parse.",
		);
		return null;
	}
	return match[1];
}

// --- Check-in window --------------------------------------------------------

const TIMING_TS = "frontend/src/lib/engagementTiming.ts";
const TIME_SLOT_CS = "backend/src/Domain/VolunteerOpportunities/TimeSlot.cs";

const timingTs = read(TIMING_TS);
const timeSlotCs = read(TIME_SLOT_CS);

for (const [edge, tsName, csName] of [
	["before", "CHECK_IN_WINDOW_BEFORE_HOURS", "CheckInWindowBefore"],
	["after", "CHECK_IN_WINDOW_AFTER_HOURS", "CheckInWindowAfter"],
]) {
	const fromTs = extract(
		timingTs,
		TIMING_TS,
		new RegExp(`${tsName}\\s*=\\s*(\\d+(?:\\.\\d+)?)`),
		tsName,
	);
	const fromCs = extract(
		timeSlotCs,
		TIME_SLOT_CS,
		new RegExp(`${csName}\\s*=\\s*TimeSpan\\.FromHours\\((\\d+(?:\\.\\d+)?)\\)`),
		csName,
	);

	if (fromTs !== null && fromCs !== null && Number(fromTs) !== Number(fromCs)) {
		fail(
			`The check-in window ${edge} a time slot disagrees across the API: ${TIMING_TS} says ` +
				`${fromTs}h, ${TIME_SLOT_CS} says ${fromCs}h. The backend value is the one that decides ` +
				"whether a check-in is accepted; the frontend copy only decides what the button says " +
				"it will do, so bring the frontend to the backend rather than the other way round.",
		);
	}
}

// --- Canonical time zone ----------------------------------------------------

const TIMEZONE_TS = "frontend/src/lib/timezone.ts";
const TIMEZONE_CS = "backend/src/Application/Common/Time/CanonicalTimeZone.cs";

const timezoneTsTimeZone = extract(
	read(TIMEZONE_TS),
	TIMEZONE_TS,
	/CANONICAL_TIME_ZONE\s*=\s*"([^"]+)"/,
	"CANONICAL_TIME_ZONE",
);
const timezoneCsTimeZone = extract(
	read(TIMEZONE_CS),
	TIMEZONE_CS,
	/Id\s*=\s*"([^"]+)"/,
	"CanonicalTimeZone.Id",
);

if (
	timezoneTsTimeZone !== null &&
	timezoneCsTimeZone !== null &&
	timezoneTsTimeZone !== timezoneCsTimeZone
) {
	fail(
		`The canonical time zone disagrees across the API: ${TIMEZONE_TS} says ` +
			`"${timezoneTsTimeZone}", ${TIMEZONE_CS} says "${timezoneCsTimeZone}". Every date the ` +
			"product renders and every time slot an organizer authors is resolved through the " +
			"frontend copy, and nothing on the server corrects it - a drift here shifts wall-clock " +
			"times with no error anywhere.",
	);
}

if (ok) {
	console.log(
		"The frontend's mirrored backend constants (check-in window, canonical time zone) still match.",
	);
} else {
	process.exit(1);
}
