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
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }), new FakeTimeProvider());

		await sut.InvokeAsync(CreateAuthenticatedContext(Guid.NewGuid().ToString()), sender);

		sender.SentRequests.Should().ContainSingle().Which.Should().BeOfType<RecordLoginCommand>();
	}

	[Test]
	public async Task InvokeAsync_ShouldNotRecordLoginAgain_OnSecondRequestSameDay()
	{
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }), new FakeTimeProvider());
		var subClaim = Guid.NewGuid().ToString();

		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);
		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);

		sender.SentRequests.Should().ContainSingle();
	}

	[Test]
	public async Task InvokeAsync_ShouldRecordOncePerDistinctLocalDay_WhenXTimezoneHeaderAlternatesBetweenExtremeZones()
	{
		// Pacific/Kiritimati (UTC+14) and Pacific/Niue (UTC-11) are 25 hours apart, so at
		// any single instant they always disagree about the calendar date, regardless of
		// what that instant is - so this can safely use the real clock (no historical
		// FakeTimeProvider value to fight IMemoryCache's own real-time expiration check).
		// The cache must still collapse repeats of the SAME local day down to one send
		// each (#2203) - it must not thrash into more than the 2 distinct days implied.
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }), new FakeTimeProvider());
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
		// A caller in Asia/Tokyo (UTC+9, no DST) can cross into their own next local day
		// well before Berlin's own midnight. Anchored to the real clock (not a hardcoded
		// date) so the computed cache expiration is always ahead of whatever IMemoryCache's
		// own real clock reads. The advance is derived from the current Tokyo time-of-day
		// so it always lands just past midnight the next calendar day - exactly one day
		// boundary crossed - no matter what time this test actually runs (#2203).
		var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
		var firstVisit = DateTimeOffset.UtcNow;
		var hoursUntilNextTokyoMidnight = 24 - TimeZoneInfo.ConvertTime(firstVisit, tokyo).TimeOfDay.TotalHours;
		var secondVisit = firstVisit.AddHours(hoursUntilNextTokyoMidnight + 1);
		var firstDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(firstVisit, tokyo).DateTime);
		var secondDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(secondVisit, tokyo).DateTime);
		secondDay.Should().Be(firstDay.AddDays(1),
			"the chosen advance must cross exactly one Tokyo local day for this test to mean anything");

		var timeProvider = new FakeTimeProvider(firstVisit);
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }), timeProvider);
		var subClaim = Guid.NewGuid().ToString();

		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim, "Asia/Tokyo"), sender);
		timeProvider.Advance(TimeSpan.FromHours(hoursUntilNextTokyoMidnight + 1));
		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim, "Asia/Tokyo"), sender);

		sender.SentRequests.Should().HaveCount(2,
			"the caller's own local day had already advanced by the second visit, even though Berlin's midnight had not");
		sender.SentRequests.OfType<RecordLoginCommand>().Select(r => r.Date).Should().Equal(firstDay, secondDay);
	}

	[Test]
	public async Task InvokeAsync_ShouldRecordLoginAgain_AfterServerMidnightRollover()
	{
		// No X-Timezone header means the middleware falls back to the canonical
		// Europe/Berlin zone. The advance is derived from the current Berlin time-of-day
		// so it always lands just past midnight the next calendar day - exactly one day
		// boundary crossed - no matter what time this test actually runs, and always
		// safely ahead of IMemoryCache's own real clock (#2203).
		var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
		var firstVisit = DateTimeOffset.UtcNow;
		var hoursUntilNextBerlinMidnight = 24 - TimeZoneInfo.ConvertTime(firstVisit, berlin).TimeOfDay.TotalHours;

		var timeProvider = new FakeTimeProvider(firstVisit);
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }), timeProvider);
		var subClaim = Guid.NewGuid().ToString();

		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);
		timeProvider.Advance(TimeSpan.FromHours(hoursUntilNextBerlinMidnight + 1));
		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);

		sender.SentRequests.Should().HaveCount(2);
	}

	[Test]
	public async Task InvokeAsync_ShouldNotCallNext_ForAnyConcurrentRequest_UntilTheSharedWriteCompletes()
	{
		var writeGate = new TaskCompletionSource();
		var sender = new GatedRecordingSender(writeGate.Task);
		var nextCallCount = 0;
		var sut = new LoginStreakMiddleware(
			_ => { Interlocked.Increment(ref nextCallCount); return Task.CompletedTask; },
			new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
			new FakeTimeProvider());
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
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }), new FakeTimeProvider());
		var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

		await sut.InvokeAsync(context, sender);

		sender.SentRequests.Should().BeEmpty();
	}

	[Test]
	public async Task InvokeAsync_ShouldAlwaysCallNext()
	{
		var nextCalled = false;
		var sut = new LoginStreakMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }), new FakeTimeProvider());

		await sut.InvokeAsync(CreateAuthenticatedContext(Guid.NewGuid().ToString()), new RecordingSender());

		nextCalled.Should().BeTrue();
	}

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
