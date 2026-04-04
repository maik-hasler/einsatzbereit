using System.Reflection;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Xunit;

namespace ArchitectureTests;

internal static class TestData
{
    public static TheoryData<Type, Type> HandlerAndCommandOrQueryPairs =>
        TypeDiscovery.GetImplementationPairs(
            AssemblyAnchors.ApplicationLayer,
            typeof(ICommandHandler<,>),
            typeof(IQueryHandler<,>));

    public static TheoryData<Type, string> CommandNamingSuffix =>
        TypeDiscovery.GetImplementationsWithExpectedSuffix(
            AssemblyAnchors.ApplicationLayer, typeof(ICommand<>), "Command");

    public static TheoryData<Type, string> QueryNamingSuffix =>
        TypeDiscovery.GetImplementationsWithExpectedSuffix(
            AssemblyAnchors.ApplicationLayer, typeof(IQuery<>), "Query");

    public static TheoryData<Type, string> CommandHandlerNamingSuffix =>
        TypeDiscovery.GetImplementationsWithExpectedSuffix(
            AssemblyAnchors.ApplicationLayer, typeof(ICommandHandler<,>), "CommandHandler");

    public static TheoryData<Type, string> QueryHandlerNamingSuffix =>
        TypeDiscovery.GetImplementationsWithExpectedSuffix(
            AssemblyAnchors.ApplicationLayer, typeof(IQueryHandler<,>), "QueryHandler");

    public static TheoryData<Type, string> EndpointNamingSuffix =>
        TypeDiscovery.GetImplementationsWithExpectedSuffix(
            AssemblyAnchors.PresentationLayer, typeof(IEndpoint), "Endpoint");
}
