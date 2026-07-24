using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities;

public class VolunteerOpportunityTests
{
	private static readonly OrganizationId TestOrganizationId = OrganizationId.New();
	private static readonly Address TestAddress = Address.Create("Sample Street", "1", "12345", "Berlin").Value;
	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
	private static readonly DateTimeOffset FutureSlotStart = Now.AddDays(1);
	private static readonly IPinGenerator PinGenerator = Substitute.For<IPinGenerator>();

	[Test]
	public void Create_ShouldCreateVolunteerOpportunity_WithValidData()
	{
		// Act
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Helpers needed",
			"We need helpers for moving",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft).Value;

		// Assert
		opportunity.Title.Should().Be("Helpers needed");
		opportunity.Description.Should().Be("We need helpers for moving");
		opportunity.OrganizationId.Should().Be(TestOrganizationId);
		opportunity.IsRemote.Should().BeFalse();
		opportunity.Address.Should().Be(TestAddress);
		opportunity.Occurrence.Should().Be(Occurrence.OneTime);
		opportunity.ParticipationType.Should().Be(ParticipationType.Waitlist);
	}

	[Test]
	public void Create_ShouldCreateRemoteOpportunity()
	{
		// Act
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Remote help",
			"Online volunteering",
			true,
			null,
			Occurrence.Recurring,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			PinGenerator).Value;

		// Assert
		opportunity.IsRemote.Should().BeTrue();
		opportunity.Address.Should().BeNull();
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Create_ShouldFail_WhenTitleIsEmpty(string? title)
	{
		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			title!,
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			PinGenerator);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Title must not be empty.");
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Create_ShouldAllow_EmptyTitle_WhenDraft(string? title)
	{
		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			title!,
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft);

		// Assert
		result.IsSuccess.Should().BeTrue();
		result.Value.Title.Should().Be(title);
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Create_ShouldFail_WhenDescriptionIsEmpty(string? description)
	{
		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			description!,
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			PinGenerator);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Description must not be empty.");
	}

	[Test]
	public void Create_ShouldFail_WhenTitleExceedsMaxLength()
	{
		// Arrange
		var title = new string('a', VolunteerOpportunity.MaxTitleLength + 1);

		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			title,
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be($"Title must not exceed {VolunteerOpportunity.MaxTitleLength} characters.");
	}

	[Test]
	public void Create_ShouldAllow_TitleAtMaxLength()
	{
		// Arrange
		var title = new string('a', VolunteerOpportunity.MaxTitleLength);

		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			title,
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft);

		// Assert
		result.IsSuccess.Should().BeTrue();
	}

	[Test]
	public void Create_ShouldFail_WhenDescriptionExceedsMaxLength()
	{
		// Arrange
		var description = new string('a', VolunteerOpportunity.MaxDescriptionLength + 1);

		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			description,
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be($"Description must not exceed {VolunteerOpportunity.MaxDescriptionLength} characters.");
	}

	[Test]
	public void Create_ShouldAllow_DescriptionAtMaxLength()
	{
		// Arrange
		var description = new string('a', VolunteerOpportunity.MaxDescriptionLength);

		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			description,
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft);

		// Assert
		result.IsSuccess.Should().BeTrue();
	}

	[Test]
	public void Create_ShouldFail_WhenNotRemoteAndAddressIsNull()
	{
		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			null,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			PinGenerator);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Address is required for non-remote opportunities.");
	}

	[Test]
	public void Create_ShouldSetOccurrenceRecurring()
	{
		// Act
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Regular help",
			"Every Saturday",
			false,
			TestAddress,
			Occurrence.Recurring,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			PinGenerator).Value;

		// Assert
		opportunity.Occurrence.Should().Be(Occurrence.Recurring);
		opportunity.ParticipationType.Should().Be(ParticipationType.IndividualContact);
	}

	[Test]
	public void Create_ShouldFail_WhenPublishedWaitlistHasNoTimeSlots()
	{
		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Published);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Waitlist opportunity*");
	}

	[Test]
	public void Create_ShouldAllow_PublishedIndividualContact_WithNoTimeSlots()
	{
		// Act
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Published).Value;

		// Assert
		opportunity.Status.Should().Be(OpportunityStatus.Published);
	}

	// --- Update (granular methods) ---

	[Test]
	public void Update_ShouldChangeAllFields()
	{
		var opportunity = CreateDraftWaitlistOpportunity();
		var newAddress = Address.Create("Neue Straße", "42", "10115", "Hamburg").Value;

		opportunity.Rename("New title");
		opportunity.ChangeDescription("New desc");
		opportunity.Relocate(false, newAddress);
		opportunity.Reschedule(Occurrence.Recurring);
		opportunity.SwitchParticipationType(ParticipationType.IndividualContact);
		opportunity.ChangeCheckInMethod(CheckInMethod.Manual, PinGenerator);

		opportunity.Title.Should().Be("New title");
		opportunity.Description.Should().Be("New desc");
		opportunity.Address.Should().Be(newAddress);
		opportunity.Occurrence.Should().Be(Occurrence.Recurring);
		opportunity.ParticipationType.Should().Be(ParticipationType.IndividualContact);
		opportunity.CheckInMethod.Should().Be(CheckInMethod.Manual);
	}

	[Test]
	public void Reschedule_ShouldChangeOccurrence()
	{
		var opportunity = CreateDraftWaitlistOpportunity();

		opportunity.Reschedule(Occurrence.Recurring);

		opportunity.Occurrence.Should().Be(Occurrence.Recurring);
	}

	[Test]
	public void SwitchParticipationType_ShouldChangeParticipationType()
	{
		var opportunity = CreateDraftWaitlistOpportunity();

		opportunity.SwitchParticipationType(ParticipationType.IndividualContact);

		opportunity.ParticipationType.Should().Be(ParticipationType.IndividualContact);
	}

	[Test]
	public void SwitchParticipationType_ShouldClearTimeSlots_WhenSwitchingAwayFromWaitlist()
	{
		var opportunity = CreateDraftWaitlistOpportunity();
		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 10, Now);

		opportunity.SwitchParticipationType(ParticipationType.IndividualContact);

		opportunity.TimeSlots.Should().BeEmpty();
	}

	[Test]
	public void SwitchParticipationType_ShouldKeepTimeSlots_WhenStayingWaitlist()
	{
		var opportunity = CreateDraftWaitlistOpportunity();
		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 10, Now);

		opportunity.Rename("New title");
		opportunity.SwitchParticipationType(ParticipationType.Waitlist);

		opportunity.TimeSlots.Should().HaveCount(1);
	}

	[Test]
	public void Relocate_ShouldAllowRemote_WithNullAddress()
	{
		var opportunity = CreateDraftWaitlistOpportunity();

		opportunity.Rename("Remote title");
		opportunity.ChangeDescription("Remote desc");
		opportunity.Relocate(true, null);

		opportunity.IsRemote.Should().BeTrue();
		opportunity.Address.Should().BeNull();
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Rename_ShouldAllow_EmptyTitle_WhenDraft(string? title)
	{
		var opportunity = CreateDraftWaitlistOpportunity();

		var result = opportunity.Rename(title!);

		result.IsSuccess.Should().BeTrue();
		opportunity.Title.Should().Be(title);
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Rename_ShouldFail_WhenTitleIsEmpty_AndPublished(string? title)
	{
		var opportunity = CreatePublishedWaitlistOpportunity();

		var result = opportunity.Rename(title!);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Title must not be empty.");
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void ChangeDescription_ShouldFail_WhenDescriptionIsEmpty_AndPublished(string? description)
	{
		var opportunity = CreatePublishedWaitlistOpportunity();

		var result = opportunity.ChangeDescription(description!);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Description must not be empty.");
	}

	[Test]
	public void Rename_ShouldFail_WhenTitleExceedsMaxLength_EvenWhenDraft()
	{
		var opportunity = CreateDraftWaitlistOpportunity();
		var title = new string('a', VolunteerOpportunity.MaxTitleLength + 1);

		var result = opportunity.Rename(title);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be($"Title must not exceed {VolunteerOpportunity.MaxTitleLength} characters.");
	}

	[Test]
	public void ChangeDescription_ShouldFail_WhenDescriptionExceedsMaxLength_EvenWhenDraft()
	{
		var opportunity = CreateDraftWaitlistOpportunity();
		var description = new string('a', VolunteerOpportunity.MaxDescriptionLength + 1);

		var result = opportunity.ChangeDescription(description);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be($"Description must not exceed {VolunteerOpportunity.MaxDescriptionLength} characters.");
	}

	[Test]
	public void Relocate_ShouldFail_WhenNotRemoteAndAddressIsNull_AndPublished()
	{
		var opportunity = CreatePublishedWaitlistOpportunity();

		var result = opportunity.Relocate(false, null);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Address is required for non-remote opportunities.");
	}

	private static VolunteerOpportunity CreateDraftWaitlistOpportunity() =>
		VolunteerOpportunity.Create(
			TestOrganizationId, "Old title", "Old desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft).Value;

	private static VolunteerOpportunity CreatePublishedWaitlistOpportunity()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft).Value;
		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 10, Now);
		opportunity.Publish();
		return opportunity;
	}

	// --- CheckInPin ---

	[Test]
	public void Create_ShouldGeneratePin_WhenPINCodeAndNoPinGiven()
	{
		var pinGenerator = Substitute.For<IPinGenerator>();
		pinGenerator.GeneratePin().Returns("1234");

		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, pinGenerator).Value;

		opportunity.CheckInPin.Should().Be("1234");
	}

	[Test]
	public void Create_ShouldUseGivenPin_WhenPINCodeAndPinGiven()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, PinGenerator, checkInPin: "13579").Value;

		opportunity.CheckInPin.Should().Be("13579");
	}

	[Test]
	public void Create_ShouldNotSetPin_WhenCheckInMethodIsNotPINCode()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, PinGenerator, checkInPin: "1234").Value;

		opportunity.CheckInPin.Should().BeNull();
	}

	[Test]
	[Arguments("123")]
	[Arguments("1234567")]
	[Arguments("12ab")]
	public void Create_ShouldFail_WhenPinIsInvalidFormat(string pin)
	{
		var result = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, PinGenerator, checkInPin: pin);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Check-in PIN must be 4 to 6 digits.");
	}

	[Test]
	public void ChangeCheckInMethod_ShouldOverwritePin_WhenCustomPinGiven()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, PinGenerator, checkInPin: "1111").Value;

		opportunity.ChangeCheckInMethod(CheckInMethod.PINCode, PinGenerator, checkInPin: "2222");

		opportunity.CheckInPin.Should().Be("2222");
	}

	[Test]
	public void ChangeCheckInMethod_ShouldKeepExistingPin_WhenNoPinGiven()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, PinGenerator, checkInPin: "1111").Value;

		opportunity.ChangeCheckInMethod(CheckInMethod.PINCode, PinGenerator);

		opportunity.CheckInPin.Should().Be("1111");
	}

	[Test]
	public void ChangeCheckInMethod_ShouldGeneratePin_WhenSwitchedToPINCodeWithNoExistingPin()
	{
		var pinGenerator = Substitute.For<IPinGenerator>();
		pinGenerator.GeneratePin().Returns("5678");

		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, pinGenerator).Value;

		opportunity.ChangeCheckInMethod(CheckInMethod.PINCode, pinGenerator);

		opportunity.CheckInPin.Should().Be("5678");
	}

	[Test]
	public void ChangeCheckInMethod_ShouldFail_WhenPinIsInvalidFormat()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, PinGenerator, checkInPin: "1111").Value;

		var result = opportunity.ChangeCheckInMethod(CheckInMethod.PINCode, PinGenerator, checkInPin: "abc");

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Check-in PIN must be 4 to 6 digits.");
	}

	// --- AddTimeSlot ---

	[Test]
	public void AddTimeSlot_ShouldAddSlot_WhenParticipationTypeIsWaitlist()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft).Value;

		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), maxParticipants: 20, Now);

		opportunity.TimeSlots.Should().HaveCount(1);
		opportunity.TimeSlots.First().MaxParticipants.Should().Be(20);
	}

	[Test]
	public void AddTimeSlot_ShouldFail_WhenParticipationTypeIsIndividualContact()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, PinGenerator).Value;

		var result = opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), maxParticipants: 10, Now);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Waitlist*");
	}

	[Test]
	public void AddTimeSlot_ShouldSupportMultipleSlots()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.Recurring, ParticipationType.Waitlist,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft).Value;

		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 10, Now);
		opportunity.AddTimeSlot(FutureSlotStart.AddDays(7), FutureSlotStart.AddDays(7).AddHours(2), 10, Now);

		opportunity.TimeSlots.Should().HaveCount(2);
	}

	// --- RemoveTimeSlot ---

	[Test]
	public void RemoveTimeSlot_ShouldRemoveSlot_WhenSlotExists()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft).Value;
		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 10, Now);
		var slotId = opportunity.TimeSlots.First().Id;

		opportunity.RemoveTimeSlot(slotId);

		opportunity.TimeSlots.Should().BeEmpty();
	}

	[Test]
	public void RemoveTimeSlot_ShouldFail_WhenSlotNotFound()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft).Value;
		var nonExistentId = TimeSlotId.New();

		var result = opportunity.RemoveTimeSlot(nonExistentId);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match($"*{nonExistentId.Value}*");
	}

	[Test]
	public void RemoveTimeSlot_ShouldOnlyRemoveTargetSlot_WhenMultipleSlotsExist()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.Recurring, ParticipationType.Waitlist,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft).Value;
		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 5, Now);
		opportunity.AddTimeSlot(FutureSlotStart.AddDays(7), FutureSlotStart.AddDays(7).AddHours(2), 5, Now);

		var idToRemove = opportunity.TimeSlots.First().Id;
		opportunity.RemoveTimeSlot(idToRemove);

		opportunity.TimeSlots.Should().HaveCount(1);
		opportunity.TimeSlots.Should().NotContain(ts => ts.Id == idToRemove);
	}
}
