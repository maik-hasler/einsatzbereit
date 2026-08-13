using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements.UpdateFeedback.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.UpdateFeedback;

public class UpdateFeedbackCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly UpdateFeedbackCommandHandler _sut;

	public UpdateFeedbackCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_sut = new UpdateFeedbackCommandHandler(_dbContext);
	}

	private static (Engagement engagement, UserId volunteerId) CreateEngagementWithFeedback(
		int rating = 3, string? comment = "Okay", DateTimeOffset? submittedAt = null)
	{
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());
		engagement.Confirm();
		engagement.CheckIn();
		engagement.SubmitFeedback(rating, comment, submittedAt ?? DateTimeOffset.UtcNow.AddDays(-1));
		return (engagement, volunteerId);
	}

	[Test]
	public async Task Handle_ShouldUpdateFeedback_WhenCalledByOwnerWithValidRating(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreateEngagementWithFeedback();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new UpdateFeedbackCommand(engagementId, volunteerId, 5, "Actually, great!");

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		engagement.FeedbackRating.Should().Be(5);
		engagement.FeedbackComment.Should().Be("Actually, great!");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

		var command = new UpdateFeedbackCommand(engagementId, UserId.New(), 4, null);

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
		// Arrange
		var (engagement, _) = CreateEngagementWithFeedback();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new UpdateFeedbackCommand(engagementId, UserId.New(), 5, "Hijacked");

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*You can only update feedback for your own engagements*"))
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
	}

	[Test]
	public async Task Handle_ShouldNotMutateEngagement_WhenCallerIsNotTheEngagementOwner(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, _) = CreateEngagementWithFeedback(rating: 3, comment: "Okay");
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new UpdateFeedbackCommand(engagementId, UserId.New(), 5, "Hijacked");

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);
		await act.Should().ThrowAsync<ResultFailureException>();

		// Assert: the ownership guard fires before the domain method runs.
		engagement.FeedbackRating.Should().Be(3);
		engagement.FeedbackComment.Should().Be("Okay");
	}

	[Test]
	public async Task Handle_ShouldThrowConflict_WhenEngagementIsAnonymized(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, _) = CreateEngagementWithFeedback();
		engagement.Anonymize();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new UpdateFeedbackCommand(engagementId, UserId.New(), 5, "Great!");

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenFeedbackNotYetSubmitted(
		CancellationToken cancellationToken)
	{
		// Arrange: checked in, but never submitted feedback.
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());
		engagement.Confirm();
		engagement.CheckIn();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new UpdateFeedbackCommand(engagementId, volunteerId, 5, "Great!");

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*not been submitted*"))
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEditWindowExpired(
		CancellationToken cancellationToken)
	{
		// Arrange
		var submittedAt = DateTimeOffset.UtcNow.AddDays(-(Engagement.FeedbackEditWindowDays + 1));
		var (engagement, volunteerId) = CreateEngagementWithFeedback(submittedAt: submittedAt);
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new UpdateFeedbackCommand(engagementId, volunteerId, 5, "Too late");

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*no longer be edited*"))
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
		var (engagement, volunteerId) = CreateEngagementWithFeedback();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new UpdateFeedbackCommand(engagementId, volunteerId, invalidRating, null);

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
		var (engagement, volunteerId) = CreateEngagementWithFeedback();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new UpdateFeedbackCommand(engagementId, volunteerId, boundaryRating, null);

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
		var (engagement, volunteerId) = CreateEngagementWithFeedback();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var tooLongComment = new string('a', 501);
		var command = new UpdateFeedbackCommand(engagementId, volunteerId, 4, tooLongComment);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*must not exceed 500 characters*"))
			.Which.Error.Type.Should().Be(ErrorType.Validation);
	}
}
