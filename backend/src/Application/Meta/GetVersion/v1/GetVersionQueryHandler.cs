using Application.Common.Messaging;
using Application.Meta;

namespace Application.Meta.GetVersion.v1;

internal sealed class GetVersionQueryHandler(
	IVersionProvider versionProvider)
	: IQueryHandler<GetVersionQuery, string>
{
	public ValueTask<string> Handle(
		GetVersionQuery request,
		CancellationToken cancellationToken = default) =>
		ValueTask.FromResult(versionProvider.GetVersion());
}
