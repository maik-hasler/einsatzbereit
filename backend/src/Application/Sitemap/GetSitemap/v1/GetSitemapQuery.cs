using Application.Common.Messaging;

namespace Application.Sitemap.GetSitemap.v1;

public sealed record GetSitemapQuery(string BaseUrl) : IQuery<string>;
