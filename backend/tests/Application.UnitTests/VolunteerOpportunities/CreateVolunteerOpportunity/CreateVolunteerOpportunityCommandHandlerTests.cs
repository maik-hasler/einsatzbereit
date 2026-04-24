using Application.Common.Persistence;
using Application.VolunteerOpportunities.CreateVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.VolunteerOpportunities;
using NSubstitute;


namespace Application.UnitTests.VolunteerOpportunities.CreateVolunteerOpportunity;

public class CreateVolunteerOpportunityCommandHandlerTests
{
    private static readonly OrganizationId TestOrganizationId = new(Guid.NewGuid());
    private static readonly Address TestAddress = new("Musterstraße", "1", "12345", "Berlin");

    private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
    private readonly CreateVolunteerOpportunityCommandHandler _sut;

    public CreateVolunteerOpportunityCommandHandlerTests()
    {
        _sut = new CreateVolunteerOpportunityCommandHandler(_dbContext);
    }

    [Test]
    public async Task Handle_ShouldCreateAndPersistOpportunity_WithCorrectData()
    {
        // Arrange
        var command = new CreateVolunteerOpportunityCommand(
            "Helpers needed",
            "For moving",
            TestOrganizationId,
            false,
            TestAddress,
            Occurrence.OneTime,
            ParticipationType.Waitlist);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Title.Should().Be("Helpers needed");
        result.Description.Should().Be("For moving");
        result.OrganizationId.Should().Be(TestOrganizationId);
        result.IsRemote.Should().BeFalse();
        result.Address.Should().Be(TestAddress);
        result.Occurrence.Should().Be(Occurrence.OneTime);
        result.ParticipationType.Should().Be(ParticipationType.Waitlist);
    }

    [Test]
    public async Task Handle_ShouldCallRepositoryAndUnitOfWork()
    {
        // Arrange
        var command = new CreateVolunteerOpportunityCommand(
            "Title",
            "Description",
            TestOrganizationId,
            false,
            TestAddress,
            Occurrence.Recurring,
            ParticipationType.IndividualContact);

        // Act
        await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await _dbContext
            .VolunteerOpportunities
            .Received(1)
            .AddAsync(Arg.Any<VolunteerOpportunity>(), Arg.Any<CancellationToken>());
    }
}
