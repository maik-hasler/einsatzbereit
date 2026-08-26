using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Keycloak;
using Application.Common.Pagination;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Keycloak;

internal sealed class KeycloakUserService(
	HttpClient httpClient,
	KeycloakAdminTokenProvider tokenProvider,
	IMemoryCache cache,
	IOptions<KeycloakOptions> options,
	ILogger<KeycloakUserService> logger)
	: IKeycloakUserService
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	// Keycloak's admin API has no bulk multi-id endpoint (only per-id GET or a
	// realm-wide page scan), so this cache - not a single network call - is what
	// keeps GetUserProfilesAsync/GetDisplayNamesAsync cheap on repeat page views.
	private static readonly TimeSpan ProfileCacheDuration = TimeSpan.FromSeconds(45);

	private const int RoleLookupConcurrency = 8;

	private const int MaxConcurrentUserLookups = 8;

	private readonly KeycloakOptions _options = options.Value;

	public async Task<KeycloakUserProfile> GetUserAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		var cacheKey = ProfileCacheKey(userId);
		if (cache.TryGetValue(cacheKey, out KeycloakUserProfile? cached) && cached is not null)
			return cached;

		var response = await SendAuthorizedAsync(
			() => new HttpRequestMessage(
				HttpMethod.Get, $"/admin/realms/{_options.Realm}/users/{userId}"),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		var user = await response.Content.ReadFromJsonAsync<KeycloakUserResponse>(
			JsonOptions, cancellationToken)
			?? throw new InvalidOperationException("Keycloak returned null user.");

		var profile = new KeycloakUserProfile(
			Guid.Parse(user.Id),
			user.Username,
			NullIfEmpty(user.FirstName),
			NullIfEmpty(user.LastName),
			user.Email ?? string.Empty);

		// Nominal Size - the shared cache's SizeLimit budget is denominated in
		// the tile bytes OpenStreetMapTileService caches, which dwarf this payload.
		cache.Set(cacheKey, profile, new MemoryCacheEntryOptions
		{
			Size = 1,
			AbsoluteExpirationRelativeToNow = ProfileCacheDuration,
		});

		return profile;
	}

	public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
		IReadOnlyList<Guid> userIds,
		CancellationToken cancellationToken = default)
	{
		var distinctIds = userIds.Distinct().ToList();
		var names = new string?[distinctIds.Count];

		await Parallel.ForEachAsync(
			Enumerable.Range(0, distinctIds.Count),
			new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentUserLookups, CancellationToken = cancellationToken },
			async (i, ct) =>
			{
				try
				{
					var profile = await GetUserAsync(distinctIds[i], ct);
					names[i] = profile.FirstName is not null || profile.LastName is not null
						? $"{profile.FirstName} {profile.LastName}".Trim()
						: profile.Username;
				}
				catch
				{
					// ignore individual lookup failures
				}
			});

		var result = new Dictionary<Guid, string>(distinctIds.Count);
		for (var i = 0; i < distinctIds.Count; i++)
		{
			if (names[i] is { } name)
				result[distinctIds[i]] = name;
		}
		return result;
	}

	public async Task<IReadOnlyDictionary<Guid, KeycloakUserProfile>> GetUserProfilesAsync(
		IReadOnlyList<Guid> userIds,
		CancellationToken cancellationToken = default)
	{
		var distinctIds = userIds.Distinct().ToList();
		var profiles = new KeycloakUserProfile?[distinctIds.Count];

		await Parallel.ForEachAsync(
			Enumerable.Range(0, distinctIds.Count),
			new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentUserLookups, CancellationToken = cancellationToken },
			async (i, ct) =>
			{
				try
				{
					profiles[i] = await GetUserAsync(distinctIds[i], ct);
				}
				catch
				{
					// ignore individual lookup failures - same tolerance as GetDisplayNamesAsync above
				}
			});

		var result = new Dictionary<Guid, KeycloakUserProfile>(distinctIds.Count);
		for (var i = 0; i < distinctIds.Count; i++)
		{
			if (profiles[i] is { } profile)
				result[distinctIds[i]] = profile;
		}
		return result;
	}

	public async Task UpdateUserAsync(
		Guid userId,
		string? firstName,
		string? lastName,
		CancellationToken cancellationToken = default)
	{
		var body = new
		{
			firstName = firstName ?? string.Empty,
			lastName = lastName ?? string.Empty,
		};

		var response = await SendAuthorizedAsync(
			() => new HttpRequestMessage(
				HttpMethod.Put, $"/admin/realms/{_options.Realm}/users/{userId}")
			{
				Content = JsonContent.Create(body, options: JsonOptions),
			},
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		cache.Remove(ProfileCacheKey(userId));
	}

	public async Task DeleteUserAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		var response = await SendAuthorizedAsync(
			() => new HttpRequestMessage(
				HttpMethod.Delete, $"/admin/realms/{_options.Realm}/users/{userId}"),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		cache.Remove(ProfileCacheKey(userId));
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
				HttpMethod.Get, $"/admin/realms/{_options.Realm}/users?{listQuery}"),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		var users = await response.Content.ReadFromJsonAsync<List<KeycloakUserResponse>>(
			JsonOptions, cancellationToken) ?? [];

		using var roleLookupThrottle = new SemaphoreSlim(RoleLookupConcurrency);
		var itemTasks = users
			.Where(user => user.ServiceAccountClientId is null)
			.Select(async user =>
			{
				await roleLookupThrottle.WaitAsync(cancellationToken);
				try
				{
					IReadOnlyList<string> roles;
					try
					{
						roles = await GetCompositeRealmRoleNamesAsync(user.Id, cancellationToken);
					}
					catch
					{
						roles = [];
					}

					return new AdminUserListItem(
						Guid.Parse(user.Id),
						user.Username,
						NullIfEmpty(user.FirstName),
						NullIfEmpty(user.LastName),
						user.Email ?? string.Empty,
						user.Enabled,
						roles);
				}
				finally
				{
					roleLookupThrottle.Release();
				}
			});

		var items = (await Task.WhenAll(itemTasks)).ToList();

		items.Sort((a, b) => string.Compare(a.Username, b.Username, StringComparison.OrdinalIgnoreCase));

		var countQuery = searchParam is null ? "" : $"?search={searchParam}";
		var countResponse = await SendAuthorizedAsync(
			() => new HttpRequestMessage(
				HttpMethod.Get, $"/admin/realms/{_options.Realm}/users/count{countQuery}"),
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
				HttpMethod.Put, $"/admin/realms/{_options.Realm}/users/{userId}")
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
				HttpMethod.Post, $"/admin/realms/{_options.Realm}/users/{userId}/role-mappings/realm")
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
				HttpMethod.Get, $"/admin/realms/{_options.Realm}/users/{userId}"),
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
				HttpMethod.Get, $"/admin/realms/{_options.Realm}/roles/{roleName}"),
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		return await response.Content.ReadFromJsonAsync<KeycloakRole>(
			JsonOptions, cancellationToken)
			?? throw new InvalidOperationException($"Keycloak role '{roleName}' not found.");
	}

	private async Task<HttpResponseMessage> SendAuthorizedAsync(
		Func<HttpRequestMessage> createRequest,
		CancellationToken cancellationToken)
	{
		var response = await SendOnceAsync(createRequest, forceRefresh: false, cancellationToken);
		if (response.StatusCode != HttpStatusCode.Unauthorized)
		{
			return response;
		}

		response.Dispose();
		return await SendOnceAsync(createRequest, forceRefresh: true, cancellationToken);
	}

	private async Task<HttpResponseMessage> SendOnceAsync(
		Func<HttpRequestMessage> createRequest,
		bool forceRefresh,
		CancellationToken cancellationToken)
	{
		var request = createRequest();
		request.Headers.Authorization = new AuthenticationHeaderValue(
			"Bearer", await tokenProvider.GetTokenAsync(forceRefresh, cancellationToken));

		return await httpClient.SendAsync(request, cancellationToken);
	}

	private static string? NullIfEmpty(string? value) =>
		string.IsNullOrEmpty(value) ? null : value;

	private static string ProfileCacheKey(Guid userId) => $"keycloak-user-profile:{userId}";

	private async Task EnsureSuccessAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		if (response.IsSuccessStatusCode)
		{
			return;
		}

		var method = response.RequestMessage?.Method;

		var path = response.RequestMessage?.RequestUri?.GetLeftPart(UriPartial.Path);

		if (logger.IsEnabled(LogLevel.Debug))
		{
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			logger.LogDebug(
				"Keycloak error response body for {Method} {Path}: {Body}",
				method,
				path,
				body);
		}

		throw new HttpRequestException(
			$"Keycloak responded with {(int)response.StatusCode} {response.StatusCode} for {method} {path}",
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
