using Application.Common.Messaging;
using Domain.Users;

namespace Application.Engagements.GetMyEngagementRecord.v1;

public sealed record GetMyEngagementRecordQuery(UserId UserId)
	: IQuery<List<EngagementRecordEntry>>;
