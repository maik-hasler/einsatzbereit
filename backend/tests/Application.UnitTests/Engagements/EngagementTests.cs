using AwesomeAssertions;
using Domain.Engagements;
using Domain.VolunteerOpportunities;

namespace Application.UnitTests.Engagements;

public class EngagementTests
{
	private static VolunteerOpportunityId AnyOpportunityId() =>
		VolunteerOpportunityId.New();

	private static Domain.Users.UserId AnyUserId() =>
		Domain.Users.UserId.New();

	private static TimeSlotId AnyTimeSlotId() =>
		TimeSlotId.New();

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
		var result = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "Ich möchte gerne helfen.");

		result.Value.Status.Should().Be(EngagementStatus.Pending);
	}

	[Test]
	public void CreateIndividualContact_ShouldSetMessage()
	{
		var result = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "Ich bin verfügbar.");

		result.Value.Message.Should().Be("Ich bin verfügbar.");
	}

	[Test]
	public void CreateIndividualContact_ShouldNotSetTimeSlotId()
	{
		var result = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "Nachricht");

		result.Value.TimeSlotId.Should().BeNull();
	}

	[Test]
	public void CreateIndividualContact_ShouldFail_WhenMessageIsEmpty()
	{
		var result = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "");

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*message is required*");
	}

	[Test]
	public void CreateIndividualContact_ShouldFail_WhenMessageIsWhitespace()
	{
		var result = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "   ");

		result.IsFailure.Should().BeTrue();
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
	public void Confirm_ShouldFail_WhenAlreadyConfirmed()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();

		var result = engagement.Confirm();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Only pending*");
	}

	[Test]
	public void Confirm_ShouldFail_WhenCancelled()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Cancel();

		var result = engagement.Confirm();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Only pending*");
	}

	[Test]
	public void Confirm_ShouldFail_WhenWithdrawn()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();

		var result = engagement.Confirm();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Only pending*");
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
	public void Cancel_ShouldFail_WhenAlreadyCancelled()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Cancel();

		var result = engagement.Cancel();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*already terminated*");
	}

	[Test]
	public void Cancel_ShouldFail_WhenWithdrawn()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();

		var result = engagement.Cancel();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*already terminated*");
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
	public void Withdraw_ShouldFail_WhenAlreadyWithdrawn()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();

		var result = engagement.Withdraw();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*already terminated*");
	}

	[Test]
	public void Withdraw_ShouldFail_WhenCancelled()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Cancel();

		var result = engagement.Withdraw();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*already terminated*");
	}

	[Test]
	public void Withdraw_ShouldFail_WhenCheckedIn()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();
		engagement.CheckIn();

		var result = engagement.Withdraw();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*checked-in*");
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
	public void Reactivate_ShouldFail_WhenPending()
	{
		var engagement = Engagement.CreateWaitlistSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		var result = engagement.Reactivate(AnyTimeSlotId(), message: null);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*withdrawn or cancelled*");
	}
}
