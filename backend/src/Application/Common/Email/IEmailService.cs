namespace Application.Common.Email;

public sealed record EmailMessage(string To, string Subject, string Body, string CorrelationId);

public interface IEmailService
{
	Task SendAsync(
		string to,
		string subject,
		string body,
		string correlationId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<bool>> SendBatchAsync(
		IReadOnlyList<EmailMessage> messages,
		CancellationToken cancellationToken = default);
}
