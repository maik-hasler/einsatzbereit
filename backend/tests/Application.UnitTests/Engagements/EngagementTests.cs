using AwesomeAssertions;
using Domain.Engagements;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.UnitTests.Engagements;

public class EngagementTests
{
	private static VolunteerOpportunityId AnyOpportunityId() =>
		new(Guid.CreateVersion7());

	private static Domain.Users.UserId AnyUserId() =>
		new(Guid.CreateVersion7());

	private static TimeSlotId AnyTimeSlotId() =>
		new(Guid.CreateVersion7());

	// --- CreateWaitlistSignUp ---

	[Test]
	public void CreateWaitlistSignUp_ShouldCreateEngagement_WithPendingStatus()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		engagement.Status.Should().Be(EngagementStatus.Pending);
	}

	[Test]
	public void CreateWaitlistSignUp_ShouldSetTimeSlotId()
	{
		var timeSlotId = AnyTimeSlotId();

		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), timeSlotId);

		engagement.TimeSlotId.Should().Be(timeSlotId);
	}

	[Test]
	public void CreateWaitlistSignUp_ShouldNotSetMessage()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		engagement.Message.Should().BeNull();
	}

	// --- CreateIndividualContact ---

	[Test]
	public void CreateIndividualContact_ShouldCreateEngagement_WithPendingStatus()
	{
		var engagement = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "Ich möchte gerne helfen.");

		engagement.Status.Should().Be(EngagementStatus.Pending);
	}

	[Test]
	public void CreateIndividualContact_ShouldSetMessage()
	{
		var engagement = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "Ich bin verfügbar.");

		engagement.Message.Should().Be("Ich bin verfügbar.");
	}

	[Test]
	public void CreateIndividualContact_ShouldNotSetTimeSlotId()
	{
		var engagement = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "Nachricht");

		engagement.TimeSlotId.Should().BeNull();
	}

	[Test]
	public void CreateIndividualContact_ShouldThrow_WhenMessageIsEmpty()
	{
		Action act = () => Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "");

		act.Should().Throw<DomainException>().WithMessage("*message is required*");
	}

	[Test]
	public void CreateIndividualContact_ShouldThrow_WhenMessageIsWhitespace()
	{
		Action act = () => Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "   ");

		act.Should().Throw<DomainException>();
	}

	// --- Confirm ---

	[Test]
	public void Confirm_ShouldSetStatus_ToConfirmed()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		engagement.Confirm();

		engagement.Status.Should().Be(EngagementStatus.Confirmed);
	}

	[Test]
	public void Confirm_ShouldThrow_WhenAlreadyConfirmed()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();

		Action act = () => engagement.Confirm();

		act.Should().Throw<DomainException>().WithMessage("*Only pending*");
	}

	[Test]
	public void Confirm_ShouldThrow_WhenCancelled()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Cancel();

		Action act = () => engagement.Confirm();

		act.Should().Throw<DomainException>().WithMessage("*Only pending*");
	}

	[Test]
	public void Confirm_ShouldThrow_WhenWithdrawn()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();

		Action act = () => engagement.Confirm();

		act.Should().Throw<DomainException>().WithMessage("*Only pending*");
	}

	// --- Cancel ---

	[Test]
	public void Cancel_ShouldSetStatus_ToCancelled_WhenPending()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		engagement.Cancel();

		engagement.Status.Should().Be(EngagementStatus.Cancelled);
	}

	[Test]
	public void Cancel_ShouldSetStatus_ToCancelled_WhenConfirmed()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();

		engagement.Cancel();

		engagement.Status.Should().Be(EngagementStatus.Cancelled);
	}

	[Test]
	public void Cancel_ShouldThrow_WhenAlreadyCancelled()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Cancel();

		Action act = () => engagement.Cancel();

		act.Should().Throw<DomainException>().WithMessage("*already terminated*");
	}

	[Test]
	public void Cancel_ShouldThrow_WhenWithdrawn()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();

		Action act = () => engagement.Cancel();

		act.Should().Throw<DomainException>().WithMessage("*already terminated*");
	}

	// --- Withdraw ---

	[Test]
	public void Withdraw_ShouldSetStatus_ToWithdrawn_WhenPending()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		engagement.Withdraw();

		engagement.Status.Should().Be(EngagementStatus.Withdrawn);
	}

	[Test]
	public void Withdraw_ShouldSetStatus_ToWithdrawn_WhenConfirmed()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();

		engagement.Withdraw();

		engagement.Status.Should().Be(EngagementStatus.Withdrawn);
	}

	[Test]
	public void Withdraw_ShouldThrow_WhenAlreadyWithdrawn()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();

		Action act = () => engagement.Withdraw();

		act.Should().Throw<DomainException>().WithMessage("*already terminated*");
	}

	[Test]
	public void Withdraw_ShouldThrow_WhenCancelled()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Cancel();

		Action act = () => engagement.Withdraw();

		act.Should().Throw<DomainException>().WithMessage("*already terminated*");
	}

	// --- Reactivate ---

	[Test]
	public void Reactivate_ShouldSetStatus_ToPending()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();

		engagement.Reactivate(AnyTimeSlotId(), message: null);

		engagement.Status.Should().Be(EngagementStatus.Pending);
	}

	[Test]
	public void Reactivate_ShouldRefreshCreatedOn_ToTheReactivationTime()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();

		var before = DateTimeOffset.UtcNow;
		engagement.Reactivate(AnyTimeSlotId(), message: null);
		var after = DateTimeOffset.UtcNow;

		engagement.CreatedOn.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
	}

	[Test]
	public void Reactivate_ShouldThrow_WhenPending()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		Action act = () => engagement.Reactivate(AnyTimeSlotId(), message: null);

		act.Should().Throw<DomainException>().WithMessage("*withdrawn or cancelled*");
	}
}
