using System.Reflection;
using Xunit;

namespace ArchitectureTests;

internal static class TypeDiscovery
{
    /// <summary>
    /// Finds concrete types implementing any of the given generic interface definitions
    /// and pairs each with the first generic argument of that interface.
    /// </summary>
    public static TheoryData<Type, Type> GetImplementationPairs(
        Assembly assembly,
        params Type[] genericInterfaceDefinitions)
    {
        var pairs = new TheoryData<Type, Type>();

        foreach (var (implementation, firstTypeArg) in assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false, DeclaringType: null })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && genericInterfaceDefinitions.Contains(i.GetGenericTypeDefinition()))
                .Select(i => (Implementation: t, FirstTypeArg: i.GetGenericArguments()[0]))))
        {
            pairs.Add(implementation, firstTypeArg);
        }

        return pairs;
    }

    /// <summary>
    /// Finds concrete types implementing the given interface and pairs each with the expected naming suffix.
    /// </summary>
    public static TheoryData<Type, string> GetImplementationsWithExpectedSuffix(
        Assembly assembly,
        Type interfaceType,
        string expectedSuffix)
    {
        var data = new TheoryData<Type, string>();

        foreach (var type in assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false, DeclaringType: null })
            .Where(t => t.GetInterfaces().Any(i =>
                i == interfaceType ||
                (i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType))))
        {
            data.Add(type, expectedSuffix);
        }

        return data;
    }
}
