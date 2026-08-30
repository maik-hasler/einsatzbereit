const INDIVIDUAL_CONTACT = "IndividualContact";

export const FEW_SPOTS_THRESHOLD = 5;

export interface CappedCapacity {
	kind: "capped";
	booked: number;
	max: number;

	spotsLeft: number;
	isFull: boolean;
}

export type OpportunityCapacity =
	| { kind: "unlimited"; booked: number }
	| { kind: "notApplicable"; booked: number; reason: "interest" | "noSlots" }
	| CappedCapacity;

export interface OpportunityCapacityInput {
	totalMaxParticipants: number | null | undefined;
	currentParticipantCount: number;

	participationType?: string | null;
}

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
 * At-a-glance capacity for a slotted opportunity, derived from the slots handed
 * in - callers pass only the slots a visitor can still book, because seats in an
 * ended slot can never be taken and must not be advertised (#2318).
 *
 * The per-slot `bookedCount` is the same for everyone, whereas
 * `currentParticipantCount` deliberately omits the caller's own engagements
 * (see `VolunteerOpportunityReadRepository.GetDetailsAsync`). Feeding that
 * viewer-relative number into an absolute "N spots left" label told a volunteer
 * who had already taken a seat that there were *more* seats free than everyone
 * else saw, so it is now only used to tally participants for opportunities that
 * have no time slots at all.
 */
export function getCapacityFromTimeSlots(
	timeSlots: readonly TimeSlotCapacityInput[],
	currentParticipantCount: number,
	participationType?: string | null,
): OpportunityCapacity {
	if (timeSlots.length === 0) {
		return getOpportunityCapacity({
			totalMaxParticipants: 0,
			currentParticipantCount,
			participationType,
		});
	}

	const totalMaxParticipants = timeSlots.some(
		(ts) => ts.maxParticipants == null,
	)
		? null
		: timeSlots.reduce((sum, ts) => sum + (ts.maxParticipants ?? 0), 0);

	return getOpportunityCapacity({
		totalMaxParticipants,
		currentParticipantCount: timeSlots.reduce(
			(sum, ts) => sum + ts.bookedCount,
			0,
		),
		participationType,
	});
}
