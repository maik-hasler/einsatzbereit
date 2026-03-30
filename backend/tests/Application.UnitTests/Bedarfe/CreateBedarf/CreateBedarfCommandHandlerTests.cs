using Application.Abstractions;
using Application.Bedarfe.CreateBedarf.v1;
using AwesomeAssertions;
using Domain.Bedarfe;
using Domain.Organisationen;
using NSubstitute;
using Xunit;

namespace Application.UnitTests.Bedarfe.CreateBedarf;

public class CreateBedarfCommandHandlerTests
{
    private static readonly OrganisationId TestOrganisationId = new(Guid.NewGuid());
    private static readonly Adresse TestAdresse = new("Musterstraße", "1", "12345", "Berlin");

    private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateBedarfCommandHandler _sut;

    public CreateBedarfCommandHandlerTests()
    {
        _sut = new CreateBedarfCommandHandler(_dbContext, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldCreateAndPersistBedarf_WithCorrectData()
    {
        // Arrange
        var command = new CreateBedarfCommand(
            "Helfer gesucht",
            "Für den Umzug",
            TestOrganisationId,
            TestAdresse,
            Frequenz.Einmalig);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Title.Should().Be("Helfer gesucht");
        result.Description.Should().Be("Für den Umzug");
        result.OrganisationId.Should().Be(TestOrganisationId);
        result.Adresse.Should().Be(TestAdresse);
        result.Frequenz.Should().Be(Frequenz.Einmalig);
    }

    [Fact]
    public async Task Handle_ShouldCallRepositoryAndUnitOfWork()
    {
        // Arrange
        var command = new CreateBedarfCommand(
            "Titel",
            "Beschreibung",
            TestOrganisationId,
            TestAdresse,
            Frequenz.Regelmaessig);

        // Act
        await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await _dbContext
            .Bedarfe
            .Received(1)
            .AddAsync(Arg.Any<Bedarf>(), Arg.Any<CancellationToken>());

        await _unitOfWork
            .Received(1)
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
