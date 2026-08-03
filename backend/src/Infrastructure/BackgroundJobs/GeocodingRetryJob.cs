using Application.Common.Exceptions;
using Application.Common.Geocoding;
using Domain.VolunteerOpportunities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

// Backstops coordinate resolution for opportunities whose address is still
// unresolved an hour after creation/update - normally
// GeocodeVolunteerOpportunityAddressHandler resolves it within seconds via the
// outbox pipeline, but a GeocodingOutcome.TransientFailure there (Nominatim
// outage, timeout) leaves it for this job to retry. Rows with
// AddressGeocodingFailed set (a confirmed NotFound) are excluded so a
// permanently-bad address isn't retried every hour forever.
internal sealed class GeocodingRetryJob(
	IServiceScopeFactory scopeFactory,
	ILogger<GeocodingRetryJob> logger)
	: IHostedService, IAsyncDisposable
{
	private Task _executeTask = Task.CompletedTask;
	private CancellationTokenSource? _cts;
	private PeriodicTimer? _timer;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_timer = new PeriodicTimer(TimeSpan.FromHours(1));
		_executeTask = RunLoopAsync(_cts.Token);
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_cts is not null)
			await _cts.CancelAsync();

		try
		{
			await _executeTask.WaitAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
		}
	}

	public ValueTask DisposeAsync()
	{
		_timer?.Dispose();
		_cts?.Dispose();
		return ValueTask.CompletedTask;
	}

	private async Task RunLoopAsync(CancellationToken ct)
	{
		if (_timer is null) return;

		while (!ct.IsCancellationRequested && await _timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
		{
			await RetryFailedGeocodingAsync(ct).ConfigureAwait(false);
		}
	}

	private async Task RetryFailedGeocodingAsync(CancellationToken ct)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var geocodingService = scope.ServiceProvider.GetRequiredService<IGeocodingService>();

		var opportunities = await dbContext.Set<VolunteerOpportunity>()
			.Where(o => !o.IsRemote && !o.AddressGeocodingFailed && o.Address != null && o.Address.Latitude == null)
			.ToListAsync(ct);

		foreach (var opportunity in opportunities)
		{
			try
			{
				var address = opportunity.Address!;

				var result = await geocodingService.GeocodeAsync(
					address.Street, address.HouseNumber, address.ZipCode, address.City, ct);

				switch (result.Outcome)
				{
					case GeocodingOutcome.Found:
						var enriched = address.WithCoordinates(
							result.Coordinates!.Latitude, result.Coordinates.Longitude).GetValueOrThrow();

						opportunity.ApplyGeocodingResult(enriched);

						await dbContext.SaveChangesAsync(ct);

						logger.LogInformation(
							"Backfilled coordinates for volunteer opportunity {OpportunityId} after a previously failed geocoding attempt.",
							opportunity.Id.Value);
						break;

					case GeocodingOutcome.NotFound:
						opportunity.MarkAddressGeocodingFailed();

						await dbContext.SaveChangesAsync(ct);

						logger.LogWarning(
							"Volunteer opportunity {OpportunityId}'s address could not be located; will not retry.",
							opportunity.Id.Value);
						break;

					default:
						break;
				}
			}
			catch (Exception ex)
			{
				logger.LogError(
					ex,
					"Failed to retry geocoding for volunteer opportunity {OpportunityId}.",
					opportunity.Id.Value);
			}
		}
	}
}
