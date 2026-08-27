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
		_sut = new CheckInWithPinCommandHandler(_dbContext, _attemptLimiter, _pinGenerator);
	}

	[Test]
	public async Task Handle_ShouldThrowNotOwner_BeforeComparingPin_WhenNonOwnerGuessesWrongPin(
		CancellationToken cancellationToken)
	{
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
		await _attemptLimiter.DidNotReceive().RegisterFailedAttemptAsync(Arg.Any<UserId>(), Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>());
	}

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
		_attemptLimiter.IsLockedOutAsync(owner, opportunityId, cancellationToken).Returns(true);

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
		await _attemptLimiter.Received(1).RegisterFailedAttemptAsync(owner, opportunity.Id, cancellationToken);
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
		await _attemptLimiter.Received(1).ResetAsync(owner, opportunity.Id, cancellationToken);
		await _attemptLimiter.DidNotReceive().RegisterFailedAttemptAsync(Arg.Any<UserId>(), Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>());
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
		await _attemptLimiter.DidNotReceive().RegisterFailedAttemptAsync(Arg.Any<UserId>(), Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>());
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
		await _attemptLimiter.DidNotReceive().RegisterFailedAttemptAsync(Arg.Any<UserId>(), Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldRotatePin_WhenSubmittedAgainstAnOccurrenceThatHasMovedOn(
		CancellationToken cancellationToken)
	{
		var engagementId = EngagementId.New();
		var owner = UserId.New();
		var opportunity = CreatePinOpportunity();
		var now = DateTimeOffset.UtcNow;

		// Ties CorrectPin to a slot that has already ended - standing in for a
		// volunteer who attended that occurrence and still remembers this PIN.
		opportunity.AddTimeSlot(now.AddDays(-10), now.AddDays(-10).AddHours(2), 10, now.AddDays(-11));
		_pinGenerator.GeneratePin().Returns(CorrectPin);
		opportunity.EnsureCurrentCheckInPin(now.AddDays(-10), _pinGenerator);

		var futureSlot = opportunity.AddTimeSlot(now.AddDays(1), now.AddDays(1).AddHours(2), 10, now).Value;
		_pinGenerator.GeneratePin().Returns("111222");

		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, owner, futureSlot.Id, futureSlot.StartDateTime, futureSlot.EndDateTime);
		engagement.Confirm();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var command = new CheckInWithPinCommand(engagementId, CorrectPin, owner);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*Invalid PIN*");
		opportunity.CheckInPinTimeSlotId.Should().Be(futureSlot.Id);
		opportunity.CheckInPin.Should().Be("111222");
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
