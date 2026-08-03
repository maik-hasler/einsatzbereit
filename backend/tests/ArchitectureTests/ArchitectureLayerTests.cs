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

		result.FailingTypeNames.Should().BeNullOrEmpty(
			$"Domain must not depend on {AssemblyAnchors.ApplicationLayerAssemblyName}");
	}

	[Test]
	public void DomainLayer_ShouldNotDependOn_InfrastructureLayer()
	{
		var result = Types
			.InAssembly(AssemblyAnchors.DomainLayer)
			.Should()
			.NotHaveDependencyOn(AssemblyAnchors.InfrastructureLayerAssemblyName)
			.GetResult();

		result.FailingTypeNames.Should().BeNullOrEmpty(
			$"Domain must not depend on {AssemblyAnchors.InfrastructureLayerAssemblyName}");
	}

	[Test]
	public void DomainLayer_ShouldNotDependOn_PresentationLayer()
	{
		var result = Types
			.InAssembly(AssemblyAnchors.DomainLayer)
			.Should()
			.NotHaveDependencyOn(AssemblyAnchors.PresentationLayerAssemblyName)
			.GetResult();

		result.FailingTypeNames.Should().BeNullOrEmpty(
			$"Domain must not depend on {AssemblyAnchors.PresentationLayerAssemblyName}");
	}

	[Test]
	public void ApplicationLayer_ShouldNotDependOn_InfrastructureLayer()
	{
		var result = Types
			.InAssembly(AssemblyAnchors.ApplicationLayer)
			.Should()
			.NotHaveDependencyOn(AssemblyAnchors.InfrastructureLayerAssemblyName)
			.GetResult();

		result.FailingTypeNames.Should().BeNullOrEmpty(
			$"Application must not depend on {AssemblyAnchors.InfrastructureLayerAssemblyName}");
	}

	[Test]
	public void ApplicationLayer_ShouldNotDependOn_PresentationLayer()
	{
		var result = Types
			.InAssembly(AssemblyAnchors.ApplicationLayer)
			.Should()
			.NotHaveDependencyOn(AssemblyAnchors.PresentationLayerAssemblyName)
			.GetResult();

		result.FailingTypeNames.Should().BeNullOrEmpty(
			$"Application must not depend on {AssemblyAnchors.PresentationLayerAssemblyName}");
	}

	[Test]
	public void InfrastructureLayer_ShouldNotDependOn_PresentationLayer()
	{
		var result = Types
			.InAssembly(AssemblyAnchors.InfrastructureLayer)
			.Should()
			.NotHaveDependencyOn(AssemblyAnchors.PresentationLayerAssemblyName)
			.GetResult();

		result.FailingTypeNames.Should().BeNullOrEmpty(
			$"Infrastructure must not depend on {AssemblyAnchors.PresentationLayerAssemblyName}");
	}
}
