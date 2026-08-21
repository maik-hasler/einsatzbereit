using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class UpdateUserProfileTests(
	IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task UpdateUserProfile_ShouldPersistChanges_WhenAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		await client.UpdateUserProfileAsync(
			new UpdateUserProfileRequest { FirstName = "Vera", LastName = "Sample" },
			cancellationToken);

		var profile = await client.GetUserProfileAsync(cancellationToken);
		profile.FirstName.Should().Be("Vera");
		profile.LastName.Should().Be("Sample");
	}

	[Test]
	public async Task UpdateUserProfile_ShouldClearNames_WhenNullValuesProvided(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		await client.UpdateUserProfileAsync(
			new UpdateUserProfileRequest { FirstName = null, LastName = null },
			cancellationToken);

		var profile = await client.GetUserProfileAsync(cancellationToken);
		profile.FirstName.Should().BeNull();
		profile.LastName.Should().BeNull();
	}

	[Test]
	public async Task UpdateUserProfile_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.UpdateUserProfileAsync(
			new UpdateUserProfileRequest { FirstName = "Test" },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(401);
	}

	// Regression for #1173: [MaxLength] attributes on request records were
	// never enforced (no validation was registered), so oversized values used
	// to reach the DB unbounded instead of being rejected.

	[Test]
	public async Task UpdateUserProfile_ShouldReturn400_WhenBioExceedsMaxLength(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => client.UpdateUserProfileAsync(
			new UpdateUserProfileRequest { Bio = new string('a', 1001) },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task UpdateUserProfile_ShouldReturn400_WhenSkillsExceedMaxCount(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => client.UpdateUserProfileAsync(
			new UpdateUserProfileRequest { Skills = Enumerable.Range(0, 51).Select(i => $"skill-{i}").ToList() },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task UpdateUserProfile_ShouldReturn400_WhenASkillExceedsMaxLength(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => client.UpdateUserProfileAsync(
			new UpdateUserProfileRequest { Skills = [new string('a', 101)] },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task UpdateUserProfile_ShouldReturn400_WhenLanguagesExceedMaxCount(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => client.UpdateUserProfileAsync(
			new UpdateUserProfileRequest { Languages = Enumerable.Range(0, 21).Select(i => $"lang-{i}").ToList() },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task UpdateUserProfile_ShouldReturn400_WhenALanguageExceedsMaxLength(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => client.UpdateUserProfileAsync(
			new UpdateUserProfileRequest { Languages = [new string('a', 51)] },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	/// <summary>1x1 transparent PNG - the smallest thing the upload endpoint accepts.</summary>
	private static readonly byte[] TinyPng = Convert.FromBase64String(
		"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

	[Test]
	public async Task DeleteUserAvatar_ShouldClearAvatarUrl(
		CancellationToken cancellationToken)
	{
		// #1063's delete endpoint, moved down from `AvatarAndLogoDisplayTests` in
		// #2148 for the same reason as the organization-logo twin: the
		// substantive assertion was a re-read of GET /v1/users/me showing
		// avatarUrl null after the DELETE, which needs no browser and had no
		// coverage here. Moving it also releases the
		// [NotInParallel("visualtests-vera-avatar")] serialisation it forced on
		// the upload test in that class - its DELETE was what made vera's
		// uploaded avatar vanish mid-assertion.
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		using var avatar = new MemoryStream(TinyPng);
		await client.UploadUserAvatarAsync(
			new FileParameter(avatar, "avatar.png", "image/png"), cancellationToken);

		var afterUpload = await client.GetUserProfileAsync(cancellationToken);
		afterUpload.AvatarUrl.Should().NotBeNull();

		await client.DeleteUserAvatarAsync(cancellationToken);

		var afterDelete = await client.GetUserProfileAsync(cancellationToken);
		afterDelete.AvatarUrl.Should().BeNull();
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
