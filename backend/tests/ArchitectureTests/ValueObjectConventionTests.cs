using AwesomeAssertions;
using Domain.Primitives;

namespace ArchitectureTests;

public sealed class ValueObjectConventionTests
{
	// IValueObject is a marker interface (#1014) - without an enforced rule, a new
	// Id-suffixed value object can silently forget to implement it and CI stays green.
	[Test]
	public void IdSuffixedTypes_ShouldImplement_IValueObject()
	{
		var violators = AssemblyAnchors.DomainLayer
			.GetTypes()
			.Where(t => t.Name.EndsWith("Id", StringComparison.Ordinal))
			.Where(t => !t.IsInterface)
			.Where(t => !typeof(IValueObject).IsAssignableFrom(t))
			.ToList();

		violators.Should().BeEmpty(
			$"every Id-suffixed value object under Domain should implement IValueObject, but found: {string.Join(", ", violators.Select(t => t.Name))}");
	}
}
