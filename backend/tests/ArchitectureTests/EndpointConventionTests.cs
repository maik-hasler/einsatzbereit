using AwesomeAssertions;
using Xunit;

namespace ArchitectureTests;

public sealed class EndpointConventionTests
{
    [Theory]
    [MemberData(nameof(TestData.EndpointNamingSuffix), MemberType = typeof(TestData))]
    public void EndpointImplementations_ShouldHaveNameEndingWith_Endpoint(
        Type type,
        string expectedSuffix)
    {
        type.Name.Should().EndWith(expectedSuffix,
            $"{type.Name} should end with '{expectedSuffix}'");
    }
}
