using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements;
using Application.Engagements.ExportEngagements.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.ExportEngagements;

public class ExportEngagementsQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IEngagementReadRepository _readRepository = Substitute.For<IEngagementReadRepository>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly ExportEngagementsQueryHandler _sut;

	private static readonly UserId DefaultRequestingUserId = UserId.New();
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly Address DefaultAddress = Address.Create("Teststraße", "1", "12345", "Berlin").Value;
	private static readonly VolunteerOpportunityId DefaultOpportunityId = VolunteerOpportunityId.New();

	public ExportEngagementsQueryHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(CreateDefaultOpportunity());
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_readRepository
			.GetForExportAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_keycloakUserService
			.GetUserProfilesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, KeycloakUserProfile>());
		_sut = new ExportEngagementsQueryHandler(_readRepository, _dbContext, _keycloakUserService);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns((VolunteerOpportunity?)null);

		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
	}

	[Test]
	public async Task Handle_ShouldNameFileAfterOpportunityId(
		CancellationToken cancellationToken)
	{
		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		var file = await _sut.Handle(query, cancellationToken);

		file.FileName.Should().Be($"engagements-{DefaultOpportunityId.Value}.csv");
	}

	[Test]
	public async Task Handle_ShouldReturnHeaderOnly_WhenNoEngagements(
		CancellationToken cancellationToken)
	{
		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		var file = await _sut.Handle(query, cancellationToken);

		file.Content.Should().Be("Name,Status,Time Slot,Check-in Status,Feedback Rating\r\n");
	}

	[Test]
	public async Task Handle_ShouldIncludeResolvedName_TimeSlot_CheckIn_AndFeedbackRating_ForACompleteRow(
		CancellationToken cancellationToken)
	{
		var volunteerId = Guid.NewGuid();
		var start = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
		var end = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
		var engagement = new EngagementSummary(
			Guid.NewGuid(),
			DefaultOpportunityId.Value,
			"Test Opportunity",
			DefaultOrgId.Value,
			"Test Org",
			volunteerId,
			Guid.NewGuid(),
			null,
			"Confirmed",
			IsCheckedIn: true,
			HasFeedback: true,
			DateTimeOffset.UtcNow,
			TimeSlotStartDateTime: start,
			TimeSlotEndDateTime: end,
			FeedbackRating: 5);

		_readRepository
			.GetForExportAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns([engagement]);
		_keycloakUserService
			.GetUserProfilesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, KeycloakUserProfile>
			{
				[volunteerId] = new KeycloakUserProfile(volunteerId, "vera", "Vera", "Volunteer", "vera@example.com"),
			});

		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		var file = await _sut.Handle(query, cancellationToken);

		file.Content.Should().Contain("Vera Volunteer,Confirmed,2026-08-10 09:00 - 2026-08-10 12:00 UTC,Checked in,5");
	}

	[Test]
	public async Task Handle_ShouldFallBackToUsername_WhenKeycloakProfileHasNoName(
		CancellationToken cancellationToken)
	{
		var volunteerId = Guid.NewGuid();
		var engagement = new EngagementSummary(
			Guid.NewGuid(),
			DefaultOpportunityId.Value,
			"Test Opportunity",
			DefaultOrgId.Value,
			"Test Org",
			volunteerId,
			null,
			null,
			"Pending",
			IsCheckedIn: false,
			HasFeedback: false,
			DateTimeOffset.UtcNow);

		_readRepository
			.GetForExportAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns([engagement]);
		_keycloakUserService
			.GetUserProfilesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, KeycloakUserProfile>
			{
				[volunteerId] = new KeycloakUserProfile(volunteerId, "vera", null, null, "vera@example.com"),
			});

		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		var file = await _sut.Handle(query, cancellationToken);

		file.Content.Should().Contain("vera,Pending");
	}

	[Test]
	public async Task Handle_ShouldFallBackToVolunteerId_WhenKeycloakLookupFailsForVolunteer(
		CancellationToken cancellationToken)
	{
		var volunteerId = Guid.NewGuid();
		var engagement = new EngagementSummary(
			Guid.NewGuid(),
			DefaultOpportunityId.Value,
			"Test Opportunity",
			DefaultOrgId.Value,
			"Test Org",
			volunteerId,
			null,
			null,
			"Pending",
			IsCheckedIn: false,
			HasFeedback: false,
			DateTimeOffset.UtcNow);

		_readRepository
			.GetForExportAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns([engagement]);

		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		var file = await _sut.Handle(query, cancellationToken);

		file.Content.Should().Contain($"Volunteer {volunteerId}");
	}

	[Test]
	public async Task Handle_ShouldUseAnonymizedVolunteerLabel_WhenVolunteerIdIsNull(
		CancellationToken cancellationToken)
	{
		var engagement = new EngagementSummary(
			Guid.NewGuid(),
			DefaultOpportunityId.Value,
			"Test Opportunity",
			DefaultOrgId.Value,
			"Test Org",
			VolunteerId: null,
			null,
			null,
			"Cancelled",
			IsCheckedIn: false,
			HasFeedback: false,
			DateTimeOffset.UtcNow);

		_readRepository
			.GetForExportAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns([engagement]);

		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		var file = await _sut.Handle(query, cancellationToken);

		file.Content.Should().Contain("Anonymized volunteer,Cancelled");
	}

	[Test]
	public async Task Handle_ShouldLeaveTimeSlotBlank_AndNotCheckedIn_AndBlankFeedbackRating_ForIndividualContactWithNoFeedback(
		CancellationToken cancellationToken)
	{
		var engagement = new EngagementSummary(
			Guid.NewGuid(),
			DefaultOpportunityId.Value,
			"Test Opportunity",
			DefaultOrgId.Value,
			"Test Org",
			VolunteerId: null,
			TimeSlotId: null,
			Message: "Please let me help",
			"Pending",
			IsCheckedIn: false,
			HasFeedback: false,
			DateTimeOffset.UtcNow);

		_readRepository
			.GetForExportAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns([engagement]);

		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		var file = await _sut.Handle(query, cancellationToken);

		file.Content.Should().Contain("Anonymized volunteer,Pending,,Not checked in,\r\n");
	}

	[Test]
	public async Task Handle_ShouldQuoteAndEscapeName_WhenItContainsACommaAndQuote(
		CancellationToken cancellationToken)
	{
		var volunteerId = Guid.NewGuid();
		var engagement = new EngagementSummary(
			Guid.NewGuid(),
			DefaultOpportunityId.Value,
			"Test Opportunity",
			DefaultOrgId.Value,
			"Test Org",
			volunteerId,
			null,
			null,
			"Pending",
			IsCheckedIn: false,
			HasFeedback: false,
			DateTimeOffset.UtcNow);

		_readRepository
			.GetForExportAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns([engagement]);
		_keycloakUserService
			.GetUserProfilesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, KeycloakUserProfile>
			{
				[volunteerId] = new KeycloakUserProfile(volunteerId, "vera", "Vera \"The Helper\"", "Doe, Jr.", "vera@example.com"),
			});

		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		var file = await _sut.Handle(query, cancellationToken);

		file.Content.Should().Contain("\"Vera \"\"The Helper\"\" Doe, Jr.\",Pending");
	}

	private static VolunteerOpportunity CreateDefaultOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Test", "Test", false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, Substitute.For<IPinGenerator>(), status: OpportunityStatus.Draft).Value;
}
