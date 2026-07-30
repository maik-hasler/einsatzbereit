namespace Application.Common.Email;

public static class EmailFooter
{
	public static string Append(string body, string unsubscribeUrl) =>
		$"{body}\n\n---\nDon't want to receive this type of email? Unsubscribe here: {unsubscribeUrl}";
}
