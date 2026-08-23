import type { VolunteerOpportunitySummary } from "../client/api-client";
import { pickLocalizedText } from "./format";

export const MAX_UPCOMING_ITEMS = 5;

export interface UpcomingItem {
	id: string;
	title: string;

	nextStart: string;

	nextStartMs: number;
	bookedCount: number;
	maxParticipants: number | null;

	participationType: string;
}

export function selectUpcomingOpportunities(
	opportunities: VolunteerOpportunitySummary[],
	unnamedTitle: string,
	lang: string,
): UpcomingItem[] {
	return opportunities
		.flatMap((o): UpcomingItem[] => {
			if (!o.nextTimeSlotStart) return [];
			const nextStart = o.nextTimeSlotStart as unknown as string;
			const nextStartMs = new Date(nextStart).getTime();

			if (Number.isNaN(nextStartMs)) return [];
			return [
				{
					id: o.id,
					title:
						pickLocalizedText(o.titleDe, o.titleEn, lang).text || unnamedTitle,
					nextStart,
					nextStartMs,
					bookedCount: o.currentParticipantCount,
					maxParticipants: o.totalMaxParticipants ?? null,
					participationType: o.participationType,
				},
			];
		})
		.sort((a, b) => a.nextStartMs - b.nextStartMs)
		.slice(0, MAX_UPCOMING_ITEMS);
}
