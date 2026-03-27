using Application.Messaging;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Application.UnitTests.Messaging;

public class SenderTests
{
    [Fact]
    public async Task Send_ShouldReturnResponse_WhenHandlerExists()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TestRequest, string>, TestHandler>();
        services.AddScoped<ISender, Sender>();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var request = new TestRequest("Hello");

        // Act
        var result = await sender.Send(request, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("Handled: Hello");
    }

    [Fact]
    public async Task Send_ShouldThrowException_WhenHandlerIsMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ISender, Sender>();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var request = new TestRequest("Hello");

        // Act
        Func<Task> act = async () => await sender.Send(request, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No service for type 'Application.Messaging.IRequestHandler*");
    }

    [Fact]
    public async Task Send_ShouldExecutePipelineBehavior_WhenBehaviorIsRegistered()
    {
        // Arrange
        var log = new List<string>();

        var services = new ServiceCollection();
        services.AddSingleton(log);

        services.AddScoped<IRequestHandler<TestRequest, string>, TestHandler>();

        services.AddScoped<IPipelineBehavior<TestRequest, string>>(
            sp => new TestBehavior(log));

        services.AddScoped<ISender, Sender>();

        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        // Act
        await sender.Send(new TestRequest("Hello"), TestContext.Current.CancellationToken);

        // Assert
        log.Should().ContainInOrder("Before", "After");
    }

    [Fact]
    public async Task Send_ShouldRespectPipelineOrder_WhenMultipleBehaviorsAreRegistered()
    {
        // Arrange
        var log = new List<string>();

        var services = new ServiceCollection();
        services.AddSingleton(log);

        services.AddScoped<IRequestHandler<TestRequest, string>, TestHandler>();

        services.AddScoped<IPipelineBehavior<TestRequest, string>>(
            _ => new NamedBehavior("A", log));

        services.AddScoped<IPipelineBehavior<TestRequest, string>>(
            _ => new NamedBehavior("B", log));

        services.AddScoped<ISender, Sender>();

        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        // Act
        await sender.Send(new TestRequest("Hello"), TestContext.Current.CancellationToken);

        // Assert
        log.Should().Equal(
            "A:Before",
            "B:Before",
            "B:After",
            "A:After");
    }

    private sealed record TestRequest(string Input) : IRequest<string>;

    private sealed class TestHandler : IRequestHandler<TestRequest, string>
    {
        public ValueTask<string> Handle(TestRequest request, CancellationToken ct)
        {
            return ValueTask.FromResult($"Handled: {request.Input}");
        }
    }

    private sealed class TestBehavior
        : IPipelineBehavior<TestRequest, string>
    {
        private readonly List<string> _log;

        public TestBehavior(List<string> log)
        {
            _log = log;
        }

        public async ValueTask<string> Handle(
            TestRequest request,
            Func<ValueTask<string>> next,
            CancellationToken cancellationToken)
        {
            _log.Add("Before");

            var result = await next();

            _log.Add("After");

            return result;
        }
    }

    private sealed class NamedBehavior
        : IPipelineBehavior<TestRequest, string>
    {
        private readonly string _name;
        private readonly List<string> _log;

        public NamedBehavior(string name, List<string> log)
        {
            _name = name;
            _log = log;
        }

        public async ValueTask<string> Handle(
            TestRequest request,
            Func<ValueTask<string>> next,
            CancellationToken cancellationToken)
        {
            _log.Add($"{_name}:Before");

            var result = await next();

            _log.Add($"{_name}:After");

            return result;
        }
    }
}