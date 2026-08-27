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

export function fetchVolunteerOpportunityDateAvailability(
	api: EinsatzbereitApi,
	options: FetchVolunteerOpportunityDateAvailabilityOptions,
	signal?: AbortSignal,
): Promise<VolunteerOpportunityAvailableDate[]> {
	return api.getVolunteerOpportunityDateAvailability(
		options.from,
		options.to,
		// The server derives the caller's zone from the X-Timezone header
		// (sent on every request, see api-instance.ts) rather than this
		// scalar offset - a single offset can't be right for every slot in
		// a multi-week window once a DST transition falls inside it (#2203).
		undefined,
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
