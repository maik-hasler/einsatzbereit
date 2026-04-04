using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;

namespace ArchitectureTests;

public sealed class ArchitectureLayerTests
{
    [Fact]
    public void DomainLayer_ShouldNotDependOn_ApplicationLayer()
    {
        var result = Types
            .InAssembly(AssemblyAnchors.DomainLayer)
            .Should()
            .NotHaveDependencyOn(AssemblyAnchors.ApplicationLayerAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainLayer_ShouldNotDependOn_InfrastructureLayer()
    {
        var result = Types
            .InAssembly(AssemblyAnchors.DomainLayer)
            .Should()
            .NotHaveDependencyOn(AssemblyAnchors.InfrastructureLayerAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainLayer_ShouldNotDependOn_PresentationLayer()
    {
        var result = Types
            .InAssembly(AssemblyAnchors.DomainLayer)
            .Should()
            .NotHaveDependencyOn(AssemblyAnchors.PresentationLayerAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ApplicationLayer_ShouldNotDependOn_InfrastructureLayer()
    {
        var result = Types
            .InAssembly(AssemblyAnchors.ApplicationLayer)
            .Should()
            .NotHaveDependencyOn(AssemblyAnchors.InfrastructureLayerAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ApplicationLayer_ShouldNotDependOn_PresentationLayer()
    {
        var result = Types
            .InAssembly(AssemblyAnchors.ApplicationLayer)
            .Should()
            .NotHaveDependencyOn(AssemblyAnchors.PresentationLayerAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void InfrastructureLayer_ShouldNotDependOn_PresentationLayer()
    {
        var result = Types
            .InAssembly(AssemblyAnchors.InfrastructureLayer)
            .Should()
            .NotHaveDependencyOn(AssemblyAnchors.PresentationLayerAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
