using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public readonly record struct VolunteerOpportunityId : IValueObject
{
	public Guid Value { get; }

	private VolunteerOpportunityId(Guid value) => Value = value;

	public static Result<VolunteerOpportunityId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<VolunteerOpportunityId>(Error.Validation("VolunteerOpportunityId.Empty", "VolunteerOpportunityId must not be empty."))
			: Result.Success(new VolunteerOpportunityId(value));

	public static VolunteerOpportunityId New() => new(Guid.CreateVersion7());
}
