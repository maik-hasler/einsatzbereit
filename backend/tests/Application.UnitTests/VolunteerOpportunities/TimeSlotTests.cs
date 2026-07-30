using AwesomeAssertions;
using Domain.VolunteerOpportunities;

namespace Application.UnitTests.VolunteerOpportunities;

public class TimeSlotTests
{
	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
	private static readonly DateTimeOffset Tomorrow = Now.AddDays(1);
	private static readonly DateTimeOffset DayAfterTomorrow = Now.AddDays(2);

	[Test]
	public void Create_ShouldCreateTimeSlot_WithValidData()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 10, Now).Value;

		timeSlot.StartDateTime.Should().Be(Tomorrow);
		timeSlot.EndDateTime.Should().Be(DayAfterTomorrow);
		timeSlot.MaxParticipants.Should().Be(10);
	}

	[Test]
	public void Create_ShouldAssignId()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 5, Now).Value;

		timeSlot.Id.Value.Should().NotBe(Guid.Empty);
	}

	[Test]
	public void Create_ShouldFail_WhenStartDateIsInThePast()
	{
		var pastStart = Now.AddHours(-1);
		var futureEnd = Now.AddDays(1);

		var result = TimeSlot.Create(pastStart, futureEnd, maxParticipants: 10, Now);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Start date must be in the future*");
	}

	[Test]
	public void Create_ShouldFail_WhenEndDateIsBeforeStartDate()
	{
		var result = TimeSlot.Create(DayAfterTomorrow, Tomorrow, maxParticipants: 10, Now);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*End date must be after start date*");
	}

	[Test]
	public void Create_ShouldFail_WhenEndDateEqualsStartDate()
	{
		var result = TimeSlot.Create(Tomorrow, Tomorrow, maxParticipants: 10, Now);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*End date must be after start date*");
	}

	[Test]
	public void Create_ShouldFail_WhenMaxParticipantsIsZero()
	{
		var result = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 0, Now);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Max participants must be greater than zero*");
	}

	[Test]
	public void Create_ShouldFail_WhenMaxParticipantsIsNegative()
	{
		var result = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: -1, Now);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Max participants must be greater than zero*");
	}

	[Test]
	public void Create_ShouldAllowMaxParticipantsOfOne()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 1, Now).Value;

		timeSlot.MaxParticipants.Should().Be(1);
	}

	[Test]
	public void Create_ShouldGenerateUniqueIds_ForDifferentSlots()
	{
		var slot1 = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 5, Now).Value;
		var slot2 = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 5, Now).Value;

		slot1.Id.Should().NotBe(slot2.Id);
	}

	[Test]
	public void Create_ShouldAllowNullMaxParticipants_ForUnlimitedCapacity()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: null, Now).Value;

		timeSlot.MaxParticipants.Should().BeNull();
	}

	[Test]
	public void Create_ShouldLeaveSeriesFieldsNull_WhenNotProvided()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 10, Now).Value;

		timeSlot.SeriesId.Should().BeNull();
		timeSlot.RecurrenceFrequency.Should().BeNull();
		timeSlot.RecurrenceCount.Should().BeNull();
	}

	[Test]
	public void Create_ShouldStoreSeriesFields_WhenProvided()
	{
		var seriesId = Guid.CreateVersion7();

		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 10, Now, seriesId, "Weekly", 8).Value;

		timeSlot.SeriesId.Should().Be(seriesId);
		timeSlot.RecurrenceFrequency.Should().Be("Weekly");
		timeSlot.RecurrenceCount.Should().Be(8);
	}

	[Test]
	public void Update_ShouldAllowChangingToUnlimitedCapacity()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 10, Now).Value;

		var result = timeSlot.Update(Tomorrow, DayAfterTomorrow, maxParticipants: null, Now);

		result.IsSuccess.Should().BeTrue();
		timeSlot.MaxParticipants.Should().BeNull();
	}

	[Test]
	public void Update_ShouldAllowChangingFromUnlimitedToFixedCapacity()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: null, Now).Value;

		var result = timeSlot.Update(Tomorrow, DayAfterTomorrow, maxParticipants: 10, Now);

		result.IsSuccess.Should().BeTrue();
		timeSlot.MaxParticipants.Should().Be(10);
	}

	[Test]
	public void UpdateCapacity_ShouldChangeMaxParticipants_WhenPositive()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 10, Now).Value;

		var result = timeSlot.UpdateCapacity(20);

		result.IsSuccess.Should().BeTrue();
		timeSlot.MaxParticipants.Should().Be(20);
	}

	[Test]
	public void UpdateCapacity_ShouldAllowSettingToUnlimited()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 10, Now).Value;

		var result = timeSlot.UpdateCapacity(null);

		result.IsSuccess.Should().BeTrue();
		timeSlot.MaxParticipants.Should().BeNull();
	}

	[Test]
	public void UpdateCapacity_ShouldNotChangeDates()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 10, Now).Value;

		timeSlot.UpdateCapacity(20);

		timeSlot.StartDateTime.Should().Be(Tomorrow);
		timeSlot.EndDateTime.Should().Be(DayAfterTomorrow);
	}

	[Test]
	public void UpdateCapacity_ShouldFail_WhenMaxParticipantsIsZeroOrNegative()
	{
		var timeSlot = TimeSlot.Create(Tomorrow, DayAfterTomorrow, maxParticipants: 10, Now).Value;

		var result = timeSlot.UpdateCapacity(0);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Max participants must be greater than zero*");
		timeSlot.MaxParticipants.Should().Be(10);
	}
}
