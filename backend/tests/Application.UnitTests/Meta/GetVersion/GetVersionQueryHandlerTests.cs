using Application.Meta;
using Application.Meta.GetVersion.v1;
using AwesomeAssertions;
using NSubstitute;

namespace Application.UnitTests.Meta.GetVersion;

public class GetVersionQueryHandlerTests
{
	private readonly IVersionProvider _versionProvider = Substitute.For<IVersionProvider>();
	private readonly GetVersionQueryHandler _sut;

	public GetVersionQueryHandlerTests()
	{
		_sut = new GetVersionQueryHandler(_versionProvider);
	}

	[Test]
	public async Task Handle_ShouldReturnTheVersionProviderResult(CancellationToken cancellationToken)
	{
		_versionProvider.GetVersion().Returns("1.2.3-rc.1");

		var version = await _sut.Handle(new GetVersionQuery(), cancellationToken);

		version.Should().Be("1.2.3-rc.1");
	}
}
