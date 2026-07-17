using Domain.Primitives;

namespace Domain.Organizations;

public readonly record struct OrganizationId : IValueObject
{
	public Guid Value { get; }

	private OrganizationId(Guid value) => Value = value;

	public static Result<OrganizationId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<OrganizationId>(Error.Validation("OrganizationId.Empty", "OrganizationId must not be empty."))
			: Result.Success(new OrganizationId(value));

	public static OrganizationId New() => new(Guid.CreateVersion7());
}
