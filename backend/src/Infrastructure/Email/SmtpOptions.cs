namespace Infrastructure.Email;

internal sealed class SmtpOptions
{
	public string Host { get; init; } = "localhost";
	public int Port { get; init; } = 1025;
	public string FromAddress { get; init; } = "noreply@einsatzbereit.local";
	public string FromName { get; init; } = "Einsatzbereit";
}
