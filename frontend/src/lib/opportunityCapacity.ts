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
