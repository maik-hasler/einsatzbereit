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
			logger.LogError(ex, "Failed to send email to {To} with subject {Subject}", to, subject);
			metrics.RecordFailed();
		}
	}
}
