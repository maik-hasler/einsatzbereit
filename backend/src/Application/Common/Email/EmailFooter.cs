namespace Application.Common.Email;

public static class EmailFooter
{
	public static string Append(
		IEmailTemplateRenderer emailTemplateRenderer,
		string language,
		string body,
		string unsubscribeUrl)
	{
		var footer = emailTemplateRenderer.Render(
			EmailTemplateKind.EmailFooter,
			language,
			new Dictionary<string, string> { ["UnsubscribeUrl"] = unsubscribeUrl }).Body;

		return $"{body}{footer}";
	}
}
