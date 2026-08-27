using System.Security.Claims;
using Api.Common.Middleware;
using Application.Common.Messaging;
using Application.Users.RecordLogin.v1;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace IntegrationTests;

public class LoginStreakMiddlewareTests
{
	[Test]
	public async Task InvokeAsync_ShouldRecordLogin_OnFirstRequestForUser()
	{
		var timeProvider = new FakeTimeProvider();
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, CreateCache(timeProvider), timeProvider);

		await sut.InvokeAsync(CreateAuthenticatedContext(Guid.NewGuid().ToString()), sender);

		sender.SentRequests.Should().ContainSingle().Which.Should().BeOfType<RecordLoginCommand>();
	}

	[Test]
	public async Task InvokeAsync_ShouldNotRecordLoginAgain_OnSecondRequestSameDay()
	{
		var timeProvider = new FakeTimeProvider();
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, CreateCache(timeProvider), timeProvider);
		var subClaim = Guid.NewGuid().ToString();

		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);
		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);

		sender.SentRequests.Should().ContainSingle();
	}

	[Test]
	public async Task InvokeAsync_ShouldRecordOncePerDistinctLocalDay_WhenXTimezoneHeaderAlternatesBetweenExtremeZones()
	{
		// Pacific/Kiritimati (UTC+14) and Pacific/Niue (UTC-11) are 25 hours apart, so at
		// any single instant they always disagree about the calendar date. The cache must
		// still collapse repeats of the SAME local day down to one send each (#2203) -
		// it must not thrash into more than the 2 distinct days actually implied.
		var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, CreateCache(timeProvider), timeProvider);
		var subClaim = Guid.NewGuid().ToString();

		for (var i = 0; i < 10; i++)
		{
			var tzHeader = i % 2 == 0 ? "Pacific/Kiritimati" : "Pacific/Niue";
			await sut.InvokeAsync(CreateAuthenticatedContext(subClaim, tzHeader), sender);
		}

		sender.SentRequests.Should().HaveCount(2,
			"exactly the 2 distinct local days the alternating header genuinely implies at this instant - one send each, not one per request and not a single shared one");
	}

	[Test]
	public async Task InvokeAsync_ShouldRecordBothVisits_WhenTheClientsLocalDayAdvancesBeforeServerMidnight()
	{
		// A caller in Asia/Tokyo (UTC+9, no DST) visiting at 10:00 and 20:00 Berlin time
		// (both well before the next Berlin midnight) has already crossed into their own
		// next local day by the second visit - the memo must not swallow it just because
		// Berlin's own midnight hasn't passed yet (#2203).
		var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero)); // 10:00 CET
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, CreateCache(timeProvider), timeProvider);
		var subClaim = Guid.NewGuid().ToString();

		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim, "Asia/Tokyo"), sender);
		timeProvider.Advance(TimeSpan.FromHours(10)); // 19:00 UTC = 20:00 CET, still the same Berlin day
		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim, "Asia/Tokyo"), sender);

		sender.SentRequests.Should().HaveCount(2,
			"the caller's own local day had already advanced by the second visit, even though Berlin's midnight had not");
		sender.SentRequests.OfType<RecordLoginCommand>().Select(r => r.Date).Should().Equal(
			new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 16));
	}

	[Test]
	public async Task InvokeAsync_ShouldRecordLoginAgain_AfterServerMidnightRollover()
	{
		var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, CreateCache(timeProvider), timeProvider);
		var subClaim = Guid.NewGuid().ToString();

		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);
		timeProvider.Advance(TimeSpan.FromHours(13));
		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);

		sender.SentRequests.Should().HaveCount(2);
	}

	[Test]
	public async Task InvokeAsync_ShouldNotCallNext_ForAnyConcurrentRequest_UntilTheSharedWriteCompletes()
	{
		var timeProvider = new FakeTimeProvider();
		var writeGate = new TaskCompletionSource();
		var sender = new GatedRecordingSender(writeGate.Task);
		var nextCallCount = 0;
		var sut = new LoginStreakMiddleware(
			_ => { Interlocked.Increment(ref nextCallCount); return Task.CompletedTask; },
			CreateCache(timeProvider),
			timeProvider);
		var subClaim = Guid.NewGuid().ToString();

		var task1 = sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);
		var task2 = sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);

		sender.SentRequests.Should().ContainSingle(
			"the write must be single-flighted - two concurrent requests for the same "
			+ "user must not each start their own RecordLoginCommand");
		nextCallCount.Should().Be(0,
			"neither concurrent request should reach its own handler before the shared "
			+ "login-streak write has completed");
		task1.IsCompleted.Should().BeFalse();
		task2.IsCompleted.Should().BeFalse();

		writeGate.SetResult();
		await Task.WhenAll(task1, task2);

		nextCallCount.Should().Be(2, "both requests must still proceed once the shared write finishes");
		sender.SentRequests.Should().ContainSingle("the write must still only have happened once");
	}

	[Test]
	public async Task InvokeAsync_ShouldNotCallSender_WhenUserIsNotAuthenticated()
	{
		var timeProvider = new FakeTimeProvider();
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, CreateCache(timeProvider), timeProvider);
		var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

		await sut.InvokeAsync(context, sender);

		sender.SentRequests.Should().BeEmpty();
	}

	[Test]
	public async Task InvokeAsync_ShouldAlwaysCallNext()
	{
		var timeProvider = new FakeTimeProvider();
		var nextCalled = false;
		var sut = new LoginStreakMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, CreateCache(timeProvider), timeProvider);

		await sut.InvokeAsync(CreateAuthenticatedContext(Guid.NewGuid().ToString()), new RecordingSender());

		nextCalled.Should().BeTrue();
	}

	// IMemoryCache evaluates AbsoluteExpiration against its own internal clock, which
	// defaults to real wall-clock time regardless of what TimeProvider the middleware
	// itself was given - without wiring the same one in here, every entry this suite
	// sets with a historical FakeTimeProvider "now" computes an expiration that is
	// already in the past by the time the real clock reads it, so nothing ever hits
	// the cache at all (#2203 caught this: 10 sends instead of 2, i.e. no caching).
	private static MemoryCache CreateCache(TimeProvider timeProvider) =>
		new(new MemoryCacheOptions { SizeLimit = 100, TimeProvider = timeProvider });

	private static DefaultHttpContext CreateAuthenticatedContext(string subClaim, string? tzHeader = null)
	{
		var context = new DefaultHttpContext
		{
			User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subClaim)], authenticationType: "Test")),
		};

		if (tzHeader is not null)
			context.Request.Headers["X-Timezone"] = tzHeader;

		return context;
	}

	private sealed class RecordingSender : ISender
	{
		public List<object?> SentRequests { get; } = [];

		public ValueTask<TResponse> Send<TResponse>(
			IRequest<TResponse> request,
			CancellationToken cancellationToken = default)
		{
			SentRequests.Add(request);
			return ValueTask.FromResult<TResponse>(default!);
		}
	}

	private sealed class GatedRecordingSender(Task gate) : ISender
	{
		public List<object?> SentRequests { get; } = [];

		public async ValueTask<TResponse> Send<TResponse>(
			IRequest<TResponse> request,
			CancellationToken cancellationToken = default)
		{
			SentRequests.Add(request);
			await gate;
			return default!;
		}
	}

	private sealed class FakeTimeProvider(DateTimeOffset? initialUtcNow = null) : TimeProvider
	{
		private DateTimeOffset _utcNow = initialUtcNow ?? DateTimeOffset.UtcNow;

		public override DateTimeOffset GetUtcNow() => _utcNow;

		public void Advance(TimeSpan by) => _utcNow += by;
	}
}
