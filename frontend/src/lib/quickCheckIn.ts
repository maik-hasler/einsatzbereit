import type { VolunteerOpportunitySummary } from "../client/api-client";

// Every published opportunity an organizer can actually check a volunteer in
// for. `None` is the only method with nothing to do at check-in time - those
// volunteers are marked checked in automatically once the event has ended.
//
// This used to keep `QRCode` alone, so an organization running PIN or manual
// check-in was shown an empty widget telling it to create an opportunity it
// already had (#2322 F6).
export function filterCheckInOpportunities(
	opportunities: VolunteerOpportunitySummary[],
): VolunteerOpportunitySummary[] {
	return opportunities.filter((o) => o.checkInMethod !== "None");
}

// QR is the one method the dashboard can complete on its own, with the
// scanner. PIN check-in needs the organizer to read the opportunity's PIN
// out, and manual check-in needs its per-volunteer buttons - both of which
// live on that opportunity's own sign-up list, so the widget sends the
// organizer there instead of pretending it can finish the job here.
export function isQrCheckIn(
	opportunity: VolunteerOpportunitySummary | undefined,
): boolean {
	return opportunity?.checkInMethod === "QRCode";
}

// Which opportunity the widget should have selected when an organizer opens the
// board. It used to be whichever one the API happened to return first, which on
// a Saturday morning with three published Einsaetze is a one-in-three chance of
// being the one actually running - and nothing on the tile hinted that it had
// picked, so a scan lands against the wrong opportunity and the volunteer in
// front of you is told their code is invalid.
//
// In order: the slot running right now, then the one starting soonest, then
// whatever ran most recently, then anything with no schedule at all. The last
// two still belong in the list - a PIN opportunity with no upcoming slot can
// still be checked into - they are just never the obvious default.
const RANK_RUNNING = 0;
const RANK_UPCOMING = 1;
const RANK_PAST = 2;
const RANK_UNSCHEDULED = 3;

function slotMs(value: Date | undefined): number {
	if (!value) return NaN;
	return new Date(value as unknown as string).getTime();
}

export function pickCheckInOpportunity(
	opportunities: VolunteerOpportunitySummary[],
	nowMs: number,
): VolunteerOpportunitySummary | undefined {
	let best: VolunteerOpportunitySummary | undefined;
	let bestRank = Infinity;
	let bestDistance = Infinity;

	for (const opportunity of opportunities) {
		const start = slotMs(opportunity.nextTimeSlotStart);
		const end = slotMs(opportunity.nextTimeSlotEnd);

		let rank = RANK_UNSCHEDULED;
		// How far from now, so ties inside a rank break towards the slot the
		// organizer is most likely standing in front of.
		let distance = 0;

		if (!Number.isNaN(start)) {
			const finish = Number.isNaN(end) ? start : end;
			if (start <= nowMs && nowMs <= finish) {
				rank = RANK_RUNNING;
				distance = nowMs - start;
			} else if (start > nowMs) {
				rank = RANK_UPCOMING;
				distance = start - nowMs;
			} else {
				rank = RANK_PAST;
				distance = nowMs - finish;
			}
		}

		if (rank < bestRank || (rank === bestRank && distance < bestDistance)) {
			best = opportunity;
			bestRank = rank;
			bestDistance = distance;
		}
	}

	return best;
}
