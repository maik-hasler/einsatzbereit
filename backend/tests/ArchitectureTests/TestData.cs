using System.Reflection;
using Api.Common.Endpoints;
using Application.Common.Messaging;

namespace ArchitectureTests;

internal static class TestData
{
    public static IEnumerable<(Type, Type)> HandlerAndCommandOrQueryPairs =>
        TypeDiscovery.GetImplementationPairs(
            AssemblyAnchors.ApplicationLayer,
            typeof(ICommandHandler<,>),
            typeof(IQueryHandler<,>));

    public static IEnumerable<(Type, string)> CommandNamingSuffix =>
        TypeDiscovery.GetImplementationsWithExpectedSuffix(
            AssemblyAnchors.ApplicationLayer, typeof(ICommand<>), "Command");

    public static IEnumerable<(Type, string)> QueryNamingSuffix =>
        TypeDiscovery.GetImplementationsWithExpectedSuffix(
            AssemblyAnchors.ApplicationLayer, typeof(IQuery<>), "Query");

    public static IEnumerable<(Type, string)> CommandHandlerNamingSuffix =>
        TypeDiscovery.GetImplementationsWithExpectedSuffix(
            AssemblyAnchors.ApplicationLayer, typeof(ICommandHandler<,>), "CommandHandler");

    public static IEnumerable<(Type, string)> QueryHandlerNamingSuffix =>
        TypeDiscovery.GetImplementationsWithExpectedSuffix(
            AssemblyAnchors.ApplicationLayer, typeof(IQueryHandler<,>), "QueryHandler");

    public static IEnumerable<(Type, string)> EndpointNamingSuffix =>
        TypeDiscovery.GetImplementationsWithExpectedSuffix(
            AssemblyAnchors.PresentationLayer, typeof(IEndpoint), "Endpoint");
}
