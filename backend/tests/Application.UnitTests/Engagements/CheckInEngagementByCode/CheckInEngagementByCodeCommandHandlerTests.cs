using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements.CheckInEngagementByCode.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.CheckInEngagementByCode;

public class CheckInEngagementByCodeCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly CheckInEngagementByCodeCommandHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public CheckInEngagementByCodeCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new CheckInEngagementByCodeCommandHandler(_dbContext);
	}

	private VolunteerOpportunity CreateOpportunity(CheckInMethod checkInMethod = CheckInMethod.QRCode) =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", null, "Beschreibung", null, true, null, Occurrence.OneTime, ParticipationType.ScheduledSlots, checkInMethod, _pinGenerator,
			status: OpportunityStatus.Draft).Value;

	private static Engagement CreateConfirmedEngagement(VolunteerOpportunityId opportunityId)
	{
		var engagement = Engagement.CreateSlotSignUp(opportunityId, UserId.New(), TimeSlotId.New());
		engagement.Confirm();
		return engagement;
	}

	private static string CodeFor(Engagement engagement) =>
		engagement.Id.Value.ToString()[..8];

	private void SetUpOpportunity(VolunteerOpportunity opportunity) =>
		_opportunityRepo.FindAsync(opportunity.Id, Arg.Any<CancellationToken>()).Returns(opportunity);

	private void SetUpCandidates(VolunteerOpportunityId opportunityId, params Engagement[] engagements) =>
		_dbContext
			.GetActiveEngagementsForOpportunityAsync(opportunityId, Arg.Any<CancellationToken>())
			.Returns(engagements.ToList());

	[Test]
	public async Task Handle_ShouldCheckInEngagement_WhenCodeMatchesExactlyOneActiveEngagement(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		var engagement = CreateConfirmedEngagement(opportunity.Id);
		SetUpOpportunity(opportunity);
		SetUpCandidates(opportunity.Id, engagement);

		var command = new CheckInEngagementByCodeCommand(opportunity.Id, CodeFor(engagement), DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Id.Should().Be(engagement.Id);
		result.IsCheckedIn.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldMatchCodeCaseInsensitively(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		var engagement = CreateConfirmedEngagement(opportunity.Id);
		SetUpOpportunity(opportunity);
		SetUpCandidates(opportunity.Id, engagement);

		var command = new CheckInEngagementByCodeCommand(
			opportunity.Id, CodeFor(engagement).ToUpperInvariant(), DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.IsCheckedIn.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldThrowForbidden_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		var engagement = CreateConfirmedEngagement(opportunity.Id);
		SetUpOpportunity(opportunity);
		SetUpCandidates(opportunity.Id, engagement);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var command = new CheckInEngagementByCodeCommand(opportunity.Id, CodeFor(engagement), DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		engagement.IsCheckedIn.Should().BeFalse();
	}

	[Test]
	public async Task Handle_ShouldThrowNotFound_WhenOpportunityIsGone(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		_opportunityRepo.FindAsync(opportunityId, Arg.Any<CancellationToken>()).Returns((VolunteerOpportunity?)null);

		var command = new CheckInEngagementByCodeCommand(opportunityId, "abcd1234", DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
		await _dbContext
			.DidNotReceive()
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrowConflict_WhenOpportunityDoesNotUseQrCodeCheckIn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity(CheckInMethod.PINCode);
		SetUpOpportunity(opportunity);

		var command = new CheckInEngagementByCodeCommand(opportunity.Id, "abcd1234", DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Should().Be(Error.Conflict("Engagement.CheckInMethodNotQrCode", "This opportunity does not use QR code check-in."));
	}

	[Test]
	[Arguments("")]
	[Arguments("abcd123")]
	[Arguments("abcd12345")]
	[Arguments("abcd123z")]
	public async Task Handle_ShouldThrowValidation_WhenCodeIsNotEightHexCharacters(
		string invalidCode, CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		SetUpOpportunity(opportunity);

		var command = new CheckInEngagementByCodeCommand(opportunity.Id, invalidCode, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Validation);
	}

	[Test]
	public async Task Handle_ShouldThrowNotFound_WhenNoActiveEngagementMatchesCode(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		var engagement = CreateConfirmedEngagement(opportunity.Id);
		SetUpOpportunity(opportunity);
		SetUpCandidates(opportunity.Id, engagement);

		// A code that isn't a real hex prefix of any Guid this engagement could
		// have (the digits d-e-a-d never appear together in a UUIDv7's
		// timestamp-derived first segment often enough to risk a false match
		// here, and even if they did the assertion below would just fail loudly).
		var command = new CheckInEngagementByCodeCommand(opportunity.Id, "deadbeef", DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
		engagement.IsCheckedIn.Should().BeFalse();
	}

	[Test]
	public async Task Handle_ShouldThrowConflict_WhenMultipleActiveEngagementsMatchCode(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		var engagement = CreateConfirmedEngagement(opportunity.Id);
		SetUpOpportunity(opportunity);
		// Stands in for two different volunteers whose engagement ids happen to
		// share their first 8 hex characters - expected under UUIDv7 (see the
		// comment in CheckInEngagementByCodeCommandHandler), not a data bug.
		SetUpCandidates(opportunity.Id, engagement, engagement);

		var command = new CheckInEngagementByCodeCommand(opportunity.Id, CodeFor(engagement), DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Should().Be(Error.Conflict(
				"Engagement.CheckInCodeAmbiguous", "Multiple sign-ups match this code. Use the QR scanner instead."));
		engagement.IsCheckedIn.Should().BeFalse();
	}

	[Test]
	public async Task Handle_ShouldThrowNotFound_WhenTheOnlyMatchIsNotYetConfirmed(
		CancellationToken cancellationToken)
	{
		// Arrange: a Pending engagement can never pass CheckIn(), and its code
		// is never shown to its own volunteer either (CheckInModal only
		// renders the QR/fallback code once an engagement is Confirmed) - the
		// only way this code is ever tried in practice is a Pending sign-up
		// coincidentally sharing a Confirmed one's UUIDv7-derived prefix.
		// Excluding Pending from the candidate pool up front (alongside
		// Handle_ShouldCheckInEngagement_WhenCodeMatchesExactlyOneActiveEngagement
		// proving a Confirmed match still succeeds) is what lets that
		// Confirmed sibling be found instead of the pair being reported as
		// ambiguous. This test pins the resulting behaviour for a solitary
		// Pending match on its own: not found, not the domain's "not
		// confirmed" error.
		var opportunity = CreateOpportunity();
		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, UserId.New(), TimeSlotId.New());
		SetUpOpportunity(opportunity);
		SetUpCandidates(opportunity.Id, engagement);

		var command = new CheckInEngagementByCodeCommand(opportunity.Id, CodeFor(engagement), DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
		engagement.IsCheckedIn.Should().BeFalse();
	}

	[Test]
	public async Task Handle_ShouldPropagateDomainError_WhenTheConfirmedMatchIsAlreadyCheckedIn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		var engagement = CreateConfirmedEngagement(opportunity.Id);
		engagement.CheckIn();
		SetUpOpportunity(opportunity);
		SetUpCandidates(opportunity.Id, engagement);

		var command = new CheckInEngagementByCodeCommand(opportunity.Id, CodeFor(engagement), DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Code.Should().Be("Engagement.AlreadyCheckedIn");
	}
}
