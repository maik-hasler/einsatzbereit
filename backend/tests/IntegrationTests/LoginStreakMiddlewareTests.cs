using System.Security.Claims;
using Api.Common.Middleware;
using Application.Common.Messaging;
using Application.Users.RecordLogin.v1;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace IntegrationTests;

// LoginStreakMiddleware is internal to Api (InternalsVisibleTo only grants
// ArchitectureTests and IntegrationTests, see Api.csproj), so this has to live
// here rather than in a project that can't see it. Pure logic - no database,
// no real request pipeline needed.
//
// Regression coverage for #1185: the middleware used to dedupe "have we
// recorded today's login for this user" with a single process-wide static
// HashSet, cleared whenever the client-supplied X-Timezone header produced a
// different calendar date than the previous request. Because that header is
// attacker-controlled, alternating between two far-apart zones could thrash
// the reset and force RecordLoginCommand (a DB write) to refire on every
// request for every user. The fix keys dedup per user in an IMemoryCache
// entry that expires at a fixed server-timezone midnight, never a
// header-derived one.
public class LoginStreakMiddlewareTests
{
	[Test]
	public async Task InvokeAsync_ShouldRecordLogin_OnFirstRequestForUser()
	{
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, new MemoryCache(new MemoryCacheOptions()), new FakeTimeProvider());

		await sut.InvokeAsync(CreateAuthenticatedContext(Guid.NewGuid().ToString()), sender);

		sender.SentRequests.Should().ContainSingle().Which.Should().BeOfType<RecordLoginCommand>();
	}

	[Test]
	public async Task InvokeAsync_ShouldNotRecordLoginAgain_OnSecondRequestSameDay()
	{
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, new MemoryCache(new MemoryCacheOptions()), new FakeTimeProvider());
		var subClaim = Guid.NewGuid().ToString();

		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);
		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);

		sender.SentRequests.Should().ContainSingle();
	}

	[Test]
	public async Task InvokeAsync_ShouldNotRecordLoginRepeatedly_WhenXTimezoneHeaderAlternatesAcrossRequests()
	{
		// The exact pair of near-antipodal zones from the issue report - about 25
		// hours apart, so alternating between them almost always produced a
		// different local calendar date on the old, vulnerable code path.
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, new MemoryCache(new MemoryCacheOptions()), new FakeTimeProvider());
		var subClaim = Guid.NewGuid().ToString();

		for (var i = 0; i < 10; i++)
		{
			var tzHeader = i % 2 == 0 ? "Pacific/Kiritimati" : "Pacific/Niue";
			await sut.InvokeAsync(CreateAuthenticatedContext(subClaim, tzHeader), sender);
		}

		sender.SentRequests.Should().ContainSingle(
			"X-Timezone must only shape the streak's local date, never thrash the shared dedup cache");
	}

	[Test]
	public async Task InvokeAsync_ShouldRecordLoginAgain_AfterServerMidnightRollover()
	{
		// 2026-06-15 10:00 UTC = 12:00 in Europe/Berlin (CEST, UTC+2) - well clear of
		// its own midnight. Advancing 13 hours crosses the next Berlin midnight
		// (2026-06-15 22:00 UTC), so the per-user cache entry must have expired.
		var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, new MemoryCache(new MemoryCacheOptions()), timeProvider);
		var subClaim = Guid.NewGuid().ToString();

		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);
		timeProvider.Advance(TimeSpan.FromHours(13));
		await sut.InvokeAsync(CreateAuthenticatedContext(subClaim), sender);

		sender.SentRequests.Should().HaveCount(2);
	}

	[Test]
	public async Task InvokeAsync_ShouldNotCallSender_WhenUserIsNotAuthenticated()
	{
		var sender = new RecordingSender();
		var sut = new LoginStreakMiddleware(_ => Task.CompletedTask, new MemoryCache(new MemoryCacheOptions()), new FakeTimeProvider());
		var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

		await sut.InvokeAsync(context, sender);

		sender.SentRequests.Should().BeEmpty();
	}

	[Test]
	public async Task InvokeAsync_ShouldAlwaysCallNext()
	{
		var nextCalled = false;
		var sut = new LoginStreakMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, new MemoryCache(new MemoryCacheOptions()), new FakeTimeProvider());

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

	private sealed class FakeTimeProvider(DateTimeOffset? initialUtcNow = null) : TimeProvider
	{
		private DateTimeOffset _utcNow = initialUtcNow ?? DateTimeOffset.UtcNow;

		public override DateTimeOffset GetUtcNow() => _utcNow;

		public void Advance(TimeSpan by) => _utcNow += by;
	}
}
