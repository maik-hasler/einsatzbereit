using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Keycloak;
using Microsoft.Extensions.Options;

namespace Infrastructure.Keycloak;

internal sealed class KeycloakUserService(
	HttpClient httpClient,
	KeycloakAdminTokenProvider tokenProvider,
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
		var response = await SendAuthorizedAsync(
			() => httpClient.GetAsync(
				$"/admin/realms/{_options.Realm}/users/{userId}",
				cancellationToken),
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

	public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
		IReadOnlyList<Guid> userIds,
		CancellationToken cancellationToken = default)
	{
		var result = new Dictionary<Guid, string>(userIds.Count);
		foreach (var userId in userIds.Distinct())
		{
			try
			{
				var profile = await GetUserAsync(userId, cancellationToken);
				var name = profile.FirstName is not null || profile.LastName is not null
					? $"{profile.FirstName} {profile.LastName}".Trim()
					: profile.Username;
				result[userId] = name;
			}
			catch
			{
				// ignore individual lookup failures
			}
		}
		return result;
	}

	public async Task UpdateUserAsync(
		Guid userId,
		string? firstName,
		string? lastName,
		CancellationToken cancellationToken = default)
	{
		// Keycloak's admin API merges PUT bodies and skips null fields, so to
		// clear firstName/lastName we must send an empty string instead of null.
		var body = new
		{
			firstName = firstName ?? string.Empty,
			lastName = lastName ?? string.Empty,
		};

		var response = await SendAuthorizedAsync(
			() => httpClient.PutAsJsonAsync(
				$"/admin/realms/{_options.Realm}/users/{userId}",
				body,
				JsonOptions,
				cancellationToken),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	public async Task DeleteUserAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		var response = await SendAuthorizedAsync(
			() => httpClient.DeleteAsync(
				$"/admin/realms/{_options.Realm}/users/{userId}",
				cancellationToken),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	private async Task<HttpResponseMessage> SendAuthorizedAsync(
		Func<Task<HttpResponseMessage>> send,
		CancellationToken cancellationToken)
	{
		await SetBearerAsync(forceRefresh: false, cancellationToken);

		var response = await send();
		if (response.StatusCode != HttpStatusCode.Unauthorized)
		{
			return response;
		}

		// The cached admin token was rejected (e.g. Keycloak cold start or key
		// rotation). Refresh once and retry so a transient 401 self-heals
		// instead of bubbling up as a 500.
		response.Dispose();
		await SetBearerAsync(forceRefresh: true, cancellationToken);
		return await send();
	}

	private async Task SetBearerAsync(
		bool forceRefresh,
		CancellationToken cancellationToken) =>
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
			"Bearer",
			await tokenProvider.GetTokenAsync(forceRefresh, cancellationToken));

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

	private sealed record KeycloakUserResponse(
		string Id,
		string Username,
		string? FirstName,
		string? LastName,
		string? Email);
}
