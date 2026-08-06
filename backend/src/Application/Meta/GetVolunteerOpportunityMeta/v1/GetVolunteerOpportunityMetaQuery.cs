using Application.Common.Messaging;

namespace Application.Meta.GetVolunteerOpportunityMeta.v1;

public sealed record GetVolunteerOpportunityMetaQuery(Guid OpportunityId, string BaseUrl)
	: IQuery<string?>;
