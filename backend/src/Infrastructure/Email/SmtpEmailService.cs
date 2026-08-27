using Application.Common.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.Email;

internal sealed class SmtpEmailService(
	IOptions<SmtpOptions> options,
	ILogger<SmtpEmailService> logger,
	EmailMetrics metrics)
	: IEmailService
{
	private readonly SmtpOptions _options = options.Value;

	public async Task SendAsync(
		string to,
		string subject,
		string body,
		string correlationId,
		CancellationToken cancellationToken = default)
	{
		try
		{
			using var client = new SmtpClient();

			var secureSocketOptions = _options.EnableSsl
				? SecureSocketOptions.StartTls
				: SecureSocketOptions.None;
			await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions, cancellationToken);

			if (!string.IsNullOrEmpty(_options.Username))
				await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);

			using var message = new MimeMessage();
			message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
			message.To.Add(MailboxAddress.Parse(to));
			message.Subject = subject;
			message.Body = new TextPart("plain") { Text = body };

			await client.SendAsync(message, cancellationToken);
			await client.DisconnectAsync(true, cancellationToken);

			metrics.RecordSucceeded();
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to send email (correlationId: {CorrelationId})", correlationId);
			metrics.RecordFailed();
			throw;
		}
	}

	public async Task<IReadOnlyList<bool>> SendBatchAsync(
		IReadOnlyList<EmailMessage> messages,
		CancellationToken cancellationToken = default)
	{
		var results = new bool[messages.Count];
		if (messages.Count == 0)
			return results;

		using var client = new SmtpClient();

		try
		{
			var secureSocketOptions = _options.EnableSsl
				? SecureSocketOptions.StartTls
				: SecureSocketOptions.None;
			await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions, cancellationToken);

			if (!string.IsNullOrEmpty(_options.Username))
				await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to establish SMTP connection for batch of {Count} emails", messages.Count);
			for (var i = 0; i < messages.Count; i++)
				metrics.RecordFailed();

			return results;
		}

		for (var i = 0; i < messages.Count; i++)
		{
			var email = messages[i];
			try
			{
				using var message = new MimeMessage();
				message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
				message.To.Add(MailboxAddress.Parse(email.To));
				message.Subject = email.Subject;
				message.Body = new TextPart("plain") { Text = email.Body };

				await client.SendAsync(message, cancellationToken);

				metrics.RecordSucceeded();
				results[i] = true;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to send email (correlationId: {CorrelationId})", email.CorrelationId);
				metrics.RecordFailed();
			}
		}

		try
		{
			await client.DisconnectAsync(true, cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to cleanly disconnect SMTP client after sending a batch of {Count} emails", messages.Count);
		}

		return results;
	}
}
