using Microsoft.Extensions.Primitives;

namespace Application.Common.Caching;

internal interface ICacheCategoryTokenProvider
{
	IChangeToken GetToken(string category);
}
