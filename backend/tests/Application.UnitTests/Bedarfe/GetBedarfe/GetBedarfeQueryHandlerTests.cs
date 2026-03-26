using Application.Bedarfe.GetBedarfe.v1;
using AwesomeAssertions;
using Domain.Bedarfe;
using Domain.Primitives;
using NSubstitute;
using Xunit;

namespace Application.UnitTests.Bedarfe.GetBedarfe;

public class GetBedarfeQueryHandlerTests
{
    private readonly IBedarfRepository _bedarfRepository = Substitute.For<IBedarfRepository>();
    private readonly GetBedarfeQueryHandler _sut;

    public GetBedarfeQueryHandlerTests()
    {
        _sut = new GetBedarfeQueryHandler(_bedarfRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnPagedList_WhenRepositoryReturnsData()
    {
        // Arrange
        var bedarfe = new List<Bedarf>
        {
            Bedarf.Create("Bedarf 1", "Beschreibung 1"),
            Bedarf.Create("Bedarf 2", "Beschreibung 2")
        };
        var pagedList = new PagedList<Bedarf>(bedarfe, 2, 1, 10);
        var query = new GetBedarfeQuery(1, 10);

        _bedarfRepository
            .GetAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(pagedList);

        // Act
        var result = await _sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.TotalItems.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.CurrentPage.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldPassCorrectParameters_WhenCalled()
    {
        // Arrange
        var pagedList = new PagedList<Bedarf>([], 0, 3, 5);
        var query = new GetBedarfeQuery(3, 5);

        _bedarfRepository
            .GetAsync(3, 5, Arg.Any<CancellationToken>())
            .Returns(pagedList);

        // Act
        await _sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        await _bedarfRepository
            .Received(1)
            .GetAsync(3, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPagedList_WhenRepositoryReturnsEmpty()
    {
        // Arrange
        var pagedList = new PagedList<Bedarf>([], 0, 1, 10);
        var query = new GetBedarfeQuery(1, 10);

        _bedarfRepository
            .GetAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(pagedList);

        // Act
        var result = await _sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.TotalItems.Should().Be(0);
        result.Items.Should().BeEmpty();
    }
}
