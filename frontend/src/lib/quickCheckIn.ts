import type { VolunteerOpportunitySummary } from "../client/api-client";

export function filterQrCheckInOpportunities(
	opportunities: VolunteerOpportunitySummary[],
): VolunteerOpportunitySummary[] {
	return opportunities.filter((o) => o.checkInMethod === "QRCode");
}
