using System.Reflection;

namespace ArchitectureTests;

internal static class TypeDiscovery
{
	public static IEnumerable<(Type Implementation, Type FirstTypeArg)> GetImplementationPairs(
		Assembly assembly,
		params Type[] genericInterfaceDefinitions)
	{
		return assembly.GetTypes()
			.Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false, DeclaringType: null })
			.SelectMany(t => t.GetInterfaces()
				.Where(i => i.IsGenericType && genericInterfaceDefinitions.Contains(i.GetGenericTypeDefinition()))
				.Select(i => (Implementation: t, FirstTypeArg: i.GetGenericArguments()[0])));
	}

	public static IEnumerable<(Type Type, string ExpectedSuffix)> GetImplementationsWithExpectedSuffix(
		Assembly assembly,
		Type interfaceType,
		string expectedSuffix)
	{
		return assembly.GetTypes()
			.Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false, DeclaringType: null })
			.Where(t => t.GetInterfaces().Any(i =>
				i == interfaceType ||
				(i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType)))
			.Select(t => (t, expectedSuffix));
	}
}
