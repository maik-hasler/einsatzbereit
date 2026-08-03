namespace Application.Common.Email;

// CorrelationId ties a send back to the domain object it was sent for (an
// engagement, invitation, or user id) so a failure can still be investigated
// without the recipient's address or subject - which may itself contain PII
// like a volunteer's name - ever reaching the logs (einsatzbereit#1189).
public sealed record EmailMessage(string To, string Subject, string Body, string CorrelationId);

public interface IEmailService
{
	Task SendAsync(
		string to,
		string subject,
		string body,
		string correlationId,
		CancellationToken cancellationToken = default);

	// Sends every message over a single connection instead of one connect/
	// authenticate/disconnect round trip per message. Never throws - like
	// SendAsync, a failure is logged and recorded via EmailMetrics - and the
	// result list is positional: result[i] reports the outcome for messages[i].
	Task<IReadOnlyList<bool>> SendBatchAsync(
		IReadOnlyList<EmailMessage> messages,
		CancellationToken cancellationToken = default);
}
