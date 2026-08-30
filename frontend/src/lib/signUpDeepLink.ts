/**
 * "Sign up again" on a withdrawn engagement used to drop the volunteer on the
 * bare opportunity page, leaving them to re-derive which slot had been theirs
 * (#2323). The slot travels in the URL instead, and the detail page reopens
 * the sign-up modal on it.
 */
export const SIGN_UP_PARAM = "signUp";

/** Stands in for the slot id on an expression of interest, which has none. */
export const SIGN_UP_INTEREST = "interest";

export function buildSignUpLink(
	opportunityId: string,
	timeSlotId?: string | null,
): string {
	return `/volunteer-opportunities/${opportunityId}?${SIGN_UP_PARAM}=${
		timeSlotId ?? SIGN_UP_INTEREST
	}`;
}
