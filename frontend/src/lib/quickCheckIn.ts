import type { VolunteerOpportunitySummary } from "../client/api-client";

// The Quick Check-in widget's scanner only understands QR check-in - #1017:
// it used to offer every published opportunity regardless of checkInMethod,
// so scanning for a PIN/manual/none opportunity produced no result or an
// error. getOrganizationOpportunities has no server-side checkInMethod
// filter (and is shared with UpcomingOpportunitiesWidget via
// useSharedOrgFetch, which needs the unfiltered list), so this filters
// client-side instead.
export function filterQrCheckInOpportunities(
	opportunities: VolunteerOpportunitySummary[],
): VolunteerOpportunitySummary[] {
	return opportunities.filter((o) => o.checkInMethod === "QRCode");
}
