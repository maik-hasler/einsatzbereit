using AwesomeAssertions;
using NetArchTest.Rules;

namespace ArchitectureTests;

public sealed class ArchitectureLayerTests
{
    [Test]
    public void DomainLayer_ShouldNotDependOn_ApplicationLayer()
    {
        var result = Types
            .InAssembly(AssemblyAnchors.DomainLayer)
            .Should()
            .NotHaveDependencyOn(AssemblyAnchors.ApplicationLayerAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Test]
    public void DomainLayer_ShouldNotDependOn_InfrastructureLayer()
    {
        var result = Types
            .InAssembly(AssemblyAnchors.DomainLayer)
            .Should()
            .NotHaveDependencyOn(AssemblyAnchors.InfrastructureLayerAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Test]
    public void DomainLayer_ShouldNotDependOn_PresentationLayer()
    {
        var result = Types
            .InAssembly(AssemblyAnchors.DomainLayer)
            .Should()
            .NotHaveDependencyOn(AssemblyAnchors.PresentationLayerAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Test]
    public void ApplicationLayer_ShouldNotDependOn_InfrastructureLayer()
    {
        var result = Types
            .InAssembly(AssemblyAnchors.ApplicationLayer)
            .Should()
            .NotHaveDependencyOn(AssemblyAnchors.InfrastructureLayerAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Test]
    public void ApplicationLayer_ShouldNotDependOn_PresentationLayer()
    {
        var result = Types
            .InAssembly(AssemblyAnchors.ApplicationLayer)
            .Should()
            .NotHaveDependencyOn(AssemblyAnchors.PresentationLayerAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Test]
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
