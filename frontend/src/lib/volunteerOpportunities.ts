import type {
	EinsatzbereitApi,
	PagedListOfVolunteerOpportunitySummary,
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
