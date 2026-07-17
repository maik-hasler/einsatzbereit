using Domain.Primitives;

namespace Application.Common.Exceptions;

public static class ResultExtensions
{
	public static TValue GetValueOrThrow<TValue>(this Result<TValue> result) =>
		result.IsSuccess ? result.Value : throw new ResultFailureException(result.Error);

	public static void ThrowIfFailure(this Result result)
	{
		if (result.IsFailure)
			throw new ResultFailureException(result.Error);
	}
}
