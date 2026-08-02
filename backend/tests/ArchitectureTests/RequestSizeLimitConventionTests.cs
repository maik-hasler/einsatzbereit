using AwesomeAssertions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ArchitectureTests;

public sealed class RequestSizeLimitConventionTests
{
	[Test]
	public void AllMultipartFormEndpoints_ShouldHaveRequestSizeLimitApplied()
	{
		var app = EndpointTestHelper.BuildMinimalAppWithAllEndpoints();

		var endpointsWithoutSizeLimit = EndpointTestHelper.GetAllRouteEndpoints(app)
			.Where(AcceptsMultipartFormData)
			.Where(e => e.Metadata.GetMetadata<IRequestSizeLimitMetadata>() is null)
			.Select(e => e.RoutePattern.RawText)
			.ToList();

		endpointsWithoutSizeLimit.Should().BeEmpty(
			"file-upload endpoints must chain .WithMetadata(new RequestSizeLimitAttribute(...)) so " +
			"Kestrel rejects an oversized body before it is fully buffered (#1177)");
	}

	private static bool AcceptsMultipartFormData(RouteEndpoint endpoint) =>
		endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes
			.Contains("multipart/form-data") == true;
}
