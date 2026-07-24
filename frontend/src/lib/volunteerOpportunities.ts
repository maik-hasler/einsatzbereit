import type {
	EinsatzbereitApi,
	PagedListOfVolunteerOpportunitySummary,
} from "../client/api-client";

export interface FetchVolunteerOpportunitiesOptions {
	pageNumber: number;
	pageSize: number;
	city?: string;
	occurrence?: string;
	participationType?: string;
	isRemote?: boolean;
	dateFrom?: Date;
	dateTo?: Date;
	north?: number;
	south?: number;
	east?: number;
	west?: number;
	centerLatitude?: number;
	centerLongitude?: number;
	radiusKm?: number;
	categories?: string[];
	tag?: string;
}

/**
 * Named-options wrapper around the NSwag-generated `EinsatzbereitApi.getVolunteerOpportunities`,
 * which takes 17 positional parameters - see einsatzbereit#870.
 */
export function fetchVolunteerOpportunities(
	api: EinsatzbereitApi,
	options: FetchVolunteerOpportunitiesOptions,
	signal?: AbortSignal,
): Promise<PagedListOfVolunteerOpportunitySummary> {
	return api.getVolunteerOpportunities(
		options.pageNumber,
		options.pageSize,
		options.city,
		options.occurrence,
		options.participationType,
		options.isRemote,
		options.dateFrom,
		options.dateTo,
		options.north,
		options.south,
		options.east,
		options.west,
		options.centerLatitude,
		options.centerLongitude,
		options.radiusKm,
		options.categories,
		options.tag,
		signal,
	);
}
