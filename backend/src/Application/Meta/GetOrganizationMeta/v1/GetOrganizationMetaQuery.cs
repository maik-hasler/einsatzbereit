using Application.Common.Messaging;

namespace Application.Meta.GetOrganizationMeta.v1;

public sealed record GetOrganizationMetaQuery(Guid OrganizationId, string BaseUrl)
	: IQuery<string?>;
