using System.Net.Http.Headers;
using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.Reports;
using Domain.Users;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

// Exercises Infrastructure.BackgroundJobs.ReportRetentionJob.DeleteExpiredReportsAsync
// directly (InternalsVisibleTo, see Infrastructure.csproj) against the real integration
// Postgres, rather than waiting a real 24-hour tick for the pruning behavior (#1725)
// to become observable.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class ReportRetentionJobTests(IntegrationTestFixture fixture)
{
	private static readonly DateTimeOffset ResolvedCutoff = DateTimeOffset.UtcNow.AddDays(-180);

	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task DeleteExpiredReportsAsync_ResolvedReportPastRetention_IsRemoved(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var reportId = await SeedReportAsync(
			dbContext,
			ReportTargetType.Organization,
			targetId: Guid.NewGuid(),
			resolvedOn: ResolvedCutoff.AddDays(-1),
			cancellationToken);

		var deleted = await ReportRetentionJob.DeleteExpiredReportsAsync(dbContext, ResolvedCutoff, cancellationToken);

		deleted.Should().Be(1);
		await ReportShouldNotExistAsync(dbContext, reportId, cancellationToken);
	}

	[Test]
	public async Task DeleteExpiredReportsAsync_ResolvedReportWithinRetention_IsNotRemoved(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var reportId = await SeedReportAsync(
			dbContext,
			ReportTargetType.Organization,
			targetId: Guid.NewGuid(),
			resolvedOn: ResolvedCutoff.AddDays(1),
			cancellationToken);

		var deleted = await ReportRetentionJob.DeleteExpiredReportsAsync(dbContext, ResolvedCutoff, cancellationToken);

		deleted.Should().Be(0);
		await ReportShouldExistAsync(dbContext, reportId, cancellationToken);
	}

	[Test]
	public async Task DeleteExpiredReportsAsync_OpenReportAgainstAnExistingTarget_IsNeverRemoved(
		CancellationToken cancellationToken)
	{
		// An Open report is never pruned by the resolved-retention rule
		// regardless of age, and targets an Organization here specifically to
		// stay clear of the orphaned-User-target rule too.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var reportId = await SeedReportAsync(
			dbContext,
			ReportTargetType.Organization,
			targetId: Guid.NewGuid(),
			resolvedOn: null,
			cancellationToken);

		var deleted = await ReportRetentionJob.DeleteExpiredReportsAsync(dbContext, ResolvedCutoff, cancellationToken);

		deleted.Should().Be(0);
		await ReportShouldExistAsync(dbContext, reportId, cancellationToken);
	}

	[Test]
	public async Task DeleteExpiredReportsAsync_ReportTargetingAUserRowThatNoLongerExists_IsRemoved(
		CancellationToken cancellationToken)
	{
		// #1725: a report naming a user as its target must not outlive that
		// user's account once it has been fully (hard-)deleted - regardless of
		// the report's own resolution status or age.
		await using var dbContext = fixture.CreateApplicationDbContext();
		var reportId = await SeedReportAsync(
			dbContext,
			ReportTargetType.User,
			targetId: Guid.NewGuid(),
			resolvedOn: null,
			cancellationToken);

		var deleted = await ReportRetentionJob.DeleteExpiredReportsAsync(dbContext, ResolvedCutoff, cancellationToken);

		deleted.Should().Be(1);
		await ReportShouldNotExistAsync(dbContext, reportId, cancellationToken);
	}

	[Test]
	public async Task DeleteExpiredReportsAsync_ReportTargetingAShadowDeletedUser_IsNotRemoved(
		CancellationToken cancellationToken)
	{
		// A shadow-deleted user's row still physically exists (hidden only by
		// UserConfiguration's !IsDeleted query filter, which this job's raw SQL
		// deliberately bypasses) - their reports are moderation history and
		// must survive until either the resolved-retention rule applies or the
		// account is later fully erased.
		var (userId, username, password) = await fixture.CreateEphemeralUserAsync(cancellationToken);
		var userClient = await CreateAuthenticatedClientAsync(username, password);
		await userClient.GetUserProfileAsync(cancellationToken);

		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");
		await adminClient.AdminShadowDeleteUserAsync(userId, cancellationToken);

		await using var dbContext = fixture.CreateApplicationDbContext();
		var reportId = await SeedReportAsync(
			dbContext,
			ReportTargetType.User,
			targetId: userId,
			resolvedOn: null,
			cancellationToken);

		var deleted = await ReportRetentionJob.DeleteExpiredReportsAsync(dbContext, ResolvedCutoff, cancellationToken);

		deleted.Should().Be(0);
		await ReportShouldExistAsync(dbContext, reportId, cancellationToken);
	}

	private static async Task<ReportId> SeedReportAsync(
		ApplicationDbContext dbContext,
		ReportTargetType targetType,
		Guid targetId,
		DateTimeOffset? resolvedOn,
		CancellationToken cancellationToken)
	{
		var report = Report.Create(
			targetType,
			targetId,
			UserId.Create(Guid.NewGuid()).GetValueOrThrow(),
			ReportReason.Other,
			details: null).GetValueOrThrow();

		if (resolvedOn is not null)
			report.Dismiss(UserId.Create(Guid.NewGuid()).GetValueOrThrow(), resolvedOn.Value).ThrowIfFailure();

		dbContext.Set<Report>().Add(report);
		await dbContext.SaveChangesAsync(cancellationToken);

		return report.Id;
	}

	private static async Task ReportShouldExistAsync(
		ApplicationDbContext dbContext, ReportId id, CancellationToken cancellationToken)
	{
		var exists = await dbContext.Set<Report>()
			.AsNoTracking()
			.AnyAsync(r => r.Id == id, cancellationToken);
		exists.Should().BeTrue();
	}

	private static async Task ReportShouldNotExistAsync(
		ApplicationDbContext dbContext, ReportId id, CancellationToken cancellationToken)
	{
		var exists = await dbContext.Set<Report>()
			.AsNoTracking()
			.AnyAsync(r => r.Id == id, cancellationToken);
		exists.Should().BeFalse();
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(
		string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}
}
