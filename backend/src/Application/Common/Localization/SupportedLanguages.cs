namespace Application.Common.Localization;

// The only two languages the product ships translations for (frontend
// en.json/de.json). A recipient's language always resolves to one of these -
// never null - so every email-sending handler can render a template without
// its own null-handling branch.
public static class SupportedLanguages
{
	public const string Default = "de";

	private static readonly HashSet<string> Codes = ["de", "en"];

	public static bool IsSupported(string? language) =>
		language is not null && Codes.Contains(language);

	public static string Resolve(string? language) =>
		IsSupported(language) ? language! : Default;
}
