using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Keycloak;
using Application.Common.Pagination;
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

	public async Task<IReadOnlyDictionary<Guid, KeycloakUserProfile>> GetUserProfilesAsync(
		IReadOnlyList<Guid> userIds,
		CancellationToken cancellationToken = default)
	{
		var result = new Dictionary<Guid, KeycloakUserProfile>(userIds.Count);
		foreach (var userId in userIds.Distinct())
		{
			try
			{
				result[userId] = await GetUserAsync(userId, cancellationToken);
			}
			catch
			{
				// ignore individual lookup failures - same tolerance as GetDisplayNamesAsync above
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

	public async Task<PagedList<AdminUserListItem>> ListUsersAsync(
		string? search,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		var searchParam = string.IsNullOrWhiteSpace(search) ? null : Uri.EscapeDataString(search);
		var first = (pageNumber - 1) * pageSize;

		var listQuery = searchParam is null
			? $"first={first}&max={pageSize}"
			: $"search={searchParam}&first={first}&max={pageSize}";

		var response = await SendAuthorizedAsync(
			() => httpClient.GetAsync(
				$"/admin/realms/{_options.Realm}/users?{listQuery}",
				cancellationToken),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		var users = await response.Content.ReadFromJsonAsync<List<KeycloakUserResponse>>(
			JsonOptions, cancellationToken) ?? [];

		var items = new List<AdminUserListItem>(users.Count);

		// Sequential, not Task.WhenAll: SendAuthorizedAsync mutates the shared
		// httpClient.DefaultRequestHeaders.Authorization on every call, so
		// concurrent calls on this one instance would race on that header
		// collection. GetDisplayNamesAsync above has the same per-user-lookup
		// shape and is sequential for the same reason - match it.
		foreach (var user in users)
		{
			if (user.ServiceAccountClientId is not null)
				continue;

			IReadOnlyList<string> roles;
			try
			{
				roles = await GetCompositeRealmRoleNamesAsync(user.Id, cancellationToken);
			}
			catch
			{
				// This user just came back from the list call above, but Keycloak
				// can still 404 the follow-up per-user role lookup if they're
				// deleted between the two calls (observed in CI: a short-lived
				// test-created user removed by its own cleanup mid-request) -
				// same "ignore individual lookup failures" tolerance as
				// GetDisplayNamesAsync above, so one stale/racy id doesn't sink
				// the whole admin Users table.
				roles = [];
			}

			items.Add(new AdminUserListItem(
				Guid.Parse(user.Id),
				user.Username,
				NullIfEmpty(user.FirstName),
				NullIfEmpty(user.LastName),
				user.Email ?? string.Empty,
				user.Enabled,
				roles));
		}

		items.Sort((a, b) => string.Compare(a.Username, b.Username, StringComparison.OrdinalIgnoreCase));

		// The count includes the filtered-out service-account entries (e.g.
		// service-account-backend), so this can be off by a small, fixed amount
		// from the actual number of human users - not worth a second full scan
		// to correct for a cosmetic pageCount imprecision on an admin-only page.
		var countQuery = searchParam is null ? "" : $"?search={searchParam}";
		var countResponse = await SendAuthorizedAsync(
			() => httpClient.GetAsync(
				$"/admin/realms/{_options.Realm}/users/count{countQuery}",
				cancellationToken),
			cancellationToken);

		await EnsureSuccessAsync(countResponse, cancellationToken);

		var totalCount = await countResponse.Content.ReadFromJsonAsync<int>(cancellationToken: cancellationToken);

		return new PagedList<AdminUserListItem>(items, totalCount, pageNumber, pageSize);
	}

	public async Task SetUserEnabledAsync(
		Guid userId,
		bool enabled,
		CancellationToken cancellationToken = default)
	{
		var response = await SendAuthorizedAsync(
			() => httpClient.PutAsJsonAsync(
				$"/admin/realms/{_options.Realm}/users/{userId}",
				new { enabled },
				JsonOptions,
				cancellationToken),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	public async Task AssignAdminRoleAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		var role = await GetRealmRoleAsync("admin", cancellationToken);

		var response = await SendAuthorizedAsync(
			() => httpClient.PostAsJsonAsync(
				$"/admin/realms/{_options.Realm}/users/{userId}/role-mappings/realm",
				new[] { role },
				JsonOptions,
				cancellationToken),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	public async Task RemoveAdminRoleAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		var role = await GetRealmRoleAsync("admin", cancellationToken);

		// A new HttpRequestMessage per attempt: SendAuthorizedAsync may invoke
		// the delegate twice (retry-after-401), and a message can only be sent once.
		var response = await SendAuthorizedAsync(
			() => httpClient.SendAsync(
				new HttpRequestMessage(
					HttpMethod.Delete,
					$"/admin/realms/{_options.Realm}/users/{userId}/role-mappings/realm")
				{
					Content = JsonContent.Create(new[] { role }, options: JsonOptions),
				},
				cancellationToken),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	public async Task<bool> IsServiceAccountAsync(
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
			JsonOptions, cancellationToken);

		return user?.ServiceAccountClientId is not null;
	}

	private async Task<IReadOnlyList<string>> GetCompositeRealmRoleNamesAsync(
		string userId,
		CancellationToken cancellationToken)
	{
		var response = await SendAuthorizedAsync(
			() => httpClient.GetAsync(
				$"/admin/realms/{_options.Realm}/users/{userId}/role-mappings/realm/composite",
				cancellationToken),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		var roles = await response.Content.ReadFromJsonAsync<List<KeycloakRole>>(
			JsonOptions, cancellationToken) ?? [];

		return roles.Select(r => r.Name).ToList();
	}

	private async Task<KeycloakRole> GetRealmRoleAsync(
		string roleName,
		CancellationToken cancellationToken)
	{
		var response = await SendAuthorizedAsync(
			() => httpClient.GetAsync(
				$"/admin/realms/{_options.Realm}/roles/{roleName}",
				cancellationToken),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		return await response.Content.ReadFromJsonAsync<KeycloakRole>(
			JsonOptions, cancellationToken)
			?? throw new InvalidOperationException($"Keycloak role '{roleName}' not found.");
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
		string? Email,
		bool Enabled = true,
		string? ServiceAccountClientId = null);

	private sealed record KeycloakRole(
		string Id,
		string Name);
}
