using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.Achievements;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Reports;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Infrastructure.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TUnit.Core.Interfaces;
// OrganizationId collides with the generated ApiClient.cs DTO of the same name in this
// same "IntegrationTests" namespace (see the same workaround in EngagementReadRepositoryTests.cs).
using DomainOrganizationId = Domain.Organizations.OrganizationId;

namespace IntegrationTests;

// Regression coverage for einsatzbereit#1210 - enum-to-string columns had no DB-level
// constraint, so a typo'd or partially-migrated string value could sit in the DB and
// throw when EF tries to parse it back into the CLR enum on read (a 500, not a 400 at
// write time). These tests assert both that valid values still round-trip through the
// full write+read path, and that Postgres now rejects an invalid string outright via the
// CHECK constraints added in the AddEnumCheckConstraints migration - bypassing the
// value converter with a raw UPDATE, since the converter itself would reject a bad
// C# enum value before it ever reached the DB.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class EnumCheckConstraintTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task Achievement_ShouldPersistAndReload_ValidType(
		CancellationToken cancellationToken)
	{
		var achievement = Achievement.Create(UserId.New(), AchievementType.Milestone, "key", "Name", "Description", DateTimeOffset.UtcNow);

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.Achievements.AddAsync(achievement, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await using var readContext = fixture.CreateApplicationDbContext();
		var reloaded = await readContext.Achievements.FindAsync(achievement.Id, cancellationToken);

		reloaded.Should().NotBeNull();
		reloaded!.Type.Should().Be(AchievementType.Milestone);
	}

	[Test]
	public async Task Achievement_Type_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var achievement = Achievement.Create(UserId.New(), AchievementType.Milestone, "key", "Name", "Description", DateTimeOffset.UtcNow);

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.Achievements.AddAsync(achievement, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"achievement", "type", achievement.Id.Value, "ck_achievement_type_valid", cancellationToken);
	}

	[Test]
	public async Task Engagement_ShouldPersistAndReload_ValidStatus(
		CancellationToken cancellationToken)
	{
		var engagement = Engagement.CreateIndividualContact(VolunteerOpportunityId.New(), UserId.New(), "Message").GetValueOrThrow();

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.Engagements.AddAsync(engagement, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await using var readContext = fixture.CreateApplicationDbContext();
		var reloaded = await readContext.Engagements.FindAsync(engagement.Id, cancellationToken);

		reloaded.Should().NotBeNull();
		reloaded!.Status.Should().Be(EngagementStatus.Pending);
	}

	[Test]
	public async Task Engagement_Status_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var engagement = Engagement.CreateIndividualContact(VolunteerOpportunityId.New(), UserId.New(), "Message").GetValueOrThrow();

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.Engagements.AddAsync(engagement, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"engagement", "status", engagement.Id.Value, "ck_engagement_status_valid", cancellationToken);
	}

	[Test]
	public async Task Notification_ShouldPersistAndReload_ValidKind(
		CancellationToken cancellationToken)
	{
		var notification = Notification.Create(UserId.New(), NotificationKind.EngagementCreated, Guid.NewGuid());

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.Notifications.AddAsync(notification, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await using var readContext = fixture.CreateApplicationDbContext();
		var reloaded = await readContext.Notifications.FindAsync(notification.Id, cancellationToken);

		reloaded.Should().NotBeNull();
		reloaded!.Kind.Should().Be(NotificationKind.EngagementCreated);
	}

	[Test]
	public async Task Notification_Kind_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var notification = Notification.Create(UserId.New(), NotificationKind.EngagementCreated, Guid.NewGuid());

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.Notifications.AddAsync(notification, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"notification", "kind", notification.Id.Value, "ck_notification_kind_valid", cancellationToken);
	}

	[Test]
	public async Task OrganizationInvitation_ShouldPersistAndReload_ValidIntendedRoleAndStatus(
		CancellationToken cancellationToken)
	{
		var invitation = OrganizationInvitation.Create(
			DomainOrganizationId.New(), UserId.New(), UserId.New(), OrganizationMemberRole.Organizer, DateTimeOffset.UtcNow);

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.OrganizationInvitations.AddAsync(invitation, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await using var readContext = fixture.CreateApplicationDbContext();
		var reloaded = await readContext.OrganizationInvitations.FindAsync(invitation.Id, cancellationToken);

		reloaded.Should().NotBeNull();
		reloaded!.IntendedRole.Should().Be(OrganizationMemberRole.Organizer);
		reloaded.Status.Should().Be(InvitationStatus.Pending);
	}

	[Test]
	public async Task OrganizationInvitation_IntendedRole_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var invitation = OrganizationInvitation.Create(
			DomainOrganizationId.New(), UserId.New(), UserId.New(), OrganizationMemberRole.Organizer, DateTimeOffset.UtcNow);

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.OrganizationInvitations.AddAsync(invitation, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"organization_invitation", "intended_role", invitation.Id.Value, "ck_organization_invitation_intended_role_valid", cancellationToken);
	}

	[Test]
	public async Task OrganizationInvitation_Status_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var invitation = OrganizationInvitation.Create(
			DomainOrganizationId.New(), UserId.New(), UserId.New(), OrganizationMemberRole.Organizer, DateTimeOffset.UtcNow);

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.OrganizationInvitations.AddAsync(invitation, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"organization_invitation", "status", invitation.Id.Value, "ck_organization_invitation_status_valid", cancellationToken);
	}

	[Test]
	public async Task OrganizationMembership_ShouldPersistAndReload_ValidRole(
		CancellationToken cancellationToken)
	{
		var membership = OrganizationMembership.Create(DomainOrganizationId.New(), UserId.New(), OrganizationMemberRole.Member);

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.OrganizationMemberships.AddAsync(membership, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await using var readContext = fixture.CreateApplicationDbContext();
		var reloaded = await readContext.OrganizationMemberships.FindAsync(membership.Id, cancellationToken);

		reloaded.Should().NotBeNull();
		reloaded!.Role.Should().Be(OrganizationMemberRole.Member);
	}

	[Test]
	public async Task OrganizationMembership_Role_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var membership = OrganizationMembership.Create(DomainOrganizationId.New(), UserId.New(), OrganizationMemberRole.Member);

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.OrganizationMemberships.AddAsync(membership, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"organization_membership", "role", membership.Id.Value, "ck_organization_membership_role_valid", cancellationToken);
	}

	[Test]
	public async Task Report_ShouldPersistAndReload_ValidTargetTypeReasonAndStatus(
		CancellationToken cancellationToken)
	{
		var report = Report.Create(ReportTargetType.User, Guid.NewGuid(), UserId.New(), ReportReason.Spam, "Details").GetValueOrThrow();

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.Reports.AddAsync(report, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await using var readContext = fixture.CreateApplicationDbContext();
		var reloaded = await readContext.Reports.FindAsync(report.Id, cancellationToken);

		reloaded.Should().NotBeNull();
		reloaded!.TargetType.Should().Be(ReportTargetType.User);
		reloaded.Reason.Should().Be(ReportReason.Spam);
		reloaded.Status.Should().Be(ReportStatus.Open);
	}

	[Test]
	public async Task Report_TargetType_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var report = Report.Create(ReportTargetType.User, Guid.NewGuid(), UserId.New(), ReportReason.Spam, "Details").GetValueOrThrow();

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.Reports.AddAsync(report, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"report", "target_type", report.Id.Value, "ck_report_target_type_valid", cancellationToken);
	}

	[Test]
	public async Task Report_Reason_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var report = Report.Create(ReportTargetType.User, Guid.NewGuid(), UserId.New(), ReportReason.Spam, "Details").GetValueOrThrow();

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.Reports.AddAsync(report, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"report", "reason", report.Id.Value, "ck_report_reason_valid", cancellationToken);
	}

	[Test]
	public async Task Report_Status_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var report = Report.Create(ReportTargetType.User, Guid.NewGuid(), UserId.New(), ReportReason.Spam, "Details").GetValueOrThrow();

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.Reports.AddAsync(report, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"report", "status", report.Id.Value, "ck_report_status_valid", cancellationToken);
	}

	[Test]
	public async Task User_ShouldPersistAndReload_ValidPreferredContact(
		CancellationToken cancellationToken)
	{
		var user = User.Create(UserId.New());
		user.SetPreferredContact(PreferredContact.Phone);

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.Users.AddAsync(user, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await using var readContext = fixture.CreateApplicationDbContext();
		var reloaded = await readContext.Users.FindAsync(user.Id, cancellationToken);

		reloaded.Should().NotBeNull();
		reloaded!.PreferredContact.Should().Be(PreferredContact.Phone);
	}

	[Test]
	public async Task User_PreferredContact_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var user = User.Create(UserId.New());

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.Users.AddAsync(user, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"user", "preferred_contact", user.Id.Value, "ck_user_preferred_contact_valid", cancellationToken);
	}

	[Test]
	public async Task VolunteerOpportunity_ShouldPersistAndReload_ValidEnumValues(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateDraftOpportunity();

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await using var readContext = fixture.CreateApplicationDbContext();
		var reloaded = await readContext.VolunteerOpportunities.FindAsync(opportunity.Id, cancellationToken);

		reloaded.Should().NotBeNull();
		reloaded!.Occurrence.Should().Be(Occurrence.OneTime);
		reloaded.ParticipationType.Should().Be(ParticipationType.IndividualContact);
		reloaded.CheckInMethod.Should().Be(CheckInMethod.None);
		reloaded.Category.Should().Be(Category.Social);
		reloaded.Status.Should().Be(OpportunityStatus.Draft);
	}

	[Test]
	public async Task VolunteerOpportunity_Category_ShouldAllowNull(
		CancellationToken cancellationToken)
	{
		var opportunity = VolunteerOpportunity.Create(
			DomainOrganizationId.New(),
			"Title",
			"Description",
			isRemote: true,
			address: null,
			Occurrence.OneTime,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			new RandomPinGenerator(),
			category: null,
			status: OpportunityStatus.Draft).GetValueOrThrow();

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await using var readContext = fixture.CreateApplicationDbContext();
		var reloaded = await readContext.VolunteerOpportunities.FindAsync(opportunity.Id, cancellationToken);

		reloaded.Should().NotBeNull();
		reloaded!.Category.Should().BeNull();
	}

	[Test]
	public async Task VolunteerOpportunity_Occurrence_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateDraftOpportunity();

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"volunteer_opportunity", "occurrence", opportunity.Id.Value, "ck_volunteer_opportunity_occurrence_valid", cancellationToken);
	}

	[Test]
	public async Task VolunteerOpportunity_ParticipationType_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateDraftOpportunity();

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"volunteer_opportunity", "participation_type", opportunity.Id.Value, "ck_volunteer_opportunity_participation_type_valid", cancellationToken);
	}

	[Test]
	public async Task VolunteerOpportunity_CheckInMethod_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateDraftOpportunity();

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"volunteer_opportunity", "check_in_method", opportunity.Id.Value, "ck_volunteer_opportunity_check_in_method_valid", cancellationToken);
	}

	[Test]
	public async Task VolunteerOpportunity_Category_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateDraftOpportunity();

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"volunteer_opportunity", "category", opportunity.Id.Value, "ck_volunteer_opportunity_category_valid", cancellationToken);
	}

	[Test]
	public async Task VolunteerOpportunity_Status_ShouldReject_InvalidValue(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateDraftOpportunity();

		await using var writeContext = fixture.CreateApplicationDbContext();
		await writeContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);
		await writeContext.SaveChangesAsync(cancellationToken);

		await AssertRejectsInvalidValueAsync(
			"volunteer_opportunity", "status", opportunity.Id.Value, "ck_volunteer_opportunity_status_valid", cancellationToken);
	}

	// Draft sidesteps Create's Published-only validation (a deadline, at least one time
	// slot for ScheduledSlots, etc.) - these tests only care about the enum columns.
	private static VolunteerOpportunity CreateDraftOpportunity() =>
		VolunteerOpportunity.Create(
			DomainOrganizationId.New(),
			"Title",
			"Description",
			isRemote: true,
			address: null,
			Occurrence.OneTime,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			new RandomPinGenerator(),
			category: Category.Social,
			status: OpportunityStatus.Draft).GetValueOrThrow();

	// Bypasses the HasConversion<string>() value converter (which would reject an invalid
	// C# enum value before it ever reached the DB) with a raw UPDATE, to prove the
	// CHECK constraint itself - not just the converter - is what's guarding the column.
	private async Task AssertRejectsInvalidValueAsync(
		string table,
		string column,
		Guid id,
		string constraintName,
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		await using var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
		await connection.OpenAsync(cancellationToken);

		// Identifiers are double-quoted since "user" is a reserved Postgres keyword and
		// would otherwise fail with a syntax error (42601), not the check violation (23514)
		// this test is actually after.
		await using var cmd = new NpgsqlCommand(
			$"UPDATE \"{table}\" SET \"{column}\" = 'NotARealValue' WHERE id = @id", connection);
		cmd.Parameters.AddWithValue("id", id);

		var act = async () => await cmd.ExecuteNonQueryAsync(cancellationToken);

		var exception = await act.Should().ThrowAsync<PostgresException>();
		exception.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
		exception.Which.ConstraintName.Should().Be(constraintName);
	}
}
