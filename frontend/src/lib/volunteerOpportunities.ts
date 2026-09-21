import type {
	ApiClient,
	PagedListOfVolunteerOpportunitySummary,
	VolunteerOpportunityAvailableDate,
} from "../client";

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
	api: ApiClient,
	options: FetchVolunteerOpportunitiesOptions,
	signal?: AbortSignal,
): Promise<PagedListOfVolunteerOpportunitySummary> {
	return api.getVolunteerOpportunities({
		query: {
			PageNumber: options.pageNumber,
			PageSize: options.pageSize,
			Occurrence: options.occurrence,
			ParticipationType: options.participationType,
			IsRemote: options.isRemote,
			DateFrom: options.dateFrom,
			DateTo: options.dateTo,
			CenterLatitude: options.centerLatitude,
			CenterLongitude: options.centerLongitude,
			RadiusKm: options.radiusKm,
			Categories: options.categories,
			Tag: options.tag,
			Keyword: options.keyword,
		},
		signal,
	});
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
	api: ApiClient,
	options: FetchVolunteerOpportunityDateAvailabilityOptions,
	signal?: AbortSignal,
): Promise<VolunteerOpportunityAvailableDate[]> {
	return api.getVolunteerOpportunityDateAvailability({
		query: {
			From: options.from,
			To: options.to,
			// The server derives the caller's zone from the X-Timezone header
			// (sent on every request, see api-instance.ts) rather than this
			// scalar offset - a single offset can't be right for every slot in
			// a multi-week window once a DST transition falls inside it (#2203).
			UtcOffsetMinutes: undefined,
			Occurrence: options.occurrence,
			ParticipationType: options.participationType,
			IsRemote: options.isRemote,
			CenterLatitude: options.centerLatitude,
			CenterLongitude: options.centerLongitude,
			RadiusKm: options.radiusKm,
			Categories: options.categories,
			Tag: options.tag,
			Keyword: options.keyword,
		},
		signal,
	});
}
