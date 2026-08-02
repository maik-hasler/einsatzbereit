using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Api.Common.Middleware;

// Minimal API endpoints don't run through MVC's filter pipeline, so RequestSizeLimitAttribute
// metadata (chained via .WithMetadata() on individual endpoints) is otherwise never read -
// unlike MVC controllers, nothing applies it to Kestrel's IHttpMaxRequestBodySizeFeature.
// Without this bridge, Kestrel's default 30 MB body limit is what actually applies, so an
// oversized upload gets fully buffered before an endpoint's own validation ever runs (#1177).
internal sealed class RequestSizeLimitMiddleware(RequestDelegate next)
{
	public async Task InvokeAsync(HttpContext context)
	{
		var sizeLimit = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>();
		var maxRequestBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();

		if (sizeLimit is not null && maxRequestBodySizeFeature is { IsReadOnly: false })
			maxRequestBodySizeFeature.MaxRequestBodySize = sizeLimit.MaxRequestBodySize;

		await next(context);
	}
}
