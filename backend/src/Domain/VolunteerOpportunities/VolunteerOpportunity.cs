using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed class VolunteerOpportunity
	: AggregateRoot<VolunteerOpportunityId>,
		IAuditableEntity,
		ISoftDeletableEntity
{
	public const int MaxTitleLength = 200;

	public const int MaxDescriptionLength = 5000;

	private readonly List<TimeSlot> _timeSlots = [];

	private List<string> _tags = [];

	public OrganizationId OrganizationId { get; private set; }

	public string Title { get; private set; }

	public string Description { get; private set; }

	public bool IsRemote { get; private set; }

	public Address? Address { get; private set; }

	// True once a geocoding attempt has come back with a confirmed no-match for
	// this Address (Nominatim NotFound) - stops GeocodingRetryJob and the
	// event-driven handler from retrying an address that will never resolve.
	// Reset to false whenever the address text actually changes (see Relocate).
	public bool AddressGeocodingFailed { get; private set; }

	public Occurrence Occurrence { get; private set; }

	public ParticipationType ParticipationType { get; private set; }

	public CheckInMethod CheckInMethod { get; private set; }

	public Category? Category { get; private set; }

	public IReadOnlyList<string> Tags => _tags.AsReadOnly();

	public OpportunityStatus Status { get; private set; }

	public string? CancellationReason { get; private set; }

	public string? BannerImageUrl { get; private set; }

	public string? Color { get; private set; }

	public string? CheckInPin { get; private set; }

	/// <summary>
	/// Application deadline for Individual contact opportunities, which can never
	/// have TimeSlots (see AddTimeSlot) and would otherwise carry no date at all
	/// (einsatzbereit#1086). Always null for ScheduledSlots opportunities, which
	/// express "when" through their time slots instead.
	/// </summary>
	public DateTimeOffset? ValidUntil { get; private set; }

	public IReadOnlyCollection<TimeSlot> TimeSlots => _timeSlots.AsReadOnly();

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

	public bool IsDeleted { get; private set; }

	public DateTimeOffset? DeletedOn { get; private set; }

#pragma warning disable CS8618
	private VolunteerOpportunity() : base(default) { }
#pragma warning restore CS8618

	private VolunteerOpportunity(
		VolunteerOpportunityId id,
		OrganizationId organizationId,
		string title,
		string description,
		bool isRemote,
		Address? address,
		Occurrence occurrence,
		ParticipationType participationType,
		CheckInMethod checkInMethod,
		Category? category,
		IReadOnlyCollection<string> tags,
		OpportunityStatus status,
		IPinGenerator pinGenerator,
		string? checkInPin,
		DateTimeOffset? validUntil)
		: base(id)
	{
		OrganizationId = organizationId;
		Title = title;
		Description = description;
		IsRemote = isRemote;
		Address = address;
		Occurrence = occurrence;
		ParticipationType = participationType;
		CheckInMethod = checkInMethod;
		Category = category;
		_tags = new List<string>(tags);
		Status = status;
		ValidUntil = validUntil;
		if (checkInMethod == CheckInMethod.PINCode)
			CheckInPin = checkInPin ?? pinGenerator.GeneratePin();
	}

	private static Result EnsureValidPin(string pin)
	{
		if (pin.Length is < 4 or > 6 || !pin.All(char.IsAsciiDigit))
			return Result.Failure(Error.Validation("VolunteerOpportunity.InvalidCheckInPin", "Check-in PIN must be 4 to 6 digits."));

		return Result.Success();
	}

	private static Result EnsureValidTitleLength(string? title)
	{
		if (title is { Length: > MaxTitleLength })
			return Result.Failure(Error.Validation("VolunteerOpportunity.TitleTooLong", $"Title must not exceed {MaxTitleLength} characters."));

		return Result.Success();
	}

	private static Result EnsureValidDescriptionLength(string? description)
	{
		if (description is { Length: > MaxDescriptionLength })
			return Result.Failure(Error.Validation("VolunteerOpportunity.DescriptionTooLong", $"Description must not exceed {MaxDescriptionLength} characters."));

		return Result.Success();
	}

	private static Result EnsureValidValidUntil(ParticipationType participationType, DateTimeOffset? validUntil, DateTimeOffset now)
	{
		if (validUntil is null)
			return Result.Success();

		if (participationType != ParticipationType.IndividualContact)
			return Result.Failure(Error.Validation(
				"VolunteerOpportunity.ValidUntilNotAllowed",
				"A deadline can only be set for Individual contact opportunities."));

		if (validUntil <= now)
			return Result.Failure(Error.Validation("VolunteerOpportunity.ValidUntilMustBeFuture", "Deadline must be in the future."));

		return Result.Success();
	}

	private static Result EnsureIndividualContactHasValidUntil(ParticipationType participationType, DateTimeOffset? validUntil)
	{
		if (participationType == ParticipationType.IndividualContact && validUntil is null)
			return Result.Failure(Error.Validation(
				"VolunteerOpportunity.IndividualContactRequiresValidUntil",
				"An Individual contact opportunity must have a deadline before it can be published."));

		return Result.Success();
	}

	public static Result<VolunteerOpportunity> Create(
		OrganizationId organizationId,
		string title,
		string description,
		bool isRemote,
		Address? address,
		Occurrence occurrence,
		ParticipationType participationType,
		CheckInMethod checkInMethod,
		IPinGenerator pinGenerator,
		Category? category = null,
		IReadOnlyCollection<string>? tags = null,
		OpportunityStatus status = OpportunityStatus.Published,
		string? checkInPin = null,
		DateTimeOffset? validUntil = null,
		DateTimeOffset? now = null)
	{
		var validTitleLength = EnsureValidTitleLength(title);
		if (validTitleLength.IsFailure)
			return Result.Failure<VolunteerOpportunity>(validTitleLength.Error);

		var validDescriptionLength = EnsureValidDescriptionLength(description);
		if (validDescriptionLength.IsFailure)
			return Result.Failure<VolunteerOpportunity>(validDescriptionLength.Error);

		if (checkInMethod == CheckInMethod.PINCode && checkInPin is not null)
		{
			var validPin = EnsureValidPin(checkInPin);
			if (validPin.IsFailure)
				return Result.Failure<VolunteerOpportunity>(validPin.Error);
		}

		var validValidUntil = EnsureValidValidUntil(participationType, validUntil, now ?? DateTimeOffset.UtcNow);
		if (validValidUntil.IsFailure)
			return Result.Failure<VolunteerOpportunity>(validValidUntil.Error);

		if (status == OpportunityStatus.Published)
		{
			var publishable = EnsurePublishable(title, description, isRemote, address);
			if (publishable.IsFailure)
				return Result.Failure<VolunteerOpportunity>(publishable.Error);

			// Time slots can only be added after the aggregate is created (see
			// AddTimeSlot), so a ScheduledSlots opportunity can never satisfy the
			// "at least one time slot" rule at construction time. Callers must
			// create it as a Draft, add slots, then call Publish().
			if (participationType == ParticipationType.ScheduledSlots)
				return Result.Failure<VolunteerOpportunity>(Error.Validation(
					"VolunteerOpportunity.ScheduledSlotsMustStartAsDraft",
					"A Scheduled slots opportunity must be created as a draft and published after adding at least one time slot."));

			var hasValidUntil = EnsureIndividualContactHasValidUntil(participationType, validUntil);
			if (hasValidUntil.IsFailure)
				return Result.Failure<VolunteerOpportunity>(hasValidUntil.Error);
		}

		var opportunity = new VolunteerOpportunity(
			VolunteerOpportunityId.New(),
			organizationId,
			title,
			description,
			isRemote,
			address,
			occurrence,
			participationType,
			checkInMethod,
			category,
			tags ?? [],
			status,
			pinGenerator,
			checkInPin,
			validUntil);

		if (!isRemote && address is not null)
			opportunity.AddEvent(new VolunteerOpportunityGeocodingRequestedDomainEvent(opportunity.Id));

		return opportunity;
	}

	private static Result EnsurePublishable(
		string title,
		string description,
		bool isRemote,
		Address? address)
	{
		if (string.IsNullOrWhiteSpace(title))
			return Result.Failure(Error.Validation("VolunteerOpportunity.TitleRequired", "Title must not be empty."));

		if (string.IsNullOrWhiteSpace(description))
			return Result.Failure(Error.Validation("VolunteerOpportunity.DescriptionRequired", "Description must not be empty."));

		if (!isRemote && address is null)
			return Result.Failure(Error.Validation("VolunteerOpportunity.AddressRequired", "Address is required for non-remote opportunities."));

		return Result.Success();
	}

	public Result Publish()
	{
		if (Status == OpportunityStatus.Published)
			return Result.Failure(Error.Conflict("VolunteerOpportunity.AlreadyPublished", "Opportunity is already published."));

		// Cancelled is terminal - unlike Unpublished, there is no way back to
		// Published. Without this guard, calling Publish() on a Cancelled
		// opportunity would silently resurrect it (Publish only otherwise checks
		// for "already Published", not the source state).
		if (Status == OpportunityStatus.Cancelled)
			return Result.Failure(Error.Conflict("VolunteerOpportunity.CannotPublishCancelled", "A cancelled opportunity cannot be published again."));

		var publishable = EnsurePublishable(Title, Description, IsRemote, Address);
		if (publishable.IsFailure)
			return publishable;

		if (ParticipationType == ParticipationType.ScheduledSlots && _timeSlots.Count == 0)
			return Result.Failure(Error.Validation(
				"VolunteerOpportunity.ScheduledSlotsRequiresTimeSlot",
				"A Scheduled slots opportunity must have at least one time slot before it can be published."));

		var hasValidUntil = EnsureIndividualContactHasValidUntil(ParticipationType, ValidUntil);
		if (hasValidUntil.IsFailure)
			return hasValidUntil;

		Status = OpportunityStatus.Published;
		AddEvent(new VolunteerOpportunityPublishedDomainEvent(Id, OrganizationId));
		return Result.Success();
	}

	public Result Unpublish()
	{
		if (Status != OpportunityStatus.Published)
			return Result.Failure(Error.Conflict("VolunteerOpportunity.NotPublished", "Only a published opportunity can be unpublished."));

		Status = OpportunityStatus.Unpublished;
		AddEvent(new VolunteerOpportunityUnpublishedDomainEvent(Id, OrganizationId));
		return Result.Success();
	}

	public Result Cancel(string? reason = null)
	{
		if (Status == OpportunityStatus.Cancelled)
			return Result.Failure(Error.Conflict("VolunteerOpportunity.AlreadyCancelled", "Opportunity is already cancelled."));

		// Draft opportunities have no engagements and no public visibility to
		// take down - Delete already covers "give up on this draft" without
		// needing a cancellation reason kept around for audit purposes.
		if (Status == OpportunityStatus.Draft)
			return Result.Failure(Error.Conflict("VolunteerOpportunity.CannotCancelDraft", "A draft opportunity cannot be cancelled - delete it instead."));

		CancellationReason = reason;
		Status = OpportunityStatus.Cancelled;
		AddEvent(new VolunteerOpportunityCancelledDomainEvent(Id, OrganizationId, reason));
		return Result.Success();
	}

	public Result SetBannerImageUrl(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
			return Result.Failure(Error.Validation("VolunteerOpportunity.BannerImageUrlRequired", "Banner image URL must not be empty."));

		BannerImageUrl = url;
		return Result.Success();
	}

	public void ClearBannerImageUrl()
	{
		BannerImageUrl = null;
	}

	public void SetColor(string? color)
	{
		Color = color;
	}

	public Result Rename(string title)
	{
		var validTitleLength = EnsureValidTitleLength(title);
		if (validTitleLength.IsFailure)
			return validTitleLength;

		if (Status == OpportunityStatus.Published)
		{
			var publishable = EnsurePublishable(title, Description, IsRemote, Address);
			if (publishable.IsFailure)
				return publishable;
		}

		Title = title;
		return Result.Success();
	}

	public Result ChangeDescription(string description)
	{
		var validDescriptionLength = EnsureValidDescriptionLength(description);
		if (validDescriptionLength.IsFailure)
			return validDescriptionLength;

		if (Status == OpportunityStatus.Published)
		{
			var publishable = EnsurePublishable(Title, description, IsRemote, Address);
			if (publishable.IsFailure)
				return publishable;
		}

		Description = description;
		return Result.Success();
	}

	public Result Relocate(bool isRemote, Address? address)
	{
		if (Status == OpportunityStatus.Published)
		{
			var publishable = EnsurePublishable(Title, Description, isRemote, address);
			if (publishable.IsFailure)
				return publishable;
		}

		var addressTextChanged = AddressTextChanged(Address, address);
		var needsGeocoding = !isRemote && address is not null && addressTextChanged;

		IsRemote = isRemote;

		// Callers always supply a fresh, uncoordinated Address (built from raw
		// street/house number/zip/city text - see CreateVolunteerOpportunityEndpoint
		// / UpdateVolunteerOpportunityEndpoint), so only replace the current
		// Address when the location actually changed; otherwise keep whatever
		// coordinates and AddressGeocodingFailed state a previous geocoding
		// attempt produced instead of wiping them out on every unrelated edit.
		if (isRemote || address is null || addressTextChanged)
		{
			Address = address;
			AddressGeocodingFailed = false;
		}

		if (needsGeocoding)
			AddEvent(new VolunteerOpportunityGeocodingRequestedDomainEvent(Id));

		return Result.Success();
	}

	// Applies the outcome of an out-of-band geocoding attempt (see
	// GeocodeVolunteerOpportunityAddressHandler / GeocodingRetryJob) - distinct
	// from Relocate, which is for organizer-driven address edits and
	// deliberately skips re-resolving an unchanged address.
	public void ApplyGeocodingResult(Address resolvedAddress)
	{
		Address = resolvedAddress;
		AddressGeocodingFailed = false;
	}

	public void MarkAddressGeocodingFailed()
	{
		AddressGeocodingFailed = true;
	}

	private static bool AddressTextChanged(Address? prev, Address? next) =>
		prev?.Street != next?.Street ||
		prev?.HouseNumber != next?.HouseNumber ||
		prev?.ZipCode != next?.ZipCode ||
		prev?.City != next?.City;

	public void Reschedule(Occurrence occurrence)
	{
		Occurrence = occurrence;
	}

	public void Recategorize(Category? category, IReadOnlyCollection<string> tags)
	{
		Category = category;
		_tags = new List<string>(tags);
	}

	public Result ChangeCheckInMethod(CheckInMethod checkInMethod, IPinGenerator pinGenerator, string? checkInPin = null)
	{
		if (checkInMethod == CheckInMethod.PINCode && checkInPin is not null)
		{
			var validPin = EnsureValidPin(checkInPin);
			if (validPin.IsFailure)
				return validPin;
		}

		CheckInMethod = checkInMethod;
		if (checkInMethod == CheckInMethod.PINCode)
		{
			if (checkInPin is not null)
				CheckInPin = checkInPin;
			else if (CheckInPin is null)
				CheckInPin = pinGenerator.GeneratePin();
		}
		else
		{
			// Otherwise the old PIN stays live for anyone who saw it even after the
			// organizer deliberately turns PIN check-in off - and it would silently
			// come back into effect if PINCode is re-selected later without
			// supplying a fresh custom PIN, since the regeneration branch above only
			// fires when CheckInPin is null (#1165).
			CheckInPin = null;
		}

		return Result.Success();
	}

	public void SwitchParticipationType(ParticipationType participationType)
	{
		// Time slots are only meaningful for ScheduledSlots opportunities (see AddTimeSlot).
		// Clearing them when switching away prevents orphaned slots from lingering
		// once the opportunity no longer surfaces them. Callers must ensure no
		// active engagements reference these slots before switching away.
		if (ParticipationType == ParticipationType.ScheduledSlots && participationType != ParticipationType.ScheduledSlots)
			_timeSlots.Clear();

		// Mirror of the above for the other direction: ValidUntil is only
		// meaningful for IndividualContact opportunities (see SetValidUntil).
		if (ParticipationType == ParticipationType.IndividualContact && participationType != ParticipationType.IndividualContact)
			ValidUntil = null;

		ParticipationType = participationType;
	}

	public Result SetValidUntil(DateTimeOffset? validUntil, DateTimeOffset now)
	{
		var validation = EnsureValidValidUntil(ParticipationType, validUntil, now);
		if (validation.IsFailure)
			return validation;

		ValidUntil = validUntil;
		return Result.Success();
	}

	public Result<TimeSlot> AddTimeSlot(
		DateTimeOffset startDateTime,
		DateTimeOffset endDateTime,
		int? maxParticipants,
		DateTimeOffset now,
		Guid? seriesId = null,
		string? recurrenceFrequency = null,
		int? recurrenceCount = null)
	{
		if (ParticipationType != ParticipationType.ScheduledSlots)
			return Result.Failure<TimeSlot>(Error.Validation(
				"VolunteerOpportunity.TimeSlotNotAllowed",
				"Time slots can only be added to opportunities with Scheduled slots participation type."));

		var timeSlotResult = TimeSlot.Create(startDateTime, endDateTime, maxParticipants, now, seriesId, recurrenceFrequency, recurrenceCount);
		if (timeSlotResult.IsFailure)
			return timeSlotResult;

		_timeSlots.Add(timeSlotResult.Value);
		return timeSlotResult;
	}

	public Result UpdateTimeSlot(TimeSlotId timeSlotId, DateTimeOffset startDateTime, DateTimeOffset endDateTime, int? maxParticipants, DateTimeOffset now)
	{
		var timeSlot = _timeSlots.Find(ts => ts.Id == timeSlotId);
		if (timeSlot is null)
			return Result.Failure(Error.NotFound("VolunteerOpportunity.TimeSlotNotFound", $"Time slot with id '{timeSlotId.Value}' not found."));

		return timeSlot.Update(startDateTime, endDateTime, maxParticipants, now);
	}

	public Result UpdateTimeSlotCapacity(TimeSlotId timeSlotId, int? maxParticipants)
	{
		var timeSlot = _timeSlots.Find(ts => ts.Id == timeSlotId);
		if (timeSlot is null)
			return Result.Failure(Error.NotFound("VolunteerOpportunity.TimeSlotNotFound", $"Time slot with id '{timeSlotId.Value}' not found."));

		return timeSlot.UpdateCapacity(maxParticipants);
	}

	public Result RemoveTimeSlot(TimeSlotId timeSlotId)
	{
		var timeSlot = _timeSlots.Find(ts => ts.Id == timeSlotId);
		if (timeSlot is null)
			return Result.Failure(Error.NotFound("VolunteerOpportunity.TimeSlotNotFound", $"Time slot with id '{timeSlotId.Value}' not found."));

		_timeSlots.Remove(timeSlot);
		return Result.Success();
	}

	public Result MarkDeleted(DateTimeOffset deletedOn)
	{
		if (IsDeleted)
			return Result.Failure(Error.Conflict("VolunteerOpportunity.AlreadyDeleted", "Opportunity is already shadow-deleted."));

		IsDeleted = true;
		DeletedOn = deletedOn;
		return Result.Success();
	}

	public Result Restore()
	{
		if (!IsDeleted)
			return Result.Failure(Error.Conflict("VolunteerOpportunity.NotDeleted", "Opportunity is not shadow-deleted."));

		IsDeleted = false;
		DeletedOn = null;
		return Result.Success();
	}
}
