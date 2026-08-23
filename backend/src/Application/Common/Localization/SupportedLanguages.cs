namespace Application.Common.Localization;

public static class SupportedLanguages
{
	public const string Default = "de";

	private static readonly HashSet<string> Codes = ["de", "en"];

	public static bool IsSupported(string? language) =>
		language is not null && Codes.Contains(language);

	public static string Resolve(string? language) =>
		IsSupported(language) ? language! : Default;
}
