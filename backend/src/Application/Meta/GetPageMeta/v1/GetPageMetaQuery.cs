using Application.Common.Messaging;

namespace Application.Meta.GetPageMeta.v1;

public sealed record GetPageMetaQuery(string Slug, string BaseUrl) : IQuery<string?>;
