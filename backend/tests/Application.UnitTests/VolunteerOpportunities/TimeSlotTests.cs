using AwesomeAssertions;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.UnitTests.VolunteerOpportunities;

public class TimeSlotTests
{
	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
	private static readonly DateTimeOffset Tomorrow = Now.AddDays(1);

	[Test]
	public void Create_ShouldCreateTimeSlot_WithValidData()
	{
		var timeSlot = TimeSlot.Create(Now, Tomorrow, maxParticipants: 10);

		timeSlot.StartDateTime.Should().Be(Now);
		timeSlot.EndDateTime.Should().Be(Tomorrow);
		timeSlot.MaxParticipants.Should().Be(10);
	}

	[Test]
	public void Create_ShouldAssignId()
	{
		var timeSlot = TimeSlot.Create(Now, Tomorrow, maxParticipants: 5);

		timeSlot.Id.Value.Should().NotBe(Guid.Empty);
	}

	[Test]
	public void Create_ShouldThrow_WhenEndDateIsBeforeStartDate()
	{
		var start = Tomorrow;
		var end = Now;

		Action act = () => TimeSlot.Create(start, end, maxParticipants: 10);

		act.Should().Throw<DomainException>().WithMessage("*End date must be after start date*");
	}

	[Test]
	public void Create_ShouldThrow_WhenEndDateEqualsStartDate()
	{
		Action act = () => TimeSlot.Create(Now, Now, maxParticipants: 10);

		act.Should().Throw<DomainException>().WithMessage("*End date must be after start date*");
	}

	[Test]
	public void Create_ShouldThrow_WhenMaxParticipantsIsZero()
	{
		Action act = () => TimeSlot.Create(Now, Tomorrow, maxParticipants: 0);

		act.Should().Throw<DomainException>().WithMessage("*Max participants must be greater than zero*");
	}

	[Test]
	public void Create_ShouldThrow_WhenMaxParticipantsIsNegative()
	{
		Action act = () => TimeSlot.Create(Now, Tomorrow, maxParticipants: -1);

		act.Should().Throw<DomainException>().WithMessage("*Max participants must be greater than zero*");
	}

	[Test]
	public void Create_ShouldAllowMaxParticipantsOfOne()
	{
		var timeSlot = TimeSlot.Create(Now, Tomorrow, maxParticipants: 1);

		timeSlot.MaxParticipants.Should().Be(1);
	}

	[Test]
	public void Create_ShouldGenerateUniqueIds_ForDifferentSlots()
	{
		var slot1 = TimeSlot.Create(Now, Tomorrow, maxParticipants: 5);
		var slot2 = TimeSlot.Create(Now, Tomorrow, maxParticipants: 5);

		slot1.Id.Should().NotBe(slot2.Id);
	}
}
