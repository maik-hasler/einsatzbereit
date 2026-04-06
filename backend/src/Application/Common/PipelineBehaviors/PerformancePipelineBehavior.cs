using System.Diagnostics;
using Application.Common.Messaging;
using Microsoft.Extensions.Logging;

namespace Application.Common.PipelineBehaviors;

internal sealed class PerformancePipelineBehavior<TRequest, TResponse>(
    ILogger<PerformancePipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const double ThresholdMs = 500;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        Func<ValueTask<TResponse>> next,
        CancellationToken cancellationToken = default)
    {
        var start = Stopwatch.GetTimestamp();

        var response = await next().ConfigureAwait(false);

        var elapsed = Stopwatch.GetElapsedTime(start);

        if (elapsed.TotalMilliseconds <= ThresholdMs)
        {
            return response;
        }

        logger.LogWarning(
            "Request {RequestName} exceeded {Threshold} ms. Took {ElapsedMilliseconds} ms",
            typeof(TRequest).Name,
            ThresholdMs,
            elapsed.TotalMilliseconds);

        return response;
    }
}