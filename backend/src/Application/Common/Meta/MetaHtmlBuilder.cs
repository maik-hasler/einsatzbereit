namespace Application.Common.Meta;

// Builds a standalone HTML document carrying one entity's own title/description/image
// in its OG and Twitter card tags, instead of the site-wide metadata frontend/index.html
// hardcodes (einsatzbereit#1680). Only ever reaches a social-preview crawler or search
// engine - frontend/nginx.conf.template only proxies here for User-Agents it recognizes
// as a bot, so this never needs to match the SPA's own hashed asset filenames or ship a
// working app shell, just correct tags plus a plain fallback link for anything that does
// render the body.
public static class MetaHtmlBuilder
{
	// OG/Twitter cards truncate long descriptions anyway; capping here keeps the
	// preview's own wording intact instead of an engine-chosen cut mid-sentence.
	private const int MaxDescriptionLength = 200;

	public static string Build(
		string title,
		string? description,
		string canonicalUrl,
		string imageUrl)
	{
		var fallbackDescription =
			"Einsatzbereit verbindet engagierte Freiwillige mit regionalen Hilfsangeboten.";
		var encodedTitle = HtmlEscape(title);
		var encodedDescription = HtmlEscape(
			Truncate(string.IsNullOrWhiteSpace(description) ? fallbackDescription : description));
		var encodedCanonicalUrl = HtmlEscape(canonicalUrl);
		var encodedImageUrl = HtmlEscape(imageUrl);

		return $"""
			<!doctype html>
			<html lang="de">
				<head>
					<meta charset="UTF-8" />
					<meta name="viewport" content="width=device-width, initial-scale=1.0" />
					<meta name="color-scheme" content="light" />
					<link rel="icon" type="image/svg+xml" href="/favicon.svg" />
					<link rel="apple-touch-icon" href="/icons/icon-192.png" />
					<meta name="theme-color" content="#2d8a5e" />
					<title>{encodedTitle}</title>
					<meta name="description" content="{encodedDescription}" />
					<link rel="canonical" href="{encodedCanonicalUrl}" />
					<meta property="og:type" content="website" />
					<meta property="og:url" content="{encodedCanonicalUrl}" />
					<meta property="og:title" content="{encodedTitle}" />
					<meta property="og:description" content="{encodedDescription}" />
					<meta property="og:image" content="{encodedImageUrl}" />
					<meta name="twitter:card" content="summary_large_image" />
					<meta name="twitter:title" content="{encodedTitle}" />
					<meta name="twitter:description" content="{encodedDescription}" />
					<meta name="twitter:image" content="{encodedImageUrl}" />
				</head>
				<body>
					<p><a href="{encodedCanonicalUrl}">{encodedTitle}</a></p>
					<p>{encodedDescription}</p>
				</body>
			</html>
			""";
	}

	private static string Truncate(string value)
	{
		if (value.Length <= MaxDescriptionLength)
			return value;

		// Back off one code unit when the cut would land inside a UTF-16 surrogate
		// pair (e.g. an emoji in a user-entered description) - otherwise the
		// trailing half-character renders as U+FFFD in any client that validates it.
		var cutIndex = char.IsLowSurrogate(value[MaxDescriptionLength])
			? MaxDescriptionLength - 1
			: MaxDescriptionLength;

		return string.Concat(value.AsSpan(0, cutIndex).TrimEnd(), "...");
	}

	// Escapes only what HTML text/double-quoted-attribute content actually needs -
	// unlike WebUtility.HtmlEncode, this leaves non-ASCII characters (e.g. umlauts in
	// organization/opportunity names) as literal UTF-8 rather than numeric character
	// references, which the document's own <meta charset="UTF-8" /> already covers.
	private static string HtmlEscape(string value) =>
		value
			.Replace("&", "&amp;")
			.Replace("<", "&lt;")
			.Replace(">", "&gt;")
			.Replace("\"", "&quot;")
			.Replace("'", "&#39;");
}
