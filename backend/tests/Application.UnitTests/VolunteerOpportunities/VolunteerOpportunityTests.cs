using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.VolunteerOpportunities;


namespace Application.UnitTests.VolunteerOpportunities;

public class VolunteerOpportunityTests
{
	private static readonly OrganizationId TestOrganizationId = new(Guid.NewGuid());
	private static readonly Address TestAddress = new("Sample Street", "1", "12345", "Berlin");
	private static readonly DateTimeOffset FutureSlotStart = DateTimeOffset.UtcNow.AddDays(1);

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
			status: OpportunityStatus.Draft);

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
			CheckInMethod.None);

		// Assert
		opportunity.IsRemote.Should().BeTrue();
		opportunity.Address.Should().BeNull();
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Create_ShouldThrowDomainException_WhenTitleIsEmpty(string? title)
	{
		// Act
		var act = () => VolunteerOpportunity.Create(
			TestOrganizationId,
			title!,
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None);

		// Assert
		act.Should().Throw<DomainException>()
			.WithMessage("Title must not be empty.");
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Create_ShouldThrowDomainException_WhenDescriptionIsEmpty(string? description)
	{
		// Act
		var act = () => VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			description!,
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None);

		// Assert
		act.Should().Throw<DomainException>()
			.WithMessage("Description must not be empty.");
	}

	[Test]
	public void Create_ShouldThrow_WhenNotRemoteAndAddressIsNull()
	{
		// Act
		var act = () => VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			null,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None);

		// Assert
		act.Should().Throw<DomainException>()
			.WithMessage("Address is required for non-remote opportunities.");
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
			CheckInMethod.None);

		// Assert
		opportunity.Occurrence.Should().Be(Occurrence.Recurring);
		opportunity.ParticipationType.Should().Be(ParticipationType.IndividualContact);
	}

	[Test]
	public void Create_ShouldThrow_WhenPublishedWaitlistHasNoTimeSlots()
	{
		// Act
		var act = () => VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			status: OpportunityStatus.Published);

		// Assert
		act.Should().Throw<DomainException>()
			.WithMessage("*Waitlist opportunity*");
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
			status: OpportunityStatus.Published);

		// Assert
		opportunity.Status.Should().Be(OpportunityStatus.Published);
	}

	// --- Update ---

	[Test]
	public void Update_ShouldChangeAllFields()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Old title", "Old desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, status: OpportunityStatus.Draft);
		var newAddress = new Address("Neue Straße", "42", "10115", "Hamburg");

		opportunity.Update("New title", "New desc", false, newAddress, Occurrence.Recurring, ParticipationType.IndividualContact, CheckInMethod.Manual, null, []);

		opportunity.Title.Should().Be("New title");
		opportunity.Description.Should().Be("New desc");
		opportunity.Address.Should().Be(newAddress);
		opportunity.Occurrence.Should().Be(Occurrence.Recurring);
		opportunity.ParticipationType.Should().Be(ParticipationType.IndividualContact);
		opportunity.CheckInMethod.Should().Be(CheckInMethod.Manual);
	}

	[Test]
	public void Update_ShouldChangeOccurrence()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, status: OpportunityStatus.Draft);

		opportunity.Update("Title", "Desc", false, TestAddress, Occurrence.Recurring, ParticipationType.Waitlist, CheckInMethod.None, null, []);

		opportunity.Occurrence.Should().Be(Occurrence.Recurring);
	}

	[Test]
	public void Update_ShouldChangeParticipationType()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, status: OpportunityStatus.Draft);

		opportunity.Update("Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, []);

		opportunity.ParticipationType.Should().Be(ParticipationType.IndividualContact);
	}

	[Test]
	public void Update_ShouldClearTimeSlots_WhenSwitchingAwayFromWaitlist()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, status: OpportunityStatus.Draft);
		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 10);

		opportunity.Update("Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, []);

		opportunity.TimeSlots.Should().BeEmpty();
	}

	[Test]
	public void Update_ShouldKeepTimeSlots_WhenStayingWaitlist()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, status: OpportunityStatus.Draft);
		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 10);

		opportunity.Update("New title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, []);

		opportunity.TimeSlots.Should().HaveCount(1);
	}

	[Test]
	public void Update_ShouldAllowRemote_WithNullAddress()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, status: OpportunityStatus.Draft);

		opportunity.Update("Remote title", "Remote desc", true, null, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, []);

		opportunity.IsRemote.Should().BeTrue();
		opportunity.Address.Should().BeNull();
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Update_ShouldThrow_WhenTitleIsEmpty(string? title)
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, status: OpportunityStatus.Draft);

		Action act = () => opportunity.Update(title!, "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, []);

		act.Should().Throw<DomainException>().WithMessage("Title must not be empty.");
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Update_ShouldThrow_WhenDescriptionIsEmpty(string? description)
	{
		var opportunity = CreatePublishedWaitlistOpportunity();

		Action act = () => opportunity.Update("Title", description!, false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, []);

		act.Should().Throw<DomainException>().WithMessage("Description must not be empty.");
	}

	[Test]
	public void Update_ShouldThrow_WhenNotRemoteAndAddressIsNull()
	{
		var opportunity = CreatePublishedWaitlistOpportunity();

		Action act = () => opportunity.Update("Title", "Desc", false, null, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, []);

		act.Should().Throw<DomainException>().WithMessage("Address is required for non-remote opportunities.");
	}

	private VolunteerOpportunity CreatePublishedWaitlistOpportunity()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, status: OpportunityStatus.Draft);
		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 10);
		opportunity.Publish();
		return opportunity;
	}

	// --- CheckInPin ---

	[Test]
	public void Create_ShouldGeneratePin_WhenPINCodeAndNoPinGiven()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode);

		opportunity.CheckInPin.Should().NotBeNullOrEmpty();
		opportunity.CheckInPin.Should().HaveLength(4);
	}

	[Test]
	public void Create_ShouldUseGivenPin_WhenPINCodeAndPinGiven()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, checkInPin: "13579");

		opportunity.CheckInPin.Should().Be("13579");
	}

	[Test]
	public void Create_ShouldNotSetPin_WhenCheckInMethodIsNotPINCode()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, checkInPin: "1234");

		opportunity.CheckInPin.Should().BeNull();
	}

	[Test]
	[Arguments("123")]
	[Arguments("1234567")]
	[Arguments("12ab")]
	public void Create_ShouldThrow_WhenPinIsInvalidFormat(string pin)
	{
		Action act = () => VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, checkInPin: pin);

		act.Should().Throw<DomainException>().WithMessage("Check-in PIN must be 4 to 6 digits.");
	}

	[Test]
	public void Update_ShouldOverwritePin_WhenCustomPinGiven()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, checkInPin: "1111");

		opportunity.Update("Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.PINCode, null, [], checkInPin: "2222");

		opportunity.CheckInPin.Should().Be("2222");
	}

	[Test]
	public void Update_ShouldKeepExistingPin_WhenNoPinGiven()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, checkInPin: "1111");

		opportunity.Update("Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.PINCode, null, []);

		opportunity.CheckInPin.Should().Be("1111");
	}

	[Test]
	public void Update_ShouldGeneratePin_WhenSwitchedToPINCodeWithNoExistingPin()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None);

		opportunity.Update("Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.PINCode, null, []);

		opportunity.CheckInPin.Should().NotBeNullOrEmpty();
	}

	[Test]
	public void Update_ShouldThrow_WhenPinIsInvalidFormat()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, checkInPin: "1111");

		Action act = () => opportunity.Update("Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.PINCode, null, [], checkInPin: "abc");

		act.Should().Throw<DomainException>().WithMessage("Check-in PIN must be 4 to 6 digits.");
	}

	// --- AddTimeSlot ---

	[Test]
	public void AddTimeSlot_ShouldAddSlot_WhenParticipationTypeIsWaitlist()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, status: OpportunityStatus.Draft);

		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), maxParticipants: 20);

		opportunity.TimeSlots.Should().HaveCount(1);
		opportunity.TimeSlots.First().MaxParticipants.Should().Be(20);
	}

	[Test]
	public void AddTimeSlot_ShouldThrow_WhenParticipationTypeIsIndividualContact()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None);

		Action act = () => opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), maxParticipants: 10);

		act.Should().Throw<DomainException>().WithMessage("*Waitlist*");
	}

	[Test]
	public void AddTimeSlot_ShouldSupportMultipleSlots()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.Recurring, ParticipationType.Waitlist,
			CheckInMethod.None, status: OpportunityStatus.Draft);

		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 10);
		opportunity.AddTimeSlot(FutureSlotStart.AddDays(7), FutureSlotStart.AddDays(7).AddHours(2), 10);

		opportunity.TimeSlots.Should().HaveCount(2);
	}

	// --- RemoveTimeSlot ---

	[Test]
	public void RemoveTimeSlot_ShouldRemoveSlot_WhenSlotExists()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, status: OpportunityStatus.Draft);
		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 10);
		var slotId = opportunity.TimeSlots.First().Id;

		opportunity.RemoveTimeSlot(slotId);

		opportunity.TimeSlots.Should().BeEmpty();
	}

	[Test]
	public void RemoveTimeSlot_ShouldThrow_WhenSlotNotFound()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None, status: OpportunityStatus.Draft);
		var nonExistentId = new TimeSlotId(Guid.CreateVersion7());

		Action act = () => opportunity.RemoveTimeSlot(nonExistentId);

		act.Should().Throw<DomainException>().WithMessage($"*{nonExistentId.Value}*");
	}

	[Test]
	public void RemoveTimeSlot_ShouldOnlyRemoveTargetSlot_WhenMultipleSlotsExist()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.Recurring, ParticipationType.Waitlist,
			CheckInMethod.None, status: OpportunityStatus.Draft);
		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 5);
		opportunity.AddTimeSlot(FutureSlotStart.AddDays(7), FutureSlotStart.AddDays(7).AddHours(2), 5);

		var idToRemove = opportunity.TimeSlots.First().Id;
		opportunity.RemoveTimeSlot(idToRemove);

		opportunity.TimeSlots.Should().HaveCount(1);
		opportunity.TimeSlots.Should().NotContain(ts => ts.Id == idToRemove);
	}
}
