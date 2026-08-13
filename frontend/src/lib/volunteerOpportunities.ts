import type {
	EinsatzbereitApi,
	PagedListOfVolunteerOpportunitySummary,
	VolunteerOpportunityAvailableDate,
} from "../client/api-client";

export interface FetchVolunteerOpportunitiesOptions {
	pageNumber: number;
	pageSize: number;
	occurrence?: string;
	participationType?: string;
	isRemote?: boolean;
	dateFrom?: Date;
	dateTo?: Date;
	centerLatitude?: number;
	centerLongitude?: number;
	radiusKm?: number;
	categories?: string[];
	tag?: string;
	keyword?: string;
}

/**
 * Named-options wrapper around the NSwag-generated `EinsatzbereitApi.getVolunteerOpportunities`,
 * which takes many positional parameters - see einsatzbereit#870.
 */
export function fetchVolunteerOpportunities(
	api: EinsatzbereitApi,
	options: FetchVolunteerOpportunitiesOptions,
	signal?: AbortSignal,
): Promise<PagedListOfVolunteerOpportunitySummary> {
	return api.getVolunteerOpportunities(
		options.pageNumber,
		options.pageSize,
		options.occurrence,
		options.participationType,
		options.isRemote,
		options.dateFrom,
		options.dateTo,
		options.centerLatitude,
		options.centerLongitude,
		options.radiusKm,
		options.categories,
		options.tag,
		options.keyword,
		signal,
	);
}

export interface FetchVolunteerOpportunityDateAvailabilityOptions {
	from: Date;
	to: Date;
	/**
	 * Minutes to add to UTC to get the caller's local time - the sign convention of
	 * `Intl`/ISO offsets ("+02:00" is 120), i.e. the negation of what
	 * `Date.prototype.getTimezoneOffset()` returns. Decides which calendar day a
	 * time slot is counted on.
	 */
	utcOffsetMinutes: number;
	occurrence?: string;
	participationType?: string;
	isRemote?: boolean;
	centerLatitude?: number;
	centerLongitude?: number;
	radiusKm?: number;
	categories?: string[];
	tag?: string;
	keyword?: string;
}

/**
 * Named-options wrapper around the NSwag-generated
 * `EinsatzbereitApi.getVolunteerOpportunityDateAvailability`, whose 12 positional
 * parameters are the same trap `fetchVolunteerOpportunities` above exists to avoid.
 */
export function fetchVolunteerOpportunityDateAvailability(
	api: EinsatzbereitApi,
	options: FetchVolunteerOpportunityDateAvailabilityOptions,
	signal?: AbortSignal,
): Promise<VolunteerOpportunityAvailableDate[]> {
	return api.getVolunteerOpportunityDateAvailability(
		options.from,
		options.to,
		options.utcOffsetMinutes,
		options.occurrence,
		options.participationType,
		options.isRemote,
		options.centerLatitude,
		options.centerLongitude,
		options.radiusKm,
		options.categories,
		options.tag,
		options.keyword,
		signal,
	);
}
