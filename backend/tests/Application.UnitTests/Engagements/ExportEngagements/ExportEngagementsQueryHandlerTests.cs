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
		// Defaults the requesting organizer to English so every test not
		// specifically about localization keeps asserting the English labels
		// below - Handle_ShouldLocalizeHeaderStatusAndLabels_... below is the
		// one exercising the German branch.
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				var requestingUser = User.Create(((IReadOnlyCollection<UserId>)call[0]!).First());
				requestingUser.SetPreferredLanguage("en");
				return new List<User> { requestingUser };
			});
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
	public async Task Handle_ShouldReturnSepDirectiveAndHeaderOnly_WhenNoEngagements(
		CancellationToken cancellationToken)
	{
		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		var file = await _sut.Handle(query, cancellationToken);

		// The leading "sep=;" line (#1675) tells Excel to use ";" as the column
		// delimiter regardless of the running install's own regional settings.
		file.Content.Should().Be(
			"sep=;\r\nName;Status;Time Slot (Europe/Berlin);Check-in Status;Feedback Rating\r\n");
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

		// 09:00-12:00 UTC on an August date is 11:00-14:00 in Europe/Berlin (CEST, UTC+2).
		file.Content.Should().Contain("Vera Volunteer;Confirmed;2026-08-10 11:00 - 2026-08-10 14:00;Checked in;5");
	}

	[Test]
	public async Task Handle_ShouldConvertTimeSlotStart_FromUtcToEuropeBerlin_NotLeaveItRawUtc(
		CancellationToken cancellationToken)
	{
		// A winter instant makes Europe/Berlin deterministically UTC+1 (CET, no
		// DST) regardless of when this test runs - mirrors
		// EngagementReminderDueHandlerTests' own approach to the same fallback.
		var start = new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.Zero);
		var engagement = new EngagementSummary(
			Guid.NewGuid(),
			DefaultOpportunityId.Value,
			"Test Opportunity",
			DefaultOrgId.Value,
			"Test Org",
			VolunteerId: null,
			TimeSlotId: null,
			Message: null,
			"Confirmed",
			IsCheckedIn: false,
			HasFeedback: false,
			DateTimeOffset.UtcNow,
			TimeSlotStartDateTime: start,
			TimeSlotEndDateTime: null);

		_readRepository
			.GetForExportAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns([engagement]);

		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		var file = await _sut.Handle(query, cancellationToken);

		file.Content.Should().Contain("2027-01-15 13:00").And.NotContain("12:00");
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

		file.Content.Should().Contain("vera;Pending");
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

		file.Content.Should().Contain("Anonymized volunteer;Cancelled");
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

		file.Content.Should().Contain("Anonymized volunteer;Pending;;Not checked in;\r\n");
	}

	[Test]
	public async Task Handle_ShouldQuoteAndEscapeName_WhenItContainsAQuote(
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

		// The embedded quote still forces quoting/escaping - the embedded comma
		// rides along inside those quotes but, unlike before #1675, no longer
		// needs escaping on its own since ";" (not ",") is now the delimiter.
		file.Content.Should().Contain("\"Vera \"\"The Helper\"\" Doe, Jr.\";Pending");
	}

	[Test]
	public async Task Handle_ShouldNotEscapeAPlainComma_SinceSemicolonIsTheDelimiterNow(
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
				[volunteerId] = new KeycloakUserProfile(volunteerId, "vera", "Doe, Jane", null, "vera@example.com"),
			});

		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		var file = await _sut.Handle(query, cancellationToken);

		file.Content.Should().Contain("Doe, Jane;Pending").And.NotContain("\"Doe, Jane\"");
	}

	[Test]
	public async Task Handle_ShouldQuoteAndEscapeName_WhenItContainsTheSemicolonDelimiter(
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
				[volunteerId] = new KeycloakUserProfile(volunteerId, "vera", "Vera; The Helper", null, "vera@example.com"),
			});

		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		var file = await _sut.Handle(query, cancellationToken);

		file.Content.Should().Contain("\"Vera; The Helper\";Pending");
	}

	[Test]
	public async Task Handle_ShouldLocalizeHeaderStatusAndLabels_InRequestingOrganizersPreferredLanguage(
		CancellationToken cancellationToken)
	{
		var organizer = User.Create(DefaultRequestingUserId);
		organizer.SetPreferredLanguage("de");
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([organizer]);

		var engagement = new EngagementSummary(
			Guid.NewGuid(),
			DefaultOpportunityId.Value,
			"Test Opportunity",
			DefaultOrgId.Value,
			"Test Org",
			VolunteerId: null,
			null,
			null,
			"Confirmed",
			IsCheckedIn: true,
			HasFeedback: false,
			DateTimeOffset.UtcNow);

		_readRepository
			.GetForExportAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns([engagement]);

		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		var file = await _sut.Handle(query, cancellationToken);

		file.Content.Should().StartWith(
			"sep=;\r\nName;Status;Zeitslot (Europe/Berlin);Check-in-Status;Feedback-Bewertung\r\n");
		file.Content.Should().Contain("Anonymisierte:r Freiwillige:r;Bestätigt;;Eingecheckt;\r\n");
	}

	[Test]
	[Arguments("=SUM(A1:A9)")]
	[Arguments("+1234567")]
	[Arguments("-1234567")]
	[Arguments("@example")]
	public async Task Handle_ShouldPrefixNameWithASingleQuote_WhenItStartsWithAFormulaTriggerCharacter(
		string maliciousFirstName,
		CancellationToken cancellationToken)
	{
		// Arrange - CWE-1236: a spreadsheet app treats a cell starting with any of
		// these characters as a formula to evaluate; the Name column comes straight
		// from a volunteer's caller-controlled Keycloak first/last name (#1678).
		// Tab/CR aren't exercisable through this column specifically - ResolveName's
		// own Trim() (below) strips a leading tab or CR as whitespace before
		// CsvEscape ever sees it - but CsvEscape still neutralizes them for any
		// other column a future caller-controlled field might add.
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
				[volunteerId] = new KeycloakUserProfile(volunteerId, "vera", maliciousFirstName, null, "vera@example.com"),
			});

		var query = new ExportEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId);

		// Act
		var file = await _sut.Handle(query, cancellationToken);

		// Assert
		file.Content.Should().Contain($"'{maliciousFirstName}");
	}

	private static VolunteerOpportunity CreateDefaultOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Test", "Test", false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, Substitute.For<IPinGenerator>(), status: OpportunityStatus.Draft).Value;
}
