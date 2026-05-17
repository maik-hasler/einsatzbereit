using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.VolunteerOpportunities;


namespace Application.UnitTests.VolunteerOpportunities;

public class VolunteerOpportunityTests
{
	private static readonly OrganizationId TestOrganizationId = new(Guid.NewGuid());
	private static readonly Address TestAddress = new("Sample Street", "1", "12345", "Berlin");

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
			CheckInMethod.None);

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

	// --- Update ---

	[Test]
	public void Update_ShouldChangeAllFields()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Old title", "Old desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None);
		var newAddress = new Address("Neue Straße", "42", "10115", "Hamburg");

		opportunity.Update("New title", "New desc", false, newAddress, CheckInMethod.None);

		opportunity.Title.Should().Be("New title");
		opportunity.Description.Should().Be("New desc");
		opportunity.Address.Should().Be(newAddress);
	}

	[Test]
	public void Update_ShouldAllowRemote_WithNullAddress()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None);

		opportunity.Update("Remote title", "Remote desc", true, null, CheckInMethod.None);

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
			CheckInMethod.None);

		Action act = () => opportunity.Update(title!, "Desc", false, TestAddress, CheckInMethod.None);

		act.Should().Throw<DomainException>().WithMessage("Title must not be empty.");
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Update_ShouldThrow_WhenDescriptionIsEmpty(string? description)
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None);

		Action act = () => opportunity.Update("Title", description!, false, TestAddress, CheckInMethod.None);

		act.Should().Throw<DomainException>().WithMessage("Description must not be empty.");
	}

	[Test]
	public void Update_ShouldThrow_WhenNotRemoteAndAddressIsNull()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None);

		Action act = () => opportunity.Update("Title", "Desc", false, null, CheckInMethod.None);

		act.Should().Throw<DomainException>().WithMessage("Address is required for non-remote opportunities.");
	}

	// --- AddTimeSlot ---

	[Test]
	public void AddTimeSlot_ShouldAddSlot_WhenParticipationTypeIsWaitlist()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None);
		var start = DateTimeOffset.UtcNow;
		var end = start.AddHours(2);

		opportunity.AddTimeSlot(start, end, maxParticipants: 20);

		opportunity.TimeSlots.Should().HaveCount(1);
		opportunity.TimeSlots.First().MaxParticipants.Should().Be(20);
	}

	[Test]
	public void AddTimeSlot_ShouldThrow_WhenParticipationTypeIsIndividualContact()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None);
		var start = DateTimeOffset.UtcNow;
		var end = start.AddHours(2);

		Action act = () => opportunity.AddTimeSlot(start, end, maxParticipants: 10);

		act.Should().Throw<DomainException>().WithMessage("*Waitlist*");
	}

	[Test]
	public void AddTimeSlot_ShouldSupportMultipleSlots()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.Recurring, ParticipationType.Waitlist,
			CheckInMethod.None);
		var base_ = DateTimeOffset.UtcNow;

		opportunity.AddTimeSlot(base_, base_.AddHours(2), 10);
		opportunity.AddTimeSlot(base_.AddDays(7), base_.AddDays(7).AddHours(2), 10);

		opportunity.TimeSlots.Should().HaveCount(2);
	}

	// --- RemoveTimeSlot ---

	[Test]
	public void RemoveTimeSlot_ShouldRemoveSlot_WhenSlotExists()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None);
		var start = DateTimeOffset.UtcNow;
		opportunity.AddTimeSlot(start, start.AddHours(2), 10);
		var slotId = opportunity.TimeSlots.First().Id;

		opportunity.RemoveTimeSlot(slotId);

		opportunity.TimeSlots.Should().BeEmpty();
	}

	[Test]
	public void RemoveTimeSlot_ShouldThrow_WhenSlotNotFound()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist,
			CheckInMethod.None);
		var nonExistentId = new TimeSlotId(Guid.CreateVersion7());

		Action act = () => opportunity.RemoveTimeSlot(nonExistentId);

		act.Should().Throw<DomainException>().WithMessage($"*{nonExistentId.Value}*");
	}

	[Test]
	public void RemoveTimeSlot_ShouldOnlyRemoveTargetSlot_WhenMultipleSlotsExist()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.Recurring, ParticipationType.Waitlist,
			CheckInMethod.None);
		var base_ = DateTimeOffset.UtcNow;
		opportunity.AddTimeSlot(base_, base_.AddHours(2), 5);
		opportunity.AddTimeSlot(base_.AddDays(7), base_.AddDays(7).AddHours(2), 5);

		var idToRemove = opportunity.TimeSlots.First().Id;
		opportunity.RemoveTimeSlot(idToRemove);

		opportunity.TimeSlots.Should().HaveCount(1);
		opportunity.TimeSlots.Should().NotContain(ts => ts.Id == idToRemove);
	}
}
