using Api.Common.Endpoints;
using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;

namespace ArchitectureTests;

public sealed class NamingConventionTests
{
    [Fact]
    public void EndpointImplementations_ShouldHaveNameEndingWith_Endpoint()
    {
        var result = Types
            .InAssembly(AssemblyAnchors.PresentationLayer)
            .That()
            .ImplementInterface(typeof(IEndpoint))
            .Should()
            .HaveNameEndingWith("Endpoint")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
