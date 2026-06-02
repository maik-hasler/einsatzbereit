using AwesomeAssertions;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.UnitTests.VolunteerOpportunities;

public class TimeSlotTests
{
	private static readonly DateTimeOffset Tomorrow = DateTimeOffset.UtcNow.AddDays(1);
	private static readonly DateTimeOffset DayAfterTomorrow = DateTimeOffset.UtcNow.AddDays(2);

	[Test]
	public void Create_ShouldCreateTimeSlot_WithValidData()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 10);

		timeSlot.StartDateTime.Should().Be(Tomorrow);
		timeSlot.EndDateTime.Should().Be(DayAfterTomorrow);
		timeSlot.MaxParticipants.Should().Be(10);
	}

	[Test]
	public void Create_ShouldAssignId()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 5);

		timeSlot.Id.Value.Should().NotBe(Guid.Empty);
	}

	[Test]
	public void Create_ShouldThrow_WhenStartDateIsInThePast()
	{
		var pastStart = DateTimeOffset.UtcNow.AddHours(-1);
		var futureEnd = DateTimeOffset.UtcNow.AddDays(1);

		Action act = () => TimeSlot.Create(pastStart, futureEnd, maxParticipants: 10);

		act.Should().Throw<DomainException>().WithMessage("*Start date must be in the future*");
	}

	[Test]
	public void Create_ShouldThrow_WhenEndDateIsBeforeStartDate()
	{
		Action act = () => TimeSlot.Create(DayAfterTomorrow, Tomorrow, maxParticipants: 10);

		act.Should().Throw<DomainException>().WithMessage("*End date must be after start date*");
	}

	[Test]
	public void Create_ShouldThrow_WhenEndDateEqualsStartDate()
	{
		Action act = () => TimeSlot.Create(Tomorrow, Tomorrow, maxParticipants: 10);

		act.Should().Throw<DomainException>().WithMessage("*End date must be after start date*");
	}

	[Test]
	public void Create_ShouldThrow_WhenMaxParticipantsIsZero()
	{
		Action act = () => TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 0);

		act.Should().Throw<DomainException>().WithMessage("*Max participants must be greater than zero*");
	}

	[Test]
	public void Create_ShouldThrow_WhenMaxParticipantsIsNegative()
	{
		Action act = () => TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: -1);

		act.Should().Throw<DomainException>().WithMessage("*Max participants must be greater than zero*");
	}

	[Test]
	public void Create_ShouldAllowMaxParticipantsOfOne()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 1);

		timeSlot.MaxParticipants.Should().Be(1);
	}

	[Test]
	public void Create_ShouldGenerateUniqueIds_ForDifferentSlots()
	{
		var slot1 = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 5);
		var slot2 = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 5);

		slot1.Id.Should().NotBe(slot2.Id);
	}
}
