using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<IntegrationTestsParallelLimit>]

public sealed class IntegrationTestsParallelLimit : IParallelLimit
{
	public int Limit => Math.Max(1, Environment.ProcessorCount - 2);
}
