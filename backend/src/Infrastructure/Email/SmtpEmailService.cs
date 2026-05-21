using System.Net.Mail;
using Application.Common.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

internal sealed class SmtpEmailService(
	IOptions<SmtpOptions> options,
	ILogger<SmtpEmailService> logger)
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
#pragma warning disable SYSLIB0006
			using var client = new SmtpClient(_options.Host, _options.Port)
			{
				DeliveryMethod = SmtpDeliveryMethod.Network,
				EnableSsl = false,
				UseDefaultCredentials = false
			};
#pragma warning restore SYSLIB0006

			using var message = new MailMessage
			{
				From = new MailAddress(_options.FromAddress, _options.FromName),
				Subject = subject,
				Body = body,
				IsBodyHtml = false
			};
			message.To.Add(to);

			await client.SendMailAsync(message, cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to send email to {To} with subject {Subject}", to, subject);
		}
	}
}
