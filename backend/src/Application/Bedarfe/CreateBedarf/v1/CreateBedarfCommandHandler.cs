using Application.Abstractions;
using Application.Messaging;
using Domain.Bedarfe;

namespace Application.Bedarfe.CreateBedarf.v1;

internal sealed class CreateBedarfCommandHandler(
    IBedarfRepository bedarfRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateBedarfCommand, Bedarf>
{
    public async ValueTask<Bedarf> Handle(
        CreateBedarfCommand request,
        CancellationToken cancellationToken = default)
    {
        var bedarf = Bedarf.Create(request.Title, request.Description);
        
        await bedarfRepository.AddAsync(bedarf, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        
        return bedarf;
    }
}