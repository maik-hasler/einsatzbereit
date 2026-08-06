using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Domain.Organizations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace Infrastructure.Keycloak;

internal sealed class KeycloakOrganizationService(
	HttpClient httpClient,
	KeycloakAdminTokenProvider tokenProvider,
	IOptions<KeycloakOptions> options,
	IApplicationDbContext dbContext,
	ILogger<KeycloakOrganizationService> logger)
	: IKeycloakOrganizationService
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private readonly KeycloakOptions _options = options.Value;

	public async Task<Guid> CreateOrganizationAsync(
		string name,
		CancellationToken cancellationToken = default)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		var alias = GenerateAlias(name);
		var request = new { name, alias };

		var response = await httpClient.PostAsJsonAsync(
			$"/admin/realms/{_options.Realm}/organizations",
			request,
			JsonOptions,
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		var location = response.Headers.Location?.ToString()
			?? throw new InvalidOperationException("Keycloak did not return a Location header.");

		var idString = location.Split('/')[^1];

		return Guid.Parse(idString);
	}

	public async Task<Guid?> FindOrganizationByNameAsync(
		string name,
		CancellationToken cancellationToken = default)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		var encoded = Uri.EscapeDataString(name);
		var response = await httpClient.GetAsync(
			$"/admin/realms/{_options.Realm}/organizations?search={encoded}&exact=true",
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		var organizations = await response.Content.ReadFromJsonAsync<List<KeycloakOrganizationResponse>>(
			JsonOptions, cancellationToken) ?? [];

		var match = organizations.FirstOrDefault(o => o.Name == name);

		return match is null ? null : Guid.Parse(match.Id);
	}

	public async Task AddMemberAsync(
		Guid organizationId,
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		var response = await httpClient.PostAsJsonAsync(
			$"/admin/realms/{_options.Realm}/organizations/{organizationId}/members",
			userId.ToString(),
			JsonOptions,
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	public async Task AssignOrganizerRoleAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		var rolesResponse = await httpClient.GetAsync(
			$"/admin/realms/{_options.Realm}/roles/organisator",
			cancellationToken);

		await EnsureSuccessAsync(rolesResponse, cancellationToken);

		var role = await rolesResponse.Content.ReadFromJsonAsync<KeycloakRole>(
			JsonOptions, cancellationToken);

		var response = await httpClient.PostAsJsonAsync(
			$"/admin/realms/{_options.Realm}/users/{userId}/role-mappings/realm",
			new[] { role },
			JsonOptions,
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	public async Task RevokeOrganizerRoleAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		var rolesResponse = await httpClient.GetAsync(
			$"/admin/realms/{_options.Realm}/roles/organisator",
			cancellationToken);

		await EnsureSuccessAsync(rolesResponse, cancellationToken);

		var role = await rolesResponse.Content.ReadFromJsonAsync<KeycloakRole>(
			JsonOptions, cancellationToken);

		using var request = new HttpRequestMessage(
			HttpMethod.Delete,
			$"/admin/realms/{_options.Realm}/users/{userId}/role-mappings/realm")
		{
			Content = JsonContent.Create(new[] { role }, options: JsonOptions)
		};

		var response = await httpClient.SendAsync(request, cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	public async Task<IReadOnlyList<KeycloakOrganizationMember>> GetMembersAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		HttpResponseMessage membersResponse;
		try
		{
			membersResponse = await httpClient.GetAsync(
				$"/admin/realms/{_options.Realm}/organizations/{organizationId}/members",
				cancellationToken);
		}
		catch (ExecutionRejectedException ex)
		{
			// AddStandardResilienceHandler's circuit breaker/timeout/rate limiter can
			// reject the call outright under sustained failure - exactly the "sustained
			// concurrent admin-API load" scenario #1709 traces the original 400 back
			// to - before it ever reaches EnsureSuccessAsync below. Normalize to the
			// same HttpRequestException that path throws, so
			// GetOrganizationDetailsQueryHandler has a single exception type to catch
			// regardless of which stage failed.
			throw new HttpRequestException(
				$"Keycloak organization members lookup for {organizationId} was rejected by the resilience pipeline: {ex.Message}",
				ex);
		}

		await EnsureSuccessAsync(membersResponse, cancellationToken);

		var members = await membersResponse.Content.ReadFromJsonAsync<List<KeycloakUserResponse>>(
			JsonOptions, cancellationToken) ?? [];

		// Organizer status is answered from the local organization_membership table
		// (scoped to this organization) instead of Keycloak's realm-wide organisator
		// role - see #1386. That also makes it correctly per-organization, where the
		// old Keycloak-role-based check was global across every organization a user
		// ever organized.
		var organizerIds = await dbContext.GetOrganizerUserIdsAsync(
			OrganizationId.Create(organizationId).GetValueOrThrow(), cancellationToken);

		return members
			.Select(u => new KeycloakOrganizationMember(
				Guid.Parse(u.Id),
				u.Username,
				u.FirstName,
				u.LastName,
				u.Email ?? string.Empty,
				organizerIds.Contains(Guid.Parse(u.Id))))
			.ToList();
	}

	public async Task<IReadOnlySet<Guid>> GetRealmOrganisatorUserIdsAsync(
		CancellationToken cancellationToken = default)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		var response = await httpClient.GetAsync(
			$"/admin/realms/{_options.Realm}/roles/organisator/users",
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		var users = await response.Content.ReadFromJsonAsync<List<KeycloakUserResponse>>(
			JsonOptions, cancellationToken) ?? [];

		return users.Select(u => Guid.Parse(u.Id)).ToHashSet();
	}

	public async Task<IReadOnlyList<KeycloakOrganizationMember>> SearchUsersAsync(
		string search,
		int max = 20,
		CancellationToken cancellationToken = default)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		var encoded = Uri.EscapeDataString(search);
		var response = await httpClient.GetAsync(
			$"/admin/realms/{_options.Realm}/users?search={encoded}&max={max}",
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		var users = await response.Content.ReadFromJsonAsync<List<KeycloakUserResponse>>(
			JsonOptions, cancellationToken) ?? [];

		return users
			.Select(u => new KeycloakOrganizationMember(
				Guid.Parse(u.Id),
				u.Username,
				u.FirstName,
				u.LastName,
				u.Email ?? string.Empty,
				false))
			.ToList();
	}

	public async Task RemoveMemberAsync(
		Guid organizationId,
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		var response = await httpClient.DeleteAsync(
			$"/admin/realms/{_options.Realm}/organizations/{organizationId}/members/{userId}",
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	public async Task DeleteOrganizationAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		var response = await httpClient.DeleteAsync(
			$"/admin/realms/{_options.Realm}/organizations/{organizationId}",
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);
	}

	private async Task EnsureAuthenticatedAsync(
		CancellationToken cancellationToken) =>
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
			"Bearer",
			await tokenProvider.GetTokenAsync(forceRefresh: false, cancellationToken));

	internal static string GenerateAlias(string name)
	{
		// Normalize to decomposed form so we can strip diacritics,
		// but first handle common German replacements explicitly.
		var sb = new StringBuilder(name.Length);

		foreach (var c in name)
		{
			var replacement = c switch
			{
				'ä' or 'Ä' => "ae",
				'ö' or 'Ö' => "oe",
				'ü' or 'Ü' => "ue",
				'ß' => "ss",
				_ => null
			};

			if (replacement is not null)
			{
				sb.Append(replacement);
				continue;
			}

			var normalized = c.ToString().Normalize(NormalizationForm.FormD);
			foreach (var nc in normalized)
			{
				if (CharUnicodeInfo.GetUnicodeCategory(nc) != UnicodeCategory.NonSpacingMark)
				{
					sb.Append(nc);
				}
			}
		}

		var alias = sb.ToString().ToLowerInvariant();

		sb.Clear();
		var prevHyphen = true; // treat start as hyphen to trim leading
		foreach (var c in alias)
		{
			if (char.IsLetterOrDigit(c))
			{
				sb.Append(c);
				prevHyphen = false;
			}
			else if (!prevHyphen)
			{
				sb.Append('-');
				prevHyphen = true;
			}
		}

		return sb.Length > 0 && sb[^1] == '-'
			? sb.ToString(0, sb.Length - 1)
			: sb.ToString();
	}

	private async Task EnsureSuccessAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		if (response.IsSuccessStatusCode)
		{
			return;
		}

		var method = response.RequestMessage?.Method;
		// Strip the query string - it can carry PII such as search terms - before it
		// ever reaches the Error-level exception message that gets logged/exported.
		var path = response.RequestMessage?.RequestUri?.GetLeftPart(UriPartial.Path);

		// Logged unconditionally rather than gated behind Debug - a non-2xx here is
		// always unexpected, and Debug logging being off in every environment that
		// hit it is exactly what made #1709's unexplained 400 undiagnosable.
		var body = await response.Content.ReadAsStringAsync(cancellationToken);
		logger.LogWarning(
			"Keycloak responded with {StatusCode} for {Method} {Path}. Response body: {Body}",
			(int)response.StatusCode,
			method,
			path,
			body);

		throw new HttpRequestException(
			$"Keycloak responded with {(int)response.StatusCode} {response.StatusCode} for {method} {path}",
			inner: null,
			response.StatusCode);
	}

	private sealed record KeycloakOrganizationResponse(
		string Id,
		string Name);

	private sealed record KeycloakRole(
		string Id,
		string Name);

	private sealed record KeycloakUserResponse(
		string Id,
		string Username,
		string? FirstName,
		string? LastName,
		string? Email);
}
