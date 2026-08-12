/**
 * One capacity contract for every surface that reports how full an
 * opportunity is (#1777).
 *
 * The backend projects capacity as a single tri-state number
 * (`VolunteerOpportunitySummary.TotalMaxParticipants`, see the comment in
 * `VolunteerOpportunityReadRepository.LoadParticipantStatsAsync`):
 *
 * - `null`  - at least one time slot is uncapped, so the total is unlimited
 * - `0`     - there are no time slots at all, so there is nothing to cap
 * - `> 0`   - the summed cap across the opportunity's time slots
 *
 * Every card previously re-derived that mapping inline and every one of them
 * handled only two of the three cases: `0` fell through both branches, so an
 * interest-based opportunity rendered *no* capacity line and *no* sign-up
 * count on the public grid, the organizer list and the dashboard widget alike.
 * That is one bug with three faces, which is why the mapping lives here now
 * and the surfaces only choose wording and colour.
 *
 * A time slot's cap is validated to be greater than zero when it is set
 * (`TimeSlot.MaxParticipants` in the domain), so `0` really does mean "no
 * slots" rather than "a slot that admits nobody".
 */

/** Sign-ups on this participation type are expressions of interest, not bookings for a dated shift. */
const INDIVIDUAL_CONTACT = "IndividualContact";

/**
 * At or below this many free places, a surface switches to its urgent
 * treatment - a warning-tone chip on a card, an orange "Only N spots left!" on
 * the detail page.
 *
 * One number for both, because they had drifted apart: the card warned at 3
 * and the detail page at 5, so a card could state "4 spots left" in a calm
 * neutral chip and link to a page shouting the same 4 in orange.
 */
export const FEW_SPOTS_THRESHOLD = 5;

export type OpportunityCapacity =
	/** At least one uncapped time slot - sign-ups are not limited. */
	| { kind: "unlimited"; booked: number }
	/**
	 * No time slots, so a place count does not apply. `reason` separates the
	 * two ways that happens, because they need different wording: an
	 * interest-based opportunity never has slots by design, while a slot-based
	 * one simply has none yet.
	 */
	| { kind: "notApplicable"; booked: number; reason: "interest" | "noSlots" }
	/** A real cap across the opportunity's time slots. */
	| {
			kind: "capped";
			booked: number;
			max: number;
			spotsLeft: number;
			isFull: boolean;
	  };

export interface OpportunityCapacityInput {
	/** The tri-state total documented above. */
	totalMaxParticipants: number | null | undefined;
	currentParticipantCount: number;
	/** Optional: only used to word the `notApplicable` state. */
	participationType?: string | null;
}

/**
 * Resolves the tri-state total into an explicit state. Shaped to take a
 * `VolunteerOpportunitySummary` directly, so a caller passes the item it
 * already has rather than picking three fields apart.
 */
export function getOpportunityCapacity({
	totalMaxParticipants,
	currentParticipantCount,
	participationType,
}: OpportunityCapacityInput): OpportunityCapacity {
	const booked = Math.max(0, currentParticipantCount);

	if (totalMaxParticipants == null) {
		return { kind: "unlimited", booked };
	}

	if (totalMaxParticipants <= 0) {
		return {
			kind: "notApplicable",
			booked,
			reason: participationType === INDIVIDUAL_CONTACT ? "interest" : "noSlots",
		};
	}

	return {
		kind: "capped",
		booked,
		max: totalMaxParticipants,
		spotsLeft: Math.max(0, totalMaxParticipants - booked),
		isFull: booked >= totalMaxParticipants,
	};
}

export interface TimeSlotCapacityInput {
	maxParticipants: number | null | undefined;
	bookedCount: number;
}

/**
 * The same contract for the detail page, which gets per-slot rows instead of
 * the summary's pre-aggregated total. Folding the slots down with the *same*
 * rule the SQL projection uses is what keeps the detail page from stating a
 * different capacity than the card that linked to it.
 *
 * Only the *cap* comes from the slots. `currentParticipantCount` is passed in
 * separately because that is how the backend counts it too - engagements on the
 * opportunity, not bookings summed per slot. The two differ for an
 * interest-based opportunity, whose sign-ups have no slot at all: summing slot
 * bookings there always yields 0 and would report an opportunity with sign-ups
 * as having none.
 */
export function getCapacityFromTimeSlots(
	timeSlots: readonly TimeSlotCapacityInput[],
	currentParticipantCount: number,
	participationType?: string | null,
): OpportunityCapacity {
	const totalMaxParticipants =
		timeSlots.length === 0
			? 0
			: timeSlots.some((ts) => ts.maxParticipants == null)
				? null
				: timeSlots.reduce((sum, ts) => sum + (ts.maxParticipants ?? 0), 0);

	return getOpportunityCapacity({
		totalMaxParticipants,
		currentParticipantCount,
		participationType,
	});
}

// Per-slot remaining places are `computeSpotsLeft`/`isSlotFull` in
// `lib/format.ts`, not something this module repeats: a single slot has no
// "no slots at all" state to report, and the sign-up modal's slot picker
// already speaks that helper.
