namespace Application.Common.StaticPages;

/// <summary>
/// One of the SPA's data-less routes: a page whose content is baked into the
/// frontend bundle rather than read from the database.
/// </summary>
/// <param name="Slug">Stable identifier used in the crawler-metadata route.</param>
/// <param name="Path">The route's path, relative to the site root.</param>
/// <param name="Title">German page title, matching the rendered page.</param>
/// <param name="Description">German meta description, matching the rendered page.</param>
public sealed record StaticPage(string Slug, string Path, string Title, string Description);
