namespace Api.Engagements.GetMyEngagements.v1;

public sealed record GetMyEngagementsRequest(
	int PageNumber,
	int PageSize,
	bool Upcoming);
