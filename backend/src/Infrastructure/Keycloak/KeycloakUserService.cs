using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Common.Keycloak;
using Microsoft.Extensions.Options;

namespace Infrastructure.Keycloak;

internal sealed class KeycloakUserService(
	HttpClient httpClient,
	IOptions<KeycloakOptions> options)
	: IKeycloakUserService
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private readonly KeycloakOptions _options = options.Value;

	public async Task<KeycloakUserProfile> GetUserAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		var response = await httpClient.GetAsync(
			$"/admin/realms/{_options.Realm}/users/{userId}",
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		var user = await response.Content.ReadFromJsonAsync<KeycloakUserResponse>(
			JsonOptions, cancellationToken)
			?? throw new InvalidOperationException("Keycloak returned null user.");

		return new KeycloakUserProfile(
			Guid.Parse(user.Id),
			user.Username,
			NullIfEmpty(user.FirstName),
			NullIfEmpty(user.LastName),
			user.Email ?? string.Empty);
	}

	public async Task UpdateUserAsync(
		Guid userId,
		string? firstName,
		string? lastName,
		CancellationToken cancellationToken = default)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		// Keycloak's admin API merges PUT bodies and skips null fields, so to
		// clear firstName/lastName we must send an empty string instead of null.
		var body = new
		{
			firstName = firstName ?? string.Empty,
			lastName = lastName ?? string.Empty,
		};

		var putResponse = await httpClient.PutAsJsonAsync(
			$"/admin/realms/{_options.Realm}/users/{userId}",
			body,
			JsonOptions,
			cancellationToken);

		await EnsureSuccessAsync(putResponse, cancellationToken);
	}

	public async Task DeleteUserAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		var deleteResponse = await httpClient.DeleteAsync(
			$"/admin/realms/{_options.Realm}/users/{userId}",
			cancellationToken);

		await EnsureSuccessAsync(deleteResponse, cancellationToken);
	}

	private async Task EnsureAuthenticatedAsync(
		CancellationToken cancellationToken)
	{
		if (httpClient.DefaultRequestHeaders.Authorization is not null)
		{
			return;
		}

		var tokenRequest = new FormUrlEncodedContent([
			new KeyValuePair<string, string>("grant_type", "client_credentials"),
			new KeyValuePair<string, string>("client_id", _options.ClientId),
			new KeyValuePair<string, string>("client_secret", _options.ClientSecret)
		]);

		var tokenResponse = await httpClient.PostAsync(
			$"/realms/{_options.Realm}/protocol/openid-connect/token",
			tokenRequest,
			cancellationToken);

		await EnsureSuccessAsync(tokenResponse, cancellationToken);

		var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>(
			JsonOptions, cancellationToken);

		httpClient.DefaultRequestHeaders.Authorization =
			new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult!.AccessToken);
	}

	private static string? NullIfEmpty(string? value) =>
		string.IsNullOrEmpty(value) ? null : value;

	private static async Task EnsureSuccessAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		if (response.IsSuccessStatusCode)
		{
			return;
		}

		var body = await response.Content.ReadAsStringAsync(cancellationToken);

		throw new HttpRequestException(
			$"Keycloak responded with {(int)response.StatusCode} {response.StatusCode} " +
			$"for {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {body}",
			inner: null,
			response.StatusCode);
	}

	private sealed record TokenResponse(
		[property: JsonPropertyName("access_token")] string AccessToken);

	private sealed record KeycloakUserResponse(
		string Id,
		string Username,
		string? FirstName,
		string? LastName,
		string? Email);

}
