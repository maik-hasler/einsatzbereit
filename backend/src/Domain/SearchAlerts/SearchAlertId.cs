using Domain.Primitives;

namespace Domain.SearchAlerts;

public readonly record struct SearchAlertId : IValueObject
{
	public Guid Value { get; }

	private SearchAlertId(Guid value) => Value = value;

	public static Result<SearchAlertId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<SearchAlertId>(Error.Validation("SearchAlertId.Empty", "SearchAlertId must not be empty."))
			: Result.Success(new SearchAlertId(value));

	public static SearchAlertId New() => new(Guid.CreateVersion7());
}
