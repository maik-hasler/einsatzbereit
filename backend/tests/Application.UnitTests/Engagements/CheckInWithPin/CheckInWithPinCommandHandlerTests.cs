using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Common.RateLimiting;
using Application.Engagements.CheckInWithPin.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.CheckInWithPin;

public class CheckInWithPinCommandHandlerTests
{
	private const string CorrectPin = "482170";

	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly ICheckInAttemptLimiter _attemptLimiter = Substitute.For<ICheckInAttemptLimiter>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly CheckInWithPinCommandHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly Address DefaultAddress = Address.Create("Teststrasse", "1", "12345", "Berlin").Value;

	public CheckInWithPinCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_sut = new CheckInWithPinCommandHandler(_dbContext, _attemptLimiter);
	}

	[Test]
	public async Task Handle_ShouldThrowNotOwner_BeforeComparingPin_WhenNonOwnerGuessesWrongPin(
		CancellationToken cancellationToken)
	{
		// Regression for #806: a non-owner must get the same "not owner" failure
		// regardless of whether the guessed PIN happens to be correct or wrong -
		// otherwise the response distinguishes valid from invalid PINs (an oracle).
		var opportunityId = VolunteerOpportunityId.New();
		var engagementId = EngagementId.New();
		var owner = UserId.New();
		var attacker = UserId.New();

		var engagement = Engagement.CreateSlotSignUp(opportunityId, owner, TimeSlotId.New());
		engagement.Confirm();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new CheckInWithPinCommand(engagementId, "000000", attacker);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*own engagement*");
		await _opportunityRepo.DidNotReceive().FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>());
		await _attemptLimiter.DidNotReceive().RegisterFailedAttemptAsync(Arg.Any<EngagementId>(), Arg.Any<CancellationToken>());
	}

	// Regression for #1217: the ownership check below runs before CheckIn()'s
	// own IsAnonymized guard (#1140), so it used to dereference the null
	// VolunteerId directly and crash with a 500 instead of returning a 409.
	[Test]
	public async Task Handle_ShouldThrowConflict_WhenEngagementIsAnonymized(
		CancellationToken cancellationToken)
	{
		var opportunityId = VolunteerOpportunityId.New();
		var engagementId = EngagementId.New();
		var engagement = Engagement.CreateSlotSignUp(opportunityId, UserId.New(), TimeSlotId.New());
		engagement.Confirm();
		engagement.Anonymize();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new CheckInWithPinCommand(engagementId, CorrectPin, UserId.New());

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
		await _opportunityRepo.DidNotReceive().FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrowCheckInLocked_BeforeComparingPin_WhenEngagementIsLockedOut(
		CancellationToken cancellationToken)
	{
		var opportunityId = VolunteerOpportunityId.New();
		var engagementId = EngagementId.New();
		var owner = UserId.New();

		var engagement = Engagement.CreateSlotSignUp(opportunityId, owner, TimeSlotId.New());
		engagement.Confirm();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_attemptLimiter.IsLockedOutAsync(engagementId, cancellationToken).Returns(true);

		var command = new CheckInWithPinCommand(engagementId, CorrectPin, owner);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*Too many failed*");
		await _opportunityRepo.DidNotReceive().FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldRegisterFailedAttempt_WhenOwnerSubmitsWrongPin(
		CancellationToken cancellationToken)
	{
		var engagementId = EngagementId.New();
		var owner = UserId.New();
		var opportunity = CreatePinOpportunity();

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, owner, TimeSlotId.New());
		engagement.Confirm();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var command = new CheckInWithPinCommand(engagementId, "000000", owner);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*Invalid PIN*");
		await _attemptLimiter.Received(1).RegisterFailedAttemptAsync(engagementId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldResetAttempts_AndCheckIn_WhenOwnerSubmitsCorrectPin(
		CancellationToken cancellationToken)
	{
		var engagementId = EngagementId.New();
		var owner = UserId.New();
		var opportunity = CreatePinOpportunity();

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, owner, TimeSlotId.New());
		engagement.Confirm();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var command = new CheckInWithPinCommand(engagementId, CorrectPin, owner);

		var result = await _sut.Handle(command, cancellationToken);

		result.IsCheckedIn.Should().BeTrue();
		await _attemptLimiter.Received(1).ResetAsync(engagementId, cancellationToken);
		await _attemptLimiter.DidNotReceive().RegisterFailedAttemptAsync(Arg.Any<EngagementId>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementNotFound(
		CancellationToken cancellationToken)
	{
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

		var command = new CheckInWithPinCommand(engagementId, CorrectPin, UserId.New());

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage($"*{engagementId.Value}*");
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task Handle_ShouldThrowValidation_WhenPinIsNullOrWhitespace_OnNonPinOpportunity(
		string? submittedPin,
		CancellationToken cancellationToken)
	{
		// Regression for #1139: a "None"/"QRCode"/"Manual" opportunity never sets
		// CheckInPin, so it stays null. Before this fix, submitting an empty body
		// (deserializing Pin as null) made `opportunity.CheckInPin != request.Pin`
		// compare null to null and pass, checking the volunteer in without any PIN.
		var engagementId = EngagementId.New();
		var owner = UserId.New();
		var opportunity = CreateNonPinOpportunity(CheckInMethod.None);

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, owner, TimeSlotId.New());
		engagement.Confirm();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var command = new CheckInWithPinCommand(engagementId, submittedPin!, owner);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*does not use PIN check-in*");
		await _attemptLimiter.DidNotReceive().RegisterFailedAttemptAsync(Arg.Any<EngagementId>(), Arg.Any<CancellationToken>());
	}

	[Test]
	[Arguments(CheckInMethod.QRCode)]
	[Arguments(CheckInMethod.Manual)]
	public async Task Handle_ShouldThrowConflict_WhenOpportunityDoesNotUsePinCheckIn(
		CheckInMethod checkInMethod,
		CancellationToken cancellationToken)
	{
		var engagementId = EngagementId.New();
		var owner = UserId.New();
		var opportunity = CreateNonPinOpportunity(checkInMethod);

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, owner, TimeSlotId.New());
		engagement.Confirm();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var command = new CheckInWithPinCommand(engagementId, CorrectPin, owner);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*does not use PIN check-in*");
	}

	[Test]
	public async Task Handle_ShouldThrowValidation_WhenPinIsNullOrWhitespace_OnPinOpportunity(
		CancellationToken cancellationToken)
	{
		var engagementId = EngagementId.New();
		var owner = UserId.New();
		var opportunity = CreatePinOpportunity();

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, owner, TimeSlotId.New());
		engagement.Confirm();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var command = new CheckInWithPinCommand(engagementId, "", owner);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*PIN is required*");
		await _attemptLimiter.DidNotReceive().RegisterFailedAttemptAsync(Arg.Any<EngagementId>(), Arg.Any<CancellationToken>());
	}

	private VolunteerOpportunity CreateNonPinOpportunity(CheckInMethod checkInMethod) =>
		VolunteerOpportunity.Create(
			DefaultOrgId,
			"Test",
			null,
			"Test",
			null,
			false,
			DefaultAddress,
			Occurrence.OneTime,
			ParticipationType.ScheduledSlots,
			checkInMethod,
			_pinGenerator,
			status: OpportunityStatus.Draft).Value;

	private VolunteerOpportunity CreatePinOpportunity() =>
		VolunteerOpportunity.Create(
			DefaultOrgId,
			"Test",
			null,
			"Test",
			null,
			false,
			DefaultAddress,
			Occurrence.OneTime,
			ParticipationType.ScheduledSlots,
			CheckInMethod.PINCode,
			_pinGenerator,
			status: OpportunityStatus.Draft,
			checkInPin: CorrectPin).Value;
}
