using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

// A hard technical cap on outbound SMTP sends, independent of how many logical emails the
// application decides to trigger - the last line of defense against exceeding the hosting
// provider's real sending limit (see SmtpOptions.MaxEmailsPerHour). Backed by a sliding
// window so a caller waits for a free slot instead of the send simply being dropped; a
// bounded queue still exists so a genuine overload fails fast instead of queuing forever.
internal sealed class EmailRateLimiter : IDisposable
{
	private readonly RateLimiter _limiter;

	public EmailRateLimiter(IOptions<SmtpOptions> options)
	{
		_limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
		{
			PermitLimit = Math.Max(1, options.Value.MaxEmailsPerHour),
			Window = TimeSpan.FromHours(1),
			SegmentsPerWindow = 12,
			QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
			QueueLimit = 1000,
			AutoReplenishment = true,
		});
	}

	public async Task WaitForPermitAsync(CancellationToken cancellationToken)
	{
		using var lease = await _limiter.AcquireAsync(1, cancellationToken);
		if (!lease.IsAcquired)
			throw new InvalidOperationException(
				"SMTP send rejected: the outbound email rate limit queue is full. This email will be retried later.");
	}

	public void Dispose() => _limiter.Dispose();
}
