namespace Application.Common.Email;

public sealed record EmailMessage(string To, string Subject, string Body);

public interface IEmailService
{
	Task SendAsync(
		string to,
		string subject,
		string body,
		CancellationToken cancellationToken = default);

	// Sends every message over a single connection instead of one connect/
	// authenticate/disconnect round trip per message. Never throws - like
	// SendAsync, a failure is logged and recorded via EmailMetrics - and the
	// result list is positional: result[i] reports the outcome for messages[i].
	Task<IReadOnlyList<bool>> SendBatchAsync(
		IReadOnlyList<EmailMessage> messages,
		CancellationToken cancellationToken = default);
}
