namespace Domain.Primitives;

public record Result : IValueObject
{
	public bool IsSuccess { get; }

	public bool IsFailure => !IsSuccess;

	public Error Error { get; }

	protected Result(bool isSuccess, Error error)
	{
		if (isSuccess && error != Error.None)
			throw new InvalidOperationException("A successful result cannot contain an error.");

		if (!isSuccess && error == Error.None)
			throw new InvalidOperationException("A failed result must contain an error.");

		IsSuccess = isSuccess;
		Error = error;
	}

	public static Result Success() => new(true, Error.None);

	public static Result Failure(Error error) => new(false, error);

	public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

	public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

public sealed record Result<TValue> : Result
{
	private readonly TValue? _value;

	internal Result(TValue? value, bool isSuccess, Error error)
		: base(isSuccess, error) =>
		_value = value;

	public TValue Value => IsSuccess
		? _value!
		: throw new InvalidOperationException("The value of a failed result cannot be accessed.");

	public static implicit operator Result<TValue>(TValue value) => Success(value);
}
