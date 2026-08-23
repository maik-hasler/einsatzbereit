using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.Reports;
using Domain.Users;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class AbuseReportRetentionJobTests(IntegrationTestFixture fixture)
{
	private static readonly DateTimeOffset TargetDeletedCutoff = DateTimeOffset.UtcNow.AddDays(-180);

	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task DeleteExpiredReportsAsync_ReportPastRetention_IsRemoved(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var reportId = await SeedReportAsync(
			dbContext, targetDeletedOn: TargetDeletedCutoff.AddDays(-1), cancellationToken);

		var deleted = await AbuseReportRetentionJob.DeleteExpiredReportsAsync(
			dbContext, TargetDeletedCutoff, cancellationToken);

		deleted.Should().Be(1);
		await ReportShouldNotExistAsync(dbContext, reportId, cancellationToken);
	}

	[Test]
	public async Task DeleteExpiredReportsAsync_ReportWithinRetention_IsNotRemoved(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var reportId = await SeedReportAsync(
			dbContext, targetDeletedOn: TargetDeletedCutoff.AddDays(1), cancellationToken);

		var deleted = await AbuseReportRetentionJob.DeleteExpiredReportsAsync(
			dbContext, TargetDeletedCutoff, cancellationToken);

		deleted.Should().Be(0);
		await ReportShouldExistAsync(dbContext, reportId, cancellationToken);
	}

	[Test]
	public async Task DeleteExpiredReportsAsync_ReportWhoseTargetWasNeverDeleted_IsNeverRemoved(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var reportId = await SeedReportAsync(
			dbContext, targetDeletedOn: null, cancellationToken);

		var deleted = await AbuseReportRetentionJob.DeleteExpiredReportsAsync(
			dbContext, TargetDeletedCutoff, cancellationToken);

		deleted.Should().Be(0);
		await ReportShouldExistAsync(dbContext, reportId, cancellationToken);
	}

	private static async Task<ReportId> SeedReportAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset? targetDeletedOn,
		CancellationToken cancellationToken)
	{
		var report = Report.Create(
			ReportTargetType.User,
			Guid.NewGuid(),
			UserId.Create(Guid.NewGuid()).GetValueOrThrow(),
			ReportReason.Harassment,
			details: null).Value;

		if (targetDeletedOn is { } deletedOn)
			report.MarkTargetDeleted(deletedOn);

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
}
