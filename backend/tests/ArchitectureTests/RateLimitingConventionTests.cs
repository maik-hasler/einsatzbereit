using Api.Common.RateLimiting;
using AwesomeAssertions;
using Microsoft.AspNetCore.Routing;

namespace ArchitectureTests;

public sealed class RateLimitingConventionTests
{
	[Test]
	public void AllEndpoints_ShouldHaveRateLimitingPolicyApplied()
	{
		var app = EndpointTestHelper.BuildMinimalAppWithAllEndpoints();

		var endpointsWithoutRateLimiting = EndpointTestHelper.GetAllRouteEndpoints(app)
			.Where(e => GetRateLimitingPolicyName(e) is null)
			.Select(e => e.RoutePattern.RawText)
			.ToList();

		endpointsWithoutRateLimiting.Should().BeEmpty(
			"every endpoint must call RequireRateLimiting() to opt in to a rate limiting policy");
	}

	[Test]
	public void AllEndpoints_ShouldUseOnlyKnownRateLimitingPolicies()
	{
		var knownPolicies = new[] { RateLimitingPolicies.Read, RateLimitingPolicies.Write };

		var app = EndpointTestHelper.BuildMinimalAppWithAllEndpoints();

		var endpointsWithUnknownPolicy = EndpointTestHelper.GetAllRouteEndpoints(app)
			.Select(e => new { Route = e.RoutePattern.RawText, Policy = GetRateLimitingPolicyName(e) })
			.Where(e => e.Policy is not null && !knownPolicies.Contains(e.Policy))
			.Select(e => $"{e.Route} uses unknown policy '{e.Policy}'")
			.ToList();

		endpointsWithUnknownPolicy.Should().BeEmpty(
			$"endpoints may only use the policies '{RateLimitingPolicies.Read}' or '{RateLimitingPolicies.Write}'");
	}

	private static string? GetRateLimitingPolicyName(RouteEndpoint endpoint)
	{
		var attr = endpoint.Metadata
			.FirstOrDefault(m => m.GetType().Name == "EnableRateLimitingAttribute");

		return attr?.GetType().GetProperty("PolicyName")?.GetValue(attr) as string;
	}
}
