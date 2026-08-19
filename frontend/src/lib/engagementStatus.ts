export const ENGAGEMENT_STATUS_COLORS: Record<string, string> = {
	Pending: "bg-yellow-50 text-yellow-700 border-yellow-100",
	Confirmed: "bg-green-50 text-green-700 border-green-100",
	Cancelled: "bg-red-50 text-red-700 border-red-100",
	Withdrawn: "bg-gray-100 text-gray-700 border-gray-200",
};

// Cancelled/Withdrawn are terminal (#2070) - the opportunity's own
// "express interest by" deadline is no longer actionable for an engagement
// that already ended this way, so surfaces showing that deadline alongside
// the engagement should gate on this rather than display it unconditionally.
export function isTerminalEngagementStatus(status: string): boolean {
	return status === "Cancelled" || status === "Withdrawn";
}
