using System.Net.Http.Headers;
using AwesomeAssertions;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class OrganizationSettingsTests(
	IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetOrganizationDetails_ShouldReturnDetails_AfterCreation(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var created = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Organization Details Test" }, cancellationToken);

		var result = await client.GetOrganizationDetailsAsync(created.Id.Value, cancellationToken);

		result.Id.Should().Be(created.Id.Value);
		result.Name.Should().Be("Organization Details Test");
		result.Members.Should().NotBeEmpty();
		result.CreatedOn.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
	}

	[Test]
	public async Task GetOrganizationDetails_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.GetOrganizationDetailsAsync(Guid.NewGuid(), cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task GetOrganizationDetails_ShouldReturn403_WhenUserLacksOrganisatorRole(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => client.GetOrganizationDetailsAsync(Guid.NewGuid(), cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task GetOrganizationDetails_ShouldReturn404_WhenOrganizationDoesNotExist(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var act = () => client.GetOrganizationDetailsAsync(Guid.NewGuid(), cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(404);
	}

	// ── UpdateOrganization ──────────────────────────────────────────────────

	[Test]
	public async Task UpdateOrganization_ShouldReturn204_WithValidData(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var created = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Before Update" }, cancellationToken);

		var updateRequest = new UpdateOrganizationRequest
		{
			Name = "After Update",
			Description = "New Description",
			ContactEmail = "contact@example.com",
			ContactPhone = "+49 30 123456",
			Website = "https://example.com",
			Address = new UpdateAddressRequest
			{
				Street = "Fire Station Street",
				HouseNumber = "1",
				ZipCode = "10115",
				City = "Berlin"
			}
		};

		await client.UpdateOrganizationAsync(created.Id.Value, updateRequest, cancellationToken);

		var result = await client.GetOrganizationDetailsAsync(created.Id.Value, cancellationToken);
		result.Name.Should().Be("After Update");
		result.Description.Should().Be("New Description");
		result.ContactEmail.Should().Be("contact@example.com");
		result.Address.Should().NotBeNull();
		result.Address!.City.Should().Be("Berlin");
	}

	[Test]
	public async Task UpdateOrganization_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.UpdateOrganizationAsync(
			Guid.NewGuid(),
			new UpdateOrganizationRequest { Name = "X" },
			cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task UpdateOrganization_ShouldClearAddress_WhenNullPassed(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var created = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Org with Address" }, cancellationToken);

		await client.UpdateOrganizationAsync(created.Id.Value, new UpdateOrganizationRequest
		{
			Name = "Org with Address",
			Address = new UpdateAddressRequest
			{
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Sample City"
			}
		}, cancellationToken);

		await client.UpdateOrganizationAsync(created.Id.Value, new UpdateOrganizationRequest
		{
			Name = "Org with Address",
			Address = null
		}, cancellationToken);

		var result = await client.GetOrganizationDetailsAsync(created.Id.Value, cancellationToken);
		result.Address.Should().BeNull();
	}

	[Test]
	public async Task RemoveMember_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task RemoveMember_ShouldReturn403_WhenRequestingUserIsNotMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		// vera creates her own organization and becomes its sole member/organizer.
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var veraOrg = await veraClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Vera's Unrelated Org" }, cancellationToken);
		var veraOrgDetails = await veraClient.GetOrganizationDetailsAsync(veraOrg.Id.Value, cancellationToken);
		var veraUserId = veraOrgDetails.Members.Single().UserId;

		// olaf is an organizer of other organizations, but not a member of vera's.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var act = () => olafClient.RemoveMemberAsync(veraOrg.Id.Value, veraUserId, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);

		var stillThere = await veraClient.GetOrganizationDetailsAsync(veraOrg.Id.Value, cancellationToken);
		stillThere.Members.Should().ContainSingle(m => m.UserId == veraUserId);
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}
}
