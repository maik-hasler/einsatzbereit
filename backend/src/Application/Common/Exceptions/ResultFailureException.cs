using Domain.Primitives;

namespace Application.Common.Exceptions;

public sealed class ResultFailureException(Error error) : Exception(error.Description)
{
	public Error Error { get; } = error;
}
