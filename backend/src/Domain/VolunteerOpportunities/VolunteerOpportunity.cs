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

	public Occurrence Occurrence { get; private set; }

	public ParticipationType ParticipationType { get; private set; }

	public CheckInMethod CheckInMethod { get; private set; }

	public Category? Category { get; private set; }

	public IReadOnlyList<string> Tags => _tags.AsReadOnly();

	public OpportunityStatus Status { get; private set; }

	public string? BannerImageUrl { get; private set; }

	public string? Color { get; private set; }

	public string? CheckInPin { get; private set; }

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
		string? checkInPin)
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
		string? checkInPin = null)
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

		if (status == OpportunityStatus.Published)
		{
			var publishable = EnsurePublishable(title, description, isRemote, address);
			if (publishable.IsFailure)
				return Result.Failure<VolunteerOpportunity>(publishable.Error);

			// Time slots can only be added after the aggregate is created (see
			// AddTimeSlot), so a Waitlist opportunity can never satisfy the
			// "at least one time slot" rule at construction time. Callers must
			// create it as a Draft, add slots, then call Publish().
			if (participationType == ParticipationType.Waitlist)
				return Result.Failure<VolunteerOpportunity>(Error.Validation(
					"VolunteerOpportunity.WaitlistMustStartAsDraft",
					"A Waitlist opportunity must be created as a draft and published after adding at least one time slot."));
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
			checkInPin);

		opportunity.AddEvent(new VolunteerOpportunityCreatedDomainEvent(opportunity.Id, organizationId));
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

		var publishable = EnsurePublishable(Title, Description, IsRemote, Address);
		if (publishable.IsFailure)
			return publishable;

		if (ParticipationType == ParticipationType.Waitlist && _timeSlots.Count == 0)
			return Result.Failure(Error.Validation(
				"VolunteerOpportunity.WaitlistRequiresTimeSlot",
				"A Waitlist opportunity must have at least one time slot before it can be published."));

		Status = OpportunityStatus.Published;
		AddEvent(new VolunteerOpportunityPublishedDomainEvent(Id, OrganizationId));
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

		IsRemote = isRemote;
		Address = address;
		return Result.Success();
	}

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

		return Result.Success();
	}

	public void SwitchParticipationType(ParticipationType participationType)
	{
		// Time slots are only meaningful for Waitlist opportunities (see AddTimeSlot).
		// Clearing them when switching away prevents orphaned slots from lingering
		// once the opportunity no longer surfaces them. Callers must ensure no
		// active engagements reference these slots before switching away.
		if (participationType != ParticipationType.Waitlist && ParticipationType == ParticipationType.Waitlist)
			_timeSlots.Clear();

		ParticipationType = participationType;
	}

	public Result<TimeSlot> AddTimeSlot(
		DateTimeOffset startDateTime,
		DateTimeOffset endDateTime,
		int maxParticipants,
		DateTimeOffset now)
	{
		if (ParticipationType != ParticipationType.Waitlist)
			return Result.Failure<TimeSlot>(Error.Validation(
				"VolunteerOpportunity.TimeSlotNotAllowed",
				"Time slots can only be added to opportunities with Waitlist participation type."));

		var timeSlotResult = TimeSlot.Create(startDateTime, endDateTime, maxParticipants, now);
		if (timeSlotResult.IsFailure)
			return timeSlotResult;

		_timeSlots.Add(timeSlotResult.Value);
		return timeSlotResult;
	}

	public Result UpdateTimeSlot(TimeSlotId timeSlotId, DateTimeOffset startDateTime, DateTimeOffset endDateTime, int maxParticipants, DateTimeOffset now)
	{
		var timeSlot = _timeSlots.Find(ts => ts.Id == timeSlotId);
		if (timeSlot is null)
			return Result.Failure(Error.NotFound("VolunteerOpportunity.TimeSlotNotFound", $"Time slot with id '{timeSlotId.Value}' not found."));

		return timeSlot.Update(startDateTime, endDateTime, maxParticipants, now);
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
		AddEvent(new VolunteerOpportunityDeletedDomainEvent(Id, OrganizationId));
		return Result.Success();
	}

	public Result Restore()
	{
		if (!IsDeleted)
			return Result.Failure(Error.Conflict("VolunteerOpportunity.NotDeleted", "Opportunity is not shadow-deleted."));

		IsDeleted = false;
		DeletedOn = null;
		AddEvent(new VolunteerOpportunityRestoredDomainEvent(Id, OrganizationId));
		return Result.Success();
	}
}
