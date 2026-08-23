using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class GetOrganizationMetaTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetOrganizationMeta_ShouldReturnNotFound_WhenOrganizationDoesNotExist(
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();

		var response = await httpClient.GetAsync(
			$"/v1/meta/organizations/{Guid.NewGuid()}", cancellationToken);

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Test]
	public async Task GetOrganizationMeta_ShouldReturnHtml_WithOrganizationNameAndDescription(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var uniqueName = $"Küstenschutz {Guid.NewGuid():N}";
		var organization = await authenticatedClient.CreateOrganizationAsync(
			new CreateOrganizationRequest
			{
				Name = uniqueName,
				Description = "Wir schützen die Küste vor Müll und Erosion.",
			},
			cancellationToken);

		using var httpClient = fixture.CreateHttpClient();
		var response = await httpClient.GetAsync(
			$"/v1/meta/organizations/{organization.Id.Value}", cancellationToken);
		var html = await response.Content.ReadAsStringAsync(cancellationToken);

		response.EnsureSuccessStatusCode();
		response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
		html.Should().Contain(uniqueName);
		html.Should().Contain("Wir schützen die Küste vor Müll und Erosion.");
		html.Should().Contain($"/organizations/{organization.Id.Value}");

		html.Should().Contain("/og-image.png");
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
	{
		var token = await fixture.GetAccessTokenAsync("olaf", "olaf123");
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}
}
