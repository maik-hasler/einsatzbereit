using Domain.Organizations;
using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed class VolunteerOpportunity
	: AggregateRoot<VolunteerOpportunityId>,
		IAuditableEntity
{
	private readonly List<TimeSlot> _timeSlots = [];

	public OrganizationId OrganizationId { get; private set; }

	public string Title { get; private set; }

	public string Description { get; private set; }

	public bool IsRemote { get; private set; }

	public Address? Address { get; private set; }

	public Occurrence Occurrence { get; private set; }

	public ParticipationType ParticipationType { get; private set; }

	public CheckInMethod CheckInMethod { get; private set; }

	public Category? Category { get; private set; }

	public List<string> Tags { get; private set; } = [];

	public OpportunityStatus Status { get; private set; }

	public string? BannerImageUrl { get; private set; }

	public string? Color { get; private set; }

	public string? CheckInPin { get; private set; }

	public IReadOnlyCollection<TimeSlot> TimeSlots => _timeSlots.AsReadOnly();

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

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
		List<string> tags,
		OpportunityStatus status)
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
		Tags = tags;
		Status = status;
		if (checkInMethod == CheckInMethod.PINCode)
			CheckInPin = GeneratePin();
	}

	private static string GeneratePin() =>
		Random.Shared.Next(1000, 10000).ToString("D4");

	public static VolunteerOpportunity Create(
		OrganizationId organizationId,
		string title,
		string description,
		bool isRemote,
		Address? address,
		Occurrence occurrence,
		ParticipationType participationType,
		CheckInMethod checkInMethod,
		Category? category = null,
		List<string>? tags = null,
		OpportunityStatus status = OpportunityStatus.Published)
	{
		if (string.IsNullOrWhiteSpace(title))
			throw new DomainException("Title must not be empty.");

		if (status == OpportunityStatus.Published)
			EnsurePublishable(description, isRemote, address);

		return new VolunteerOpportunity(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
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
			status);
	}

	private static void EnsurePublishable(
		string description,
		bool isRemote,
		Address? address)
	{
		if (string.IsNullOrWhiteSpace(description))
			throw new DomainException("Description must not be empty.");

		if (!isRemote && address is null)
			throw new DomainException("Address is required for non-remote opportunities.");
	}

	public void Publish()
	{
		if (Status == OpportunityStatus.Published)
			throw new DomainException("Opportunity is already published.");

		EnsurePublishable(Description, IsRemote, Address);

		if (ParticipationType == ParticipationType.Waitlist && _timeSlots.Count == 0)
			throw new DomainException("A Waitlist opportunity must have at least one time slot before it can be published.");

		Status = OpportunityStatus.Published;
	}

	public void SetBannerImageUrl(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
			throw new DomainException("Banner image URL must not be empty.");

		BannerImageUrl = url;
	}

	public void ClearBannerImageUrl()
	{
		BannerImageUrl = null;
	}

	public void SetColor(string? color)
	{
		Color = color;
	}

	public void Update(
		string title,
		string description,
		bool isRemote,
		Address? address,
		Occurrence occurrence,
		ParticipationType participationType,
		CheckInMethod checkInMethod,
		Category? category,
		List<string> tags)
	{
		if (string.IsNullOrWhiteSpace(title))
			throw new DomainException("Title must not be empty.");

		if (Status == OpportunityStatus.Published)
			EnsurePublishable(description, isRemote, address);

		Title = title;
		Description = description;
		IsRemote = isRemote;
		Address = address;
		Occurrence = occurrence;
		ParticipationType = participationType;
		CheckInMethod = checkInMethod;
		Category = category;
		Tags = tags;
	}

	public TimeSlot AddTimeSlot(
		DateTimeOffset startDateTime,
		DateTimeOffset endDateTime,
		int maxParticipants)
	{
		if (ParticipationType != ParticipationType.Waitlist)
			throw new DomainException("Time slots can only be added to opportunities with Waitlist participation type.");

		var timeSlot = TimeSlot.Create(startDateTime, endDateTime, maxParticipants);
		_timeSlots.Add(timeSlot);
		return timeSlot;
	}

	public void UpdateTimeSlot(TimeSlotId timeSlotId, DateTimeOffset startDateTime, DateTimeOffset endDateTime, int maxParticipants)
	{
		var timeSlot = _timeSlots.Find(ts => ts.Id == timeSlotId)
			?? throw new DomainException($"Time slot with id '{timeSlotId.Value}' not found.");

		timeSlot.Update(startDateTime, endDateTime, maxParticipants);
	}

	public void RemoveTimeSlot(TimeSlotId timeSlotId)
	{
		var timeSlot = _timeSlots.Find(ts => ts.Id == timeSlotId)
			?? throw new DomainException($"Time slot with id '{timeSlotId.Value}' not found.");

		_timeSlots.Remove(timeSlot);
	}
}
