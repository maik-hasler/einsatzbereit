using Application.Abstractions;
using Application.Messaging;
using Domain.Bedarfe;

namespace Application.Bedarfe.CreateBedarf.v1;

internal sealed class CreateBedarfCommandHandler(
    IApplicationDbContext dbContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateBedarfCommand, Bedarf>
{
    public async ValueTask<Bedarf> Handle(
        CreateBedarfCommand request,
        CancellationToken cancellationToken = default)
    {
        var bedarf = Bedarf.Create(
            request.Title,
            request.Description,
            request.OrganisationId,
            request.Adresse,
            request.Frequenz);

        await dbContext.Bedarfe.AddAsync(bedarf, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return bedarf;
    }
}
