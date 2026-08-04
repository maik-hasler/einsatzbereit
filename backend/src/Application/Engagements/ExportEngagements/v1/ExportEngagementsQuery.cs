using Application.Common.Messaging;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Engagements.ExportEngagements.v1;

public sealed record ExportEngagementsQuery(VolunteerOpportunityId OpportunityId, UserId RequestingUserId)
	: IQuery<EngagementExportFile>;
