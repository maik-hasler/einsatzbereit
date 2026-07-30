namespace Application.Common.Email;

public sealed record EmailContent(string Subject, string Body);

public interface IEmailTemplateRenderer
{
	EmailContent Render(
		EmailTemplateKind kind,
		string language,
		IReadOnlyDictionary<string, string> placeholders);
}
