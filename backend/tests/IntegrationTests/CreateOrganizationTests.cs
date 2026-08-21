using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class CreateOrganizationTests(
	IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task CreateOrganization_ShouldReturnOrganization_WhenNameIsValid(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var result = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Sample Fire Department" }, cancellationToken);

		result.Name.Should().Be("Sample Fire Department");
	}

	[Test]
	public async Task CreateOrganization_ShouldSucceed_WhenNameContainsGermanCharacters(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var result = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Ärztlicher Übungsdienst Straße" }, cancellationToken);

		result.Name.Should().Be("Ärztlicher Übungsdienst Straße");
	}

	[Test]
	public async Task CreateOrganization_ShouldSucceed_WhenNameContainsSpecialCharacters(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var result = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Org (Test) & Co. #1" }, cancellationToken);

		result.Name.Should().Be("Org (Test) & Co. #1");
	}

	[Test]
	public async Task CreateOrganization_ShouldReturn400_WhenAddressStreetExceedsMaxLength(
		CancellationToken cancellationToken)
	{
		// Issue #1146: [MaxLength] on request DTOs used to only affect the generated
		// OpenAPI/NSwag client - nothing evaluated DataAnnotations server-side, so an
		// oversized Address field was silently accepted and stored as unbounded text.
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var act = () => client.CreateOrganizationAsync(
			new CreateOrganizationRequest
			{
				Name = "Org With Bad Address",
				Address = new CreateAddressRequest
				{
					Street = new string('a', 201),
					HouseNumber = "1",
					ZipCode = "12345",
					City = "Berlin",
				},
			},
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task CreateOrganization_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Unauthorized Org" }, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task GetOrganizations_ShouldReturnCreatedOrganization(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Test Organization" }, cancellationToken);

		var result = await client.GetOrganizationsAsync(cancellationToken);

		result.Should().Contain(o => o.Name == "Test Organization");
	}

	[Test]
	public async Task GetOrganizations_ShouldReturnEmpty_WhenUserHasNoOrganizations(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		var result = await client.GetOrganizationsAsync(cancellationToken);

		result.Should().BeEmpty();
	}

	[Test]
	public async Task CreateOrganization_ShouldPersistEveryOptionalField_AndReturnThemFromDetails(
		CancellationToken cancellationToken)
	{
		// Moved down from `OrganizationTests` in #2148. The modal half of that
		// case - that every typed field is collected into one create request, and
		// that a name-only organization omits the address object entirely rather
		// than sending an empty one - is in
		// `frontend/src/components/CreateOrganizationModal.test.tsx`. What no
		// test covered is the server side of the same claim: the create endpoint
		// has to persist all five optional fields plus the address, and
		// GetOrganizationDetails has to read them back. The E2E original proved
		// it by reloading the settings page and reading the values off it.
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var created = await client.CreateOrganizationAsync(new CreateOrganizationRequest
		{
			Name = "Full Details Org",
			Description = "A helpful description for volunteers.",
			ContactEmail = "contact@example.com",
			ContactPhone = "+49 30 1234567",
			Website = "https://example.com",
			Address = new CreateAddressRequest
			{
				Street = "Main Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
			},
		}, cancellationToken);

		var details = await client.GetOrganizationDetailsAsync(created.Id.Value, cancellationToken);

		details.Name.Should().Be("Full Details Org");
		details.Description.Should().Be("A helpful description for volunteers.");
		details.ContactEmail.Should().Be("contact@example.com");
		details.ContactPhone.Should().Be("+49 30 1234567");
		details.Website.Should().Be("https://example.com");
		details.Address.Should().NotBeNull();
		details.Address.Street.Should().Be("Main Street");
		details.Address.HouseNumber.Should().Be("1");
		details.Address.ZipCode.Should().Be("12345");
		details.Address.City.Should().Be("Berlin");
	}

	[Test]
	public async Task CreateOrganization_ShouldLeaveOptionalFieldsNull_WhenOnlyANameWasGiven(
		CancellationToken cancellationToken)
	{
		// The companion to the case above: without it, an endpoint that filled
		// every field with a default would satisfy the round trip. It is also the
		// server half of the modal's "omits the address entirely" case - a
		// name-only organization is a perfectly legal thing to create.
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var created = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Name Only Org" }, cancellationToken);

		var details = await client.GetOrganizationDetailsAsync(created.Id.Value, cancellationToken);

		details.Name.Should().Be("Name Only Org");
		details.Description.Should().BeNull();
		details.ContactEmail.Should().BeNull();
		details.ContactPhone.Should().BeNull();
		details.Website.Should().BeNull();
		details.Address.Should().BeNull();
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
