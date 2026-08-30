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
