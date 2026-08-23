using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Users;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;

using DomainOrganization = Domain.Organizations.Organization;
using DomainOrganizationId = Domain.Organizations.OrganizationId;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class InvitationExpiryJobTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task ExpireDueInvitationsAsync_PendingInvitationPastItsWindow_IsExpired(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var createdAt = DateTimeOffset.UtcNow.AddDays(-(OrganizationInvitation.ExpiryWindowDays + 1));
		var invitationId = await SeedInvitationAsync(dbContext, createdAt, cancellationToken);

		var expired = await InvitationExpiryJob.ExpireDueInvitationsAsync(
			dbContext, DateTimeOffset.UtcNow, cancellationToken);

		expired.Should().Be(1);

		var status = await dbContext.Set<OrganizationInvitation>()
			.AsNoTracking()
			.Where(i => i.Id == invitationId)
			.Select(i => i.Status)
			.SingleAsync(cancellationToken);
		status.Should().Be(InvitationStatus.Expired);
	}

	[Test]
	public async Task ExpireDueInvitationsAsync_PendingInvitationStillWithinItsWindow_IsNotExpired(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var createdAt = DateTimeOffset.UtcNow.AddDays(-1);
		var invitationId = await SeedInvitationAsync(dbContext, createdAt, cancellationToken);

		var expired = await InvitationExpiryJob.ExpireDueInvitationsAsync(
			dbContext, DateTimeOffset.UtcNow, cancellationToken);

		expired.Should().Be(0);

		var status = await dbContext.Set<OrganizationInvitation>()
			.AsNoTracking()
			.Where(i => i.Id == invitationId)
			.Select(i => i.Status)
			.SingleAsync(cancellationToken);
		status.Should().Be(InvitationStatus.Pending);
	}

	[Test]
	public async Task ExpireDueInvitationsAsync_PendingInvitationPastItsWindow_DeletesItsInvitationReceivedNotification(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var createdAt = DateTimeOffset.UtcNow.AddDays(-(OrganizationInvitation.ExpiryWindowDays + 1));
		var invitationId = await SeedInvitationAsync(dbContext, createdAt, cancellationToken);

		var notification = Notification.Create(UserId.New(), NotificationKind.InvitationReceived, invitationId.Value);
		dbContext.Set<Notification>().Add(notification);
		await dbContext.SaveChangesAsync(cancellationToken);

		var expired = await InvitationExpiryJob.ExpireDueInvitationsAsync(
			dbContext, DateTimeOffset.UtcNow, cancellationToken);

		expired.Should().Be(1);

		var remainingNotification = await dbContext.Set<Notification>()
			.AsNoTracking()
			.Where(n => n.Id == notification.Id)
			.SingleOrDefaultAsync(cancellationToken);
		remainingNotification.Should().BeNull();
	}

	[Test]
	public async Task ExpireDueInvitationsAsync_AlreadyAcceptedInvitationPastTheWindow_IsNotTouched(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var createdAt = DateTimeOffset.UtcNow.AddDays(-(OrganizationInvitation.ExpiryWindowDays + 1));
		var invitationId = await SeedInvitationAsync(dbContext, createdAt, cancellationToken, accept: true);

		var expired = await InvitationExpiryJob.ExpireDueInvitationsAsync(
			dbContext, DateTimeOffset.UtcNow, cancellationToken);

		expired.Should().Be(0);

		var status = await dbContext.Set<OrganizationInvitation>()
			.AsNoTracking()
			.Where(i => i.Id == invitationId)
			.Select(i => i.Status)
			.SingleAsync(cancellationToken);
		status.Should().Be(InvitationStatus.Accepted);
	}

	private static async Task<OrganizationInvitationId> SeedInvitationAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset createdAt,
		CancellationToken cancellationToken,
		bool accept = false)
	{
		var organization = DomainOrganization.Create(DomainOrganizationId.New(), $"ExpiryTestOrg_{Guid.NewGuid()}").GetValueOrThrow();
		dbContext.Set<DomainOrganization>().Add(organization);

		var invitation = OrganizationInvitation.Create(
			organization.Id, UserId.New(), UserId.New(), OrganizationMemberRole.Organizer, createdAt);
		if (accept)
			invitation.Accept().ThrowIfFailure();
		dbContext.Set<OrganizationInvitation>().Add(invitation);

		await dbContext.SaveChangesAsync(cancellationToken);

		return invitation.Id;
	}
}
