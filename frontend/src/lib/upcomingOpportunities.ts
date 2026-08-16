import type { VolunteerOpportunitySummary } from "../client/api-client";
import { pickLocalizedText } from "./format";

export const MAX_UPCOMING_ITEMS = 5;

export interface UpcomingItem {
	id: string;
	title: string;
	/**
	 * The next slot's start, as the ISO string it actually is on the wire.
	 *
	 * `VolunteerOpportunitySummary.nextTimeSlotStart` is *typed* `Date` by the
	 * generated client, but that client is generated in plain-DTO mode - it
	 * does `JSON.parse(text) as T` with no reviver and no class instances (see
	 * processGetOrganizationOpportunities in api-client.ts), so nothing ever
	 * turns that string into a Date. Calling a Date method on it throws, which
	 * takes the whole widget down into its error boundary. Every date value
	 * from this client is a string at runtime; keeping the string here, and
	 * carrying a separate parsed timestamp for ordering, is what stops that
	 * mismatch from reaching a render.
	 */
	nextStart: string;
	/** Epoch ms parsed from nextStart - sort key only. */
	nextStartMs: number;
	bookedCount: number;
	maxParticipants: number | null;
	/**
	 * Carried through so the widget's sign-up count goes through the same
	 * capacity contract as the organizer list and the public card, rather than
	 * re-deriving a two-state version of it (#1777).
	 */
	participationType: string;
}

/**
 * The soonest few opportunities that actually have an upcoming slot, soonest
 * first. Interest-based opportunities have no time slot at all, so they used
 * to fill the widget with rows carrying nothing but a title under a heading
 * promising dates - and pushed the ones that really are upcoming out of the
 * visible list.
 *
 * `unnamedTitle` is the caller's translated fallback for an untitled draft -
 * passed in rather than translated here so this stays a pure function.
 * `lang` is i18n.language ("de"/"en"), used to pick the matching title
 * variant (einsatzbereit#1946) - same reasoning as `unnamedTitle`.
 */
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
			// A value the runtime can't read as a date sorts nowhere sensible
			// and renders as "Invalid Date" - drop the row instead of showing
			// one that says nothing.
			if (Number.isNaN(nextStartMs)) return [];
			return [
				{
					id: o.id,
					title: pickLocalizedText(o.titleDe, o.titleEn, lang) || unnamedTitle,
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
