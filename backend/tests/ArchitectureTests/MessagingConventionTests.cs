using Application.Common.Messaging;
using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;

namespace ArchitectureTests;

public sealed class MessagingConventionTests
{
    [Theory]
    [MemberData(nameof(TestData.CommandNamingSuffix), MemberType = typeof(TestData))]
    public void CommandImplementations_ShouldHaveNameEndingWith_Command(
        Type type,
        string expectedSuffix)
    {
        type.Name.Should().EndWith(expectedSuffix,
            $"{type.Name} should end with '{expectedSuffix}'");
    }
    
    [Theory]
    [MemberData(nameof(TestData.CommandHandlerNamingSuffix), MemberType = typeof(TestData))]
    public void CommandHandlerImplementations_ShouldHaveNameEndingWith_CommandHandler(
        Type type,
        string expectedSuffix)
    {
        type.Name.Should().EndWith(expectedSuffix,
            $"{type.Name} should end with '{expectedSuffix}'");
    }
    
    [Theory]
    [MemberData(nameof(TestData.QueryNamingSuffix), MemberType = typeof(TestData))]
    public void QueryImplementations_ShouldHaveNameEndingWith_Query(
        Type type,
        string expectedSuffix)
    {
        type.Name.Should().EndWith(expectedSuffix,
            $"{type.Name} should end with '{expectedSuffix}'");
    }
    
    [Theory]
    [MemberData(nameof(TestData.QueryHandlerNamingSuffix), MemberType = typeof(TestData))]
    public void QueryHandlerImplementations_ShouldHaveNameEndingWith_QueryHandler(
        Type type,
        string expectedSuffix)
    {
        type.Name.Should().EndWith(expectedSuffix,
            $"{type.Name} should end with '{expectedSuffix}'");
    }
    
    [Theory]
    [MemberData(nameof(TestData.HandlerAndCommandOrQueryPairs), MemberType = typeof(TestData))]
    public void Handlers_ShouldResideInSameNamespace_AsTheirCommandOrQuery(
        Type handlerType,
        Type commandOrQueryType)
    {
        handlerType.Namespace.Should().Be(
            commandOrQueryType.Namespace,
            $"{handlerType.Name} should be in the same namespace as {commandOrQueryType.Name}");
    }

    [Fact]
    public void NoType_ShouldDirectlyImplement_IRequest()
    {
        var types = Types
            .InAssembly(AssemblyAnchors.ApplicationLayer)
            .That()
            .ImplementInterface(typeof(IRequest<>))
            .GetTypes();

        var violators = types
            .Where(t => !t.IsInterface)
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
            .Where(t => !t.GetInterfaces().Any(i =>
                i.IsGenericType && (
                    i.GetGenericTypeDefinition() == typeof(ICommand<>) ||
                    i.GetGenericTypeDefinition() == typeof(IQuery<>))))
            .ToList();

        violators.Should().BeEmpty(
            "all types should implement ICommand<T> or IQuery<T> instead of IRequest<T> directly");
    }

    [Fact]
    public void NoType_ShouldDirectlyImplement_IRequestHandler()
    {
        var types = Types
            .InAssembly(AssemblyAnchors.ApplicationLayer)
            .That()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .GetTypes();

        var violators = types
            .Where(t => !t.IsInterface)
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            .Where(t => !t.GetInterfaces().Any(i =>
                i.IsGenericType && (
                    i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
                    i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))))
            .ToList();

        violators.Should().BeEmpty(
            "all types should implement ICommandHandler<,> or IQueryHandler<,> instead of IRequestHandler<,> directly");
    }
}
