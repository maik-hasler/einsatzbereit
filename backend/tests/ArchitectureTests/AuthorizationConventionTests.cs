using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;

namespace ArchitectureTests;

public sealed class AuthorizationConventionTests
{
	[Test]
	public void AllEndpoints_ShouldDeclareAnExplicitAuthorizationDecision()
	{
		var app = EndpointTestHelper.BuildMinimalAppWithAllEndpoints();

		var endpointsWithoutAuthorizationMetadata = EndpointTestHelper.GetAllRouteEndpoints(app)
			.Where(e => !e.Metadata.OfType<IAllowAnonymous>().Any()
				&& !e.Metadata.OfType<IAuthorizeData>().Any())
			.Select(e => e.RoutePattern.RawText)
			.ToList();

		endpointsWithoutAuthorizationMetadata.Should().BeEmpty(
			"every endpoint must call RequireAuthorization(...) or AllowAnonymous() explicitly - " +
			"omitting both leaves it anonymously reachable by accident");
	}
}
