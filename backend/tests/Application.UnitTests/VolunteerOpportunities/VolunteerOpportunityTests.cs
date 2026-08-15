using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
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
			ParticipationType.ScheduledSlots,
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
		opportunity.ParticipationType.Should().Be(ParticipationType.ScheduledSlots);
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
			PinGenerator,
			validUntil: Now.AddDays(30)).Value;

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
			ParticipationType.ScheduledSlots,
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
			ParticipationType.ScheduledSlots,
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
			ParticipationType.ScheduledSlots,
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
			ParticipationType.ScheduledSlots,
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
			ParticipationType.ScheduledSlots,
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
			ParticipationType.ScheduledSlots,
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
			ParticipationType.ScheduledSlots,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft);

		// Assert
		result.IsSuccess.Should().BeTrue();
	}

	// --- Tags (#1678) ---

	[Test]
	public void Create_ShouldFail_WhenTooManyTags()
	{
		// Arrange
		var tags = Enumerable.Range(0, VolunteerOpportunity.MaxTagsCount + 1).Select(i => $"tag{i}").ToList();

		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.ScheduledSlots,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft,
			tags: tags);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be($"An opportunity cannot have more than {VolunteerOpportunity.MaxTagsCount} tags.");
	}

	[Test]
	public void Create_ShouldAllow_TagsCountAtMax()
	{
		// Arrange
		var tags = Enumerable.Range(0, VolunteerOpportunity.MaxTagsCount).Select(i => $"tag{i}").ToList();

		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.ScheduledSlots,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft,
			tags: tags);

		// Assert
		result.IsSuccess.Should().BeTrue();
		result.Value.Tags.Should().HaveCount(VolunteerOpportunity.MaxTagsCount);
	}

	[Test]
	public void Create_ShouldFail_WhenATagExceedsMaxLength()
	{
		// Arrange
		var tag = new string('a', VolunteerOpportunity.MaxTagLength + 1);

		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.ScheduledSlots,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft,
			tags: [tag]);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be($"Each tag must not exceed {VolunteerOpportunity.MaxTagLength} characters.");
	}

	[Test]
	public void Create_ShouldAllow_TagAtMaxLength()
	{
		// Arrange
		var tag = new string('a', VolunteerOpportunity.MaxTagLength);

		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.ScheduledSlots,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft,
			tags: [tag]);

		// Assert
		result.IsSuccess.Should().BeTrue();
	}

	[Test]
	public void Recategorize_ShouldFail_WhenTooManyTags()
	{
		// Arrange
		var opportunity = CreateDraftScheduledSlotsOpportunity();
		var tags = Enumerable.Range(0, VolunteerOpportunity.MaxTagsCount + 1).Select(i => $"tag{i}").ToList();

		// Act
		var result = opportunity.Recategorize(null, tags);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be($"An opportunity cannot have more than {VolunteerOpportunity.MaxTagsCount} tags.");
		opportunity.Tags.Should().BeEmpty();
	}

	[Test]
	public void Recategorize_ShouldFail_WhenATagExceedsMaxLength()
	{
		// Arrange
		var opportunity = CreateDraftScheduledSlotsOpportunity();
		var tag = new string('a', VolunteerOpportunity.MaxTagLength + 1);

		// Act
		var result = opportunity.Recategorize(null, [tag]);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be($"Each tag must not exceed {VolunteerOpportunity.MaxTagLength} characters.");
	}

	[Test]
	public void Recategorize_ShouldUpdateCategoryAndTags_WhenValid()
	{
		// Arrange
		var opportunity = CreateDraftScheduledSlotsOpportunity();

		// Act
		var result = opportunity.Recategorize(Category.Environment, ["gardening", "cleanup"]);

		// Assert
		result.IsSuccess.Should().BeTrue();
		opportunity.Category.Should().Be(Category.Environment);
		opportunity.Tags.Should().BeEquivalentTo(["gardening", "cleanup"]);
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
			ParticipationType.ScheduledSlots,
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
			PinGenerator,
			validUntil: Now.AddDays(30)).Value;

		// Assert
		opportunity.Occurrence.Should().Be(Occurrence.Recurring);
		opportunity.ParticipationType.Should().Be(ParticipationType.IndividualContact);
	}

	[Test]
	public void Create_ShouldFail_WhenPublishedScheduledSlotsHasNoTimeSlots()
	{
		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.ScheduledSlots,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Published);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Scheduled slots opportunity*");
	}

	[Test]
	public void Create_ShouldAllow_PublishedIndividualContact_WithNoTimeSlots_AndValidUntilGiven()
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
			status: OpportunityStatus.Published,
			validUntil: Now.AddDays(30)).Value;

		// Assert
		opportunity.Status.Should().Be(OpportunityStatus.Published);
		opportunity.TimeSlots.Should().BeEmpty();
	}

	// --- ValidUntil (einsatzbereit#1086) ---

	[Test]
	public void Create_ShouldFail_WhenPublishedIndividualContact_HasNoValidUntil()
	{
		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Published);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*deadline*");
	}

	[Test]
	public void Create_ShouldAllow_DraftIndividualContact_WithNoValidUntil()
	{
		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft);

		// Assert
		result.IsSuccess.Should().BeTrue();
		result.Value.ValidUntil.Should().BeNull();
	}

	[Test]
	public void Create_ShouldFail_WhenValidUntilGiven_ForScheduledSlots()
	{
		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.ScheduledSlots,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft,
			validUntil: Now.AddDays(30));

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("A deadline can only be set for Individual contact opportunities.");
	}

	[Test]
	[Arguments(0)]
	[Arguments(-1)]
	public void Create_ShouldFail_WhenValidUntilIsNotInFuture(int daysOffset)
	{
		// Act
		var result = VolunteerOpportunity.Create(
			TestOrganizationId,
			"Title",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			PinGenerator,
			status: OpportunityStatus.Draft,
			validUntil: Now.AddDays(daysOffset),
			now: Now);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Deadline must be in the future.");
	}

	[Test]
	public void Publish_ShouldFail_WhenIndividualContactHasNoValidUntil()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft).Value;

		var result = opportunity.Publish();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*deadline*");
		opportunity.Status.Should().Be(OpportunityStatus.Draft);
	}

	[Test]
	public void Publish_ShouldSucceed_WhenIndividualContactHasValidUntil()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft).Value;
		opportunity.SetValidUntil(Now.AddDays(14), Now);

		var result = opportunity.Publish();

		result.IsSuccess.Should().BeTrue();
		opportunity.Status.Should().Be(OpportunityStatus.Published);
	}

	[Test]
	public void SetValidUntil_ShouldSetValue_WhenIndividualContact()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft).Value;

		var result = opportunity.SetValidUntil(Now.AddDays(14), Now);

		result.IsSuccess.Should().BeTrue();
		opportunity.ValidUntil.Should().Be(Now.AddDays(14));
	}

	[Test]
	public void SetValidUntil_ShouldClearValue_WhenGivenNull()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft, validUntil: Now.AddDays(14)).Value;

		var result = opportunity.SetValidUntil(null, Now);

		result.IsSuccess.Should().BeTrue();
		opportunity.ValidUntil.Should().BeNull();
	}

	[Test]
	public void SetValidUntil_ShouldFail_WhenScheduledSlots()
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();

		var result = opportunity.SetValidUntil(Now.AddDays(14), Now);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("A deadline can only be set for Individual contact opportunities.");
	}

	[Test]
	public void SetValidUntil_ShouldFail_WhenNotInFuture()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft).Value;

		var result = opportunity.SetValidUntil(Now, Now);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Deadline must be in the future.");
	}

	[Test]
	public void SwitchParticipationType_ShouldClearValidUntil_WhenSwitchingAwayFromIndividualContact()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft, validUntil: Now.AddDays(14)).Value;

		opportunity.SwitchParticipationType(ParticipationType.ScheduledSlots);

		opportunity.ValidUntil.Should().BeNull();
	}

	[Test]
	public void SwitchParticipationType_ShouldKeepValidUntil_WhenStayingIndividualContact()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft, validUntil: Now.AddDays(14)).Value;

		opportunity.SwitchParticipationType(ParticipationType.IndividualContact);

		opportunity.ValidUntil.Should().Be(Now.AddDays(14));
	}

	// --- Unpublish / Cancel (einsatzbereit#1038) ---

	[Test]
	public void Unpublish_ShouldSetStatusToUnpublished_WhenPublished()
	{
		var opportunity = CreatePublishedScheduledSlotsOpportunity();

		var result = opportunity.Unpublish();

		result.IsSuccess.Should().BeTrue();
		opportunity.Status.Should().Be(OpportunityStatus.Unpublished);
	}

	[Test]
	public void Unpublish_ShouldFail_WhenDraft()
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();

		var result = opportunity.Unpublish();

		result.IsFailure.Should().BeTrue();
		result.Error.Type.Should().Be(ErrorType.Conflict);
	}

	[Test]
	public void Unpublish_ShouldFail_WhenAlreadyUnpublished()
	{
		var opportunity = CreatePublishedScheduledSlotsOpportunity();
		opportunity.Unpublish();

		var result = opportunity.Unpublish();

		result.IsFailure.Should().BeTrue();
	}

	[Test]
	public void Unpublish_ShouldFail_WhenCancelled()
	{
		var opportunity = CreatePublishedScheduledSlotsOpportunity();
		opportunity.Cancel();

		var result = opportunity.Unpublish();

		result.IsFailure.Should().BeTrue();
	}

	[Test]
	public void Publish_ShouldRepublish_AnUnpublishedOpportunity()
	{
		var opportunity = CreatePublishedScheduledSlotsOpportunity();
		opportunity.Unpublish();

		var result = opportunity.Publish();

		result.IsSuccess.Should().BeTrue();
		opportunity.Status.Should().Be(OpportunityStatus.Published);
	}

	[Test]
	public void Cancel_ShouldSetStatusToCancelled_WhenPublished()
	{
		var opportunity = CreatePublishedScheduledSlotsOpportunity();

		var result = opportunity.Cancel("No longer needed");

		result.IsSuccess.Should().BeTrue();
		opportunity.Status.Should().Be(OpportunityStatus.Cancelled);
		opportunity.CancellationReason.Should().Be("No longer needed");
	}

	[Test]
	public void Cancel_ShouldSetStatusToCancelled_WhenUnpublished()
	{
		var opportunity = CreatePublishedScheduledSlotsOpportunity();
		opportunity.Unpublish();

		var result = opportunity.Cancel();

		result.IsSuccess.Should().BeTrue();
		opportunity.Status.Should().Be(OpportunityStatus.Cancelled);
		opportunity.CancellationReason.Should().BeNull();
	}

	[Test]
	public void Cancel_ShouldFail_WhenDraft()
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();

		var result = opportunity.Cancel();

		result.IsFailure.Should().BeTrue();
		result.Error.Type.Should().Be(ErrorType.Conflict);
		opportunity.Status.Should().Be(OpportunityStatus.Draft);
	}

	[Test]
	public void Cancel_ShouldFail_WhenAlreadyCancelled()
	{
		var opportunity = CreatePublishedScheduledSlotsOpportunity();
		opportunity.Cancel();

		var result = opportunity.Cancel();

		result.IsFailure.Should().BeTrue();
	}

	[Test]
	public void Publish_ShouldFail_WhenCancelled_AndNotResurrectOpportunity()
	{
		var opportunity = CreatePublishedScheduledSlotsOpportunity();
		opportunity.Cancel();

		var result = opportunity.Publish();

		result.IsFailure.Should().BeTrue();
		result.Error.Type.Should().Be(ErrorType.Conflict);
		opportunity.Status.Should().Be(OpportunityStatus.Cancelled);
	}

	// --- Update (granular methods) ---

	[Test]
	public void Update_ShouldChangeAllFields()
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();
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
		var opportunity = CreateDraftScheduledSlotsOpportunity();

		opportunity.Reschedule(Occurrence.Recurring);

		opportunity.Occurrence.Should().Be(Occurrence.Recurring);
	}

	[Test]
	public void SwitchParticipationType_ShouldChangeParticipationType()
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();

		opportunity.SwitchParticipationType(ParticipationType.IndividualContact);

		opportunity.ParticipationType.Should().Be(ParticipationType.IndividualContact);
	}

	[Test]
	public void SwitchParticipationType_ShouldClearTimeSlots_WhenSwitchingAwayFromScheduledSlots()
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();
		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 10, Now);

		opportunity.SwitchParticipationType(ParticipationType.IndividualContact);

		opportunity.TimeSlots.Should().BeEmpty();
	}

	[Test]
	public void SwitchParticipationType_ShouldKeepTimeSlots_WhenStayingScheduledSlots()
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();
		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 10, Now);

		opportunity.Rename("New title");
		opportunity.SwitchParticipationType(ParticipationType.ScheduledSlots);

		opportunity.TimeSlots.Should().HaveCount(1);
	}

	[Test]
	public void Relocate_ShouldAllowRemote_WithNullAddress()
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();

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
		var opportunity = CreateDraftScheduledSlotsOpportunity();

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
		var opportunity = CreatePublishedScheduledSlotsOpportunity();

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
		var opportunity = CreatePublishedScheduledSlotsOpportunity();

		var result = opportunity.ChangeDescription(description!);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Description must not be empty.");
	}

	[Test]
	public void Rename_ShouldFail_WhenTitleExceedsMaxLength_EvenWhenDraft()
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();
		var title = new string('a', VolunteerOpportunity.MaxTitleLength + 1);

		var result = opportunity.Rename(title);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be($"Title must not exceed {VolunteerOpportunity.MaxTitleLength} characters.");
	}

	[Test]
	public void ChangeDescription_ShouldFail_WhenDescriptionExceedsMaxLength_EvenWhenDraft()
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();
		var description = new string('a', VolunteerOpportunity.MaxDescriptionLength + 1);

		var result = opportunity.ChangeDescription(description);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be($"Description must not exceed {VolunteerOpportunity.MaxDescriptionLength} characters.");
	}

	[Test]
	public void Relocate_ShouldFail_WhenNotRemoteAndAddressIsNull_AndPublished()
	{
		var opportunity = CreatePublishedScheduledSlotsOpportunity();

		var result = opportunity.Relocate(false, null);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Address is required for non-remote opportunities.");
	}

	// --- SetColor (einsatzbereit#1286) ---

	[Test]
	public void SetColor_ShouldSetValue_WhenHexHasSufficientContrast()
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();

		// #c10007, not #ff0000: pure red's best text contrast (white-on-red)
		// is only 4.44:1 - it clears the 3:1 chip-vs-page floor but not the
		// 4.5:1 text floor added for einsatzbereit#1726, see
		// SetColor_ShouldFail_WhenTextContrastIsBelowMinimum below.
		var result = opportunity.SetColor("#c10007");

		result.IsSuccess.Should().BeTrue();
		opportunity.Color.Should().Be("#c10007");
	}

	[Test]
	public void SetColor_ShouldClearValue_WhenGivenNull()
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();
		opportunity.SetColor("#ff0000");

		var result = opportunity.SetColor(null);

		result.IsSuccess.Should().BeTrue();
		opportunity.Color.Should().BeNull();
	}

	[Test]
	[Arguments("ff0000")]
	[Arguments("#ff00")]
	[Arguments("#gggggg")]
	[Arguments("red")]
	public void SetColor_ShouldFail_WhenNotAValidHexColor(string color)
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();

		var result = opportunity.SetColor(color);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Color must be a #rrggbb hex value.");
		opportunity.Color.Should().BeNull();
	}

	[Test]
	[Arguments("#ffff00")]
	[Arguments("#ffffff")]
	[Arguments("#5bbf8c")]
	public void SetColor_ShouldFail_WhenContrastAgainstWhiteIsBelowMinimum(string color)
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();

		var result = opportunity.SetColor(color);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*contrast*");
		opportunity.Color.Should().BeNull();
	}

	// einsatzbereit#1726: #2d8a5e (the project's own brand-600) clears the
	// 3:1 chip-vs-page floor above (4.28:1) but its best possible chip text
	// (white, also 4.28:1) still falls short of the independent 4.5:1 text
	// floor - the two candidates cross over near this luminance, so neither
	// white nor near-black text clears it.
	[Test]
	[Arguments("#2d8a5e")]
	[Arguments("#ff0000")]
	public void SetColor_ShouldFail_WhenTextContrastIsBelowMinimum(string color)
	{
		var opportunity = CreateDraftScheduledSlotsOpportunity();

		var result = opportunity.SetColor(color);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*contrast*");
		opportunity.Color.Should().BeNull();
	}

	private static VolunteerOpportunity CreateDraftScheduledSlotsOpportunity() =>
		VolunteerOpportunity.Create(
			TestOrganizationId, "Old title", "Old desc", false, TestAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft).Value;

	private static VolunteerOpportunity CreatePublishedScheduledSlotsOpportunity()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots,
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
			CheckInMethod.PINCode, pinGenerator, validUntil: Now.AddDays(30)).Value;

		opportunity.CheckInPin.Should().Be("1234");
	}

	[Test]
	public void Create_ShouldUseGivenPin_WhenPINCodeAndPinGiven()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, PinGenerator, checkInPin: "13579", validUntil: Now.AddDays(30)).Value;

		opportunity.CheckInPin.Should().Be("13579");
	}

	[Test]
	public void Create_ShouldNotSetPin_WhenCheckInMethodIsNotPINCode()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, PinGenerator, checkInPin: "1234", validUntil: Now.AddDays(30)).Value;

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

	// --- CheckInPin triviality (#1176) ---

	[Test]
	[Arguments("0000")]
	[Arguments("1111")]
	[Arguments("9999")]
	[Arguments("000000")]
	[Arguments("555555")]
	[Arguments("1234")]
	[Arguments("4321")]
	[Arguments("123456")]
	[Arguments("654321")]
	[Arguments("1212")]
	[Arguments("6969")]
	[Arguments("2000")]
	[Arguments("1010")]
	public void Create_ShouldFail_WhenPinIsTrivial(string pin)
	{
		var result = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, PinGenerator, checkInPin: pin);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("This PIN is too easy to guess - choose a less predictable one.");
	}

	[Test]
	public void ChangeCheckInMethod_ShouldFail_WhenPinIsTrivial()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, PinGenerator, checkInPin: "4827", validUntil: Now.AddDays(30)).Value;

		var result = opportunity.ChangeCheckInMethod(CheckInMethod.PINCode, PinGenerator, checkInPin: "1111");

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("This PIN is too easy to guess - choose a less predictable one.");
		opportunity.CheckInPin.Should().Be("4827");
	}

	[Test]
	public void ChangeCheckInMethod_ShouldOverwritePin_WhenCustomPinGiven()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, PinGenerator, checkInPin: "4827", validUntil: Now.AddDays(30)).Value;

		opportunity.ChangeCheckInMethod(CheckInMethod.PINCode, PinGenerator, checkInPin: "6193");

		opportunity.CheckInPin.Should().Be("6193");
	}

	[Test]
	public void ChangeCheckInMethod_ShouldKeepExistingPin_WhenNoPinGiven()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, PinGenerator, checkInPin: "4827", validUntil: Now.AddDays(30)).Value;

		opportunity.ChangeCheckInMethod(CheckInMethod.PINCode, PinGenerator);

		opportunity.CheckInPin.Should().Be("4827");
	}

	[Test]
	public void ChangeCheckInMethod_ShouldGeneratePin_WhenSwitchedToPINCodeWithNoExistingPin()
	{
		var pinGenerator = Substitute.For<IPinGenerator>();
		pinGenerator.GeneratePin().Returns("5678");

		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, pinGenerator, validUntil: Now.AddDays(30)).Value;

		opportunity.ChangeCheckInMethod(CheckInMethod.PINCode, pinGenerator);

		opportunity.CheckInPin.Should().Be("5678");
	}

	[Test]
	public void ChangeCheckInMethod_ShouldFail_WhenPinIsInvalidFormat()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.PINCode, PinGenerator, checkInPin: "4827", validUntil: Now.AddDays(30)).Value;

		var result = opportunity.ChangeCheckInMethod(CheckInMethod.PINCode, PinGenerator, checkInPin: "abc");

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Check-in PIN must be 4 to 6 digits.");
	}

	[Test]
	public void AddTimeSlot_ShouldAddSlot_WhenParticipationTypeIsScheduledSlots()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots,
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
			CheckInMethod.None, PinGenerator, validUntil: Now.AddDays(30)).Value;

		var result = opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), maxParticipants: 10, Now);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Scheduled slots*");
	}

	[Test]
	public void AddTimeSlot_ShouldSupportMultipleSlots()
	{
		var opportunity = VolunteerOpportunity.Create(
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.Recurring, ParticipationType.ScheduledSlots,
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
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots,
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
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots,
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
			TestOrganizationId, "Title", "Desc", false, TestAddress, Occurrence.Recurring, ParticipationType.ScheduledSlots,
			CheckInMethod.None, PinGenerator, status: OpportunityStatus.Draft).Value;
		opportunity.AddTimeSlot(FutureSlotStart, FutureSlotStart.AddHours(2), 5, Now);
		opportunity.AddTimeSlot(FutureSlotStart.AddDays(7), FutureSlotStart.AddDays(7).AddHours(2), 5, Now);

		var idToRemove = opportunity.TimeSlots.First().Id;
		opportunity.RemoveTimeSlot(idToRemove);

		opportunity.TimeSlots.Should().HaveCount(1);
		opportunity.TimeSlots.Should().NotContain(ts => ts.Id == idToRemove);
	}
}
