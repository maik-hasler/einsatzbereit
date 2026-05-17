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
		CheckInMethod checkInMethod)
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
		CheckInMethod checkInMethod)
	{
		if (string.IsNullOrWhiteSpace(title))
			throw new DomainException("Title must not be empty.");

		if (string.IsNullOrWhiteSpace(description))
			throw new DomainException("Description must not be empty.");

		if (!isRemote && address is null)
			throw new DomainException("Address is required for non-remote opportunities.");

		return new VolunteerOpportunity(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			organizationId,
			title,
			description,
			isRemote,
			address,
			occurrence,
			participationType,
			checkInMethod);
	}

	public void Update(
		string title,
		string description,
		bool isRemote,
		Address? address,
		Occurrence occurrence,
		ParticipationType participationType,
		CheckInMethod checkInMethod)
	{
		if (string.IsNullOrWhiteSpace(title))
			throw new DomainException("Title must not be empty.");

		if (string.IsNullOrWhiteSpace(description))
			throw new DomainException("Description must not be empty.");

		if (!isRemote && address is null)
			throw new DomainException("Address is required for non-remote opportunities.");

		Title = title;
		Description = description;
		IsRemote = isRemote;
		Address = address;
		Occurrence = occurrence;
		ParticipationType = participationType;
		CheckInMethod = checkInMethod;
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
