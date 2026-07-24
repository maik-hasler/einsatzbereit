using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements.SubmitFeedback.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.SubmitFeedback;

public class SubmitFeedbackCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly SubmitFeedbackCommandHandler _sut;

	public SubmitFeedbackCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_sut = new SubmitFeedbackCommandHandler(_dbContext);
	}

	private static (Engagement engagement, UserId volunteerId) CreateCheckedInEngagementWithVolunteer()
	{
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateWaitlistSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());
		engagement.Confirm();
		engagement.CheckIn();
		return (engagement, volunteerId);
	}

	[Test]
	public async Task Handle_ShouldSubmitFeedback_WhenCalledByOwnerWithValidRating(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreateCheckedInEngagementWithVolunteer();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new SubmitFeedbackCommand(engagementId, volunteerId, 4, "Great experience");

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		engagement.FeedbackRating.Should().Be(4);
		engagement.FeedbackComment.Should().Be("Great experience");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

		var command = new SubmitFeedbackCommand(engagementId, UserId.New(), 4, null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{engagementId.Value}*"))
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenCallerIsNotTheEngagementOwner(
		CancellationToken cancellationToken)
	{
		// Arrange: a different volunteer attempts to submit feedback for someone else's engagement.
		var (engagement, _) = CreateCheckedInEngagementWithVolunteer();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new SubmitFeedbackCommand(engagementId, UserId.New(), 4, "Great experience");

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*You can only submit feedback for your own engagements*"))
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
	}

	[Test]
	public async Task Handle_ShouldNotMutateEngagement_WhenCallerIsNotTheEngagementOwner(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, _) = CreateCheckedInEngagementWithVolunteer();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new SubmitFeedbackCommand(engagementId, UserId.New(), 4, "Great experience");

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);
		await act.Should().ThrowAsync<ResultFailureException>();

		// Assert: the ownership guard fires before the domain method runs, so feedback stays unset.
		engagement.FeedbackRating.Should().BeNull();
		engagement.FeedbackSubmittedAt.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsNotCheckedIn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateWaitlistSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());
		engagement.Confirm();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new SubmitFeedbackCommand(engagementId, volunteerId, 4, null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*checked-in*"))
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenFeedbackAlreadySubmitted(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreateCheckedInEngagementWithVolunteer();
		engagement.SubmitFeedback(3, "Already rated", DateTimeOffset.UtcNow);
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new SubmitFeedbackCommand(engagementId, volunteerId, 5, "Trying again");

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*already been submitted*"))
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
	}

	[Test]
	[Arguments(0)]
	[Arguments(-1)]
	[Arguments(6)]
	[Arguments(100)]
	public async Task Handle_ShouldThrow_WhenRatingIsOutOfRange(
		int invalidRating, CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreateCheckedInEngagementWithVolunteer();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new SubmitFeedbackCommand(engagementId, volunteerId, invalidRating, null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Rating must be between 1 and 5*"))
			.Which.Error.Type.Should().Be(ErrorType.Validation);
	}

	[Test]
	[Arguments(1)]
	[Arguments(5)]
	public async Task Handle_ShouldSucceed_WhenRatingIsAtBoundary(
		int boundaryRating, CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreateCheckedInEngagementWithVolunteer();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new SubmitFeedbackCommand(engagementId, volunteerId, boundaryRating, null);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		engagement.FeedbackRating.Should().Be(boundaryRating);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenCommentExceedsMaxLength(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreateCheckedInEngagementWithVolunteer();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var tooLongComment = new string('a', 501);
		var command = new SubmitFeedbackCommand(engagementId, volunteerId, 4, tooLongComment);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*must not exceed 500 characters*"))
			.Which.Error.Type.Should().Be(ErrorType.Validation);
	}
}
