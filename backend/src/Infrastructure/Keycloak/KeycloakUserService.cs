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
			() => new HttpRequestMessage(
				HttpMethod.Get,
				$"/admin/realms/{_options.Realm}/users/{userId}"),
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
			() => new HttpRequestMessage(
				HttpMethod.Put,
				$"/admin/realms/{_options.Realm}/users/{userId}")
			{
				Content = JsonContent.Create(body, options: JsonOptions),
			},
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	public async Task DeleteUserAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		var response = await SendAuthorizedAsync(
			() => new HttpRequestMessage(
				HttpMethod.Delete,
				$"/admin/realms/{_options.Realm}/users/{userId}"),
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
			() => new HttpRequestMessage(
				HttpMethod.Get,
				$"/admin/realms/{_options.Realm}/users?{listQuery}"),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		var users = await response.Content.ReadFromJsonAsync<List<KeycloakUserResponse>>(
			JsonOptions, cancellationToken) ?? [];

		var humanUsers = users.Where(u => u.ServiceAccountClientId is null).ToList();

		// Each request now sends its own Authorization header (see
		// SendAuthorizedAsync) instead of mutating a header shared across the
		// HttpClient instance, so these per-user role lookups are safe to run
		// concurrently instead of one blocking round trip at a time.
		var roleNamesByUser = await Task.WhenAll(humanUsers.Select(async user =>
		{
			try
			{
				return await GetCompositeRealmRoleNamesAsync(user.Id, cancellationToken);
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
				return (IReadOnlyList<string>)[];
			}
		}));

		var items = humanUsers
			.Zip(roleNamesByUser, (user, roles) => new AdminUserListItem(
				Guid.Parse(user.Id),
				user.Username,
				NullIfEmpty(user.FirstName),
				NullIfEmpty(user.LastName),
				user.Email ?? string.Empty,
				user.Enabled,
				roles))
			.ToList();

		items.Sort((a, b) => string.Compare(a.Username, b.Username, StringComparison.OrdinalIgnoreCase));

		// The count includes the filtered-out service-account entries (e.g.
		// service-account-backend), so this can be off by a small, fixed amount
		// from the actual number of human users - not worth a second full scan
		// to correct for a cosmetic pageCount imprecision on an admin-only page.
		var countQuery = searchParam is null ? "" : $"?search={searchParam}";
		var countResponse = await SendAuthorizedAsync(
			() => new HttpRequestMessage(
				HttpMethod.Get,
				$"/admin/realms/{_options.Realm}/users/count{countQuery}"),
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
			() => new HttpRequestMessage(
				HttpMethod.Put,
				$"/admin/realms/{_options.Realm}/users/{userId}")
			{
				Content = JsonContent.Create(new { enabled }, options: JsonOptions),
			},
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	public async Task AssignAdminRoleAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		var role = await GetRealmRoleAsync("admin", cancellationToken);

		var response = await SendAuthorizedAsync(
			() => new HttpRequestMessage(
				HttpMethod.Post,
				$"/admin/realms/{_options.Realm}/users/{userId}/role-mappings/realm")
			{
				Content = JsonContent.Create(new[] { role }, options: JsonOptions),
			},
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	public async Task RemoveAdminRoleAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		var role = await GetRealmRoleAsync("admin", cancellationToken);

		var response = await SendAuthorizedAsync(
			() => new HttpRequestMessage(
				HttpMethod.Delete,
				$"/admin/realms/{_options.Realm}/users/{userId}/role-mappings/realm")
			{
				Content = JsonContent.Create(new[] { role }, options: JsonOptions),
			},
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	public async Task<bool> IsServiceAccountAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		var response = await SendAuthorizedAsync(
			() => new HttpRequestMessage(
				HttpMethod.Get,
				$"/admin/realms/{_options.Realm}/users/{userId}"),
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
			() => new HttpRequestMessage(
				HttpMethod.Get,
				$"/admin/realms/{_options.Realm}/users/{userId}/role-mappings/realm/composite"),
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
			() => new HttpRequestMessage(
				HttpMethod.Get,
				$"/admin/realms/{_options.Realm}/roles/{roleName}"),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		return await response.Content.ReadFromJsonAsync<KeycloakRole>(
			JsonOptions, cancellationToken)
			?? throw new InvalidOperationException($"Keycloak role '{roleName}' not found.");
	}

	// Builds a fresh HttpRequestMessage per attempt (rather than accepting an
	// already-built one, or a delegate that calls a shared-httpClient
	// extension method) and sets the bearer token directly on that message's
	// own Headers instead of httpClient.DefaultRequestHeaders. That keeps
	// concurrent calls on this one HttpClient instance from racing on shared
	// header state, so callers (e.g. ListUsersAsync's per-user role lookups)
	// can safely run them via Task.WhenAll instead of one at a time.
	private async Task<HttpResponseMessage> SendAuthorizedAsync(
		Func<HttpRequestMessage> createRequest,
		CancellationToken cancellationToken)
	{
		var token = await tokenProvider.GetTokenAsync(forceRefresh: false, cancellationToken);
		var response = await SendWithBearerAsync(createRequest(), token, cancellationToken);
		if (response.StatusCode != HttpStatusCode.Unauthorized)
		{
			return response;
		}

		// The cached admin token was rejected (e.g. Keycloak cold start or key
		// rotation). Refresh once and retry so a transient 401 self-heals
		// instead of bubbling up as a 500. A message can only be sent once, so
		// the retry sends a freshly built request from createRequest().
		response.Dispose();
		token = await tokenProvider.GetTokenAsync(forceRefresh: true, cancellationToken);
		return await SendWithBearerAsync(createRequest(), token, cancellationToken);
	}

	private Task<HttpResponseMessage> SendWithBearerAsync(
		HttpRequestMessage request,
		string token,
		CancellationToken cancellationToken)
	{
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return httpClient.SendAsync(request, cancellationToken);
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
