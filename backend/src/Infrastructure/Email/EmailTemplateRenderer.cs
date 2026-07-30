using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Common.Email;
using Application.Common.Localization;

namespace Infrastructure.Email;

// Templates are embedded JSON resources (Email/Templates/{language}.json,
// one file per supported language, keyed by EmailTemplateKind) rather than
// .resx - mirrors the frontend's own en.json/de.json convention so the same
// mental model applies on both sides. Parsed once at startup; this class is
// registered as a singleton.
internal sealed class EmailTemplateRenderer
	: IEmailTemplateRenderer
{
	private sealed record TemplateDefinition(
		[property: JsonPropertyName("subject")] string Subject,
		[property: JsonPropertyName("body")] string Body);

	private readonly IReadOnlyDictionary<string, Dictionary<EmailTemplateKind, TemplateDefinition>> _templatesByLanguage;

	public EmailTemplateRenderer()
	{
		_templatesByLanguage = new Dictionary<string, Dictionary<EmailTemplateKind, TemplateDefinition>>
		{
			["en"] = LoadTemplates("en"),
			["de"] = LoadTemplates("de"),
		};
	}

	public EmailContent Render(
		EmailTemplateKind kind,
		string language,
		IReadOnlyDictionary<string, string> placeholders)
	{
		var resolvedLanguage = SupportedLanguages.Resolve(language);
		var templates = _templatesByLanguage[resolvedLanguage];

		if (!templates.TryGetValue(kind, out var template))
			throw new InvalidOperationException($"No email template registered for '{kind}' in language '{resolvedLanguage}'.");

		return new EmailContent(
			Interpolate(template.Subject, placeholders),
			Interpolate(template.Body, placeholders));
	}

	private static string Interpolate(string template, IReadOnlyDictionary<string, string> placeholders)
	{
		var result = template;
		foreach (var (key, value) in placeholders)
			result = result.Replace($"{{{key}}}", value);
		return result;
	}

	private static Dictionary<EmailTemplateKind, TemplateDefinition> LoadTemplates(string language)
	{
		var resourceName = $"Infrastructure.Email.Templates.{language}.json";
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
			?? throw new InvalidOperationException($"Embedded email template resource '{resourceName}' was not found.");

		var raw = JsonSerializer.Deserialize<Dictionary<string, TemplateDefinition>>(stream)
			?? throw new InvalidOperationException($"Embedded email template resource '{resourceName}' is empty or invalid.");

		return raw.ToDictionary(
			kvp => Enum.Parse<EmailTemplateKind>(kvp.Key),
			kvp => kvp.Value);
	}
}
