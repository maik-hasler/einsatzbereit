using AwesomeAssertions;

namespace ArchitectureTests;

public sealed class EndpointConventionTests
{
    [Test]
    [MethodDataSource(typeof(TestData), nameof(TestData.EndpointNamingSuffix))]
    public void EndpointImplementations_ShouldHaveNameEndingWith_Endpoint(
        Type type,
        string expectedSuffix)
    {
        type.Name.Should().EndWith(expectedSuffix,
            $"{type.Name} should end with '{expectedSuffix}'");
    }
}
