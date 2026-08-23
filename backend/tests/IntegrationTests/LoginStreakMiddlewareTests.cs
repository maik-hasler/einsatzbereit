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
	public async Task InvokeAsync_ShouldNotCallNext_ForAnyConcurrentRequest_UntilTheSharedWriteCompletes()
	{
		var writeGate = new TaskCompletionSource();
		var sender = new GatedRecordingSender(writeGate.Task);
		var nextCallCount = 0;
		var sut = new LoginStreakMiddleware(
			_ => { Interlocked.Increment(ref nextCallCount); return Task.CompletedTask; },
			new MemoryCache(new MemoryCacheOptions()),
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
