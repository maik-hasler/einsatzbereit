using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Infrastructure.Keycloak;

internal sealed class KeycloakOrganisationService(
    HttpClient httpClient,
    IOptions<KeycloakOptions> options)
    : IKeycloakOrganisationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly KeycloakOptions _options = options.Value;

    public async Task<Guid> CreateOrganisationAsync(
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

    public async Task AddMemberAsync(
        Guid organisationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var response = await httpClient.PostAsJsonAsync(
            $"/admin/realms/{_options.Realm}/organizations/{organisationId}/members",
            userId.ToString(),
            JsonOptions,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task AssignOrganisatorRoleAsync(
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

    public async Task<IReadOnlyList<KeycloakOrganisation>> GetUserOrganisationenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var response = await httpClient.GetAsync(
            $"/admin/realms/{_options.Realm}/organizations/members/{userId}/organizations",
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var organisations = await response.Content.ReadFromJsonAsync<List<KeycloakOrganisationResponse>>(
            JsonOptions, cancellationToken) ?? [];

        return organisations
            .Select(o => new KeycloakOrganisation(Guid.Parse(o.Id), o.Name))
            .ToList();
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

            // Decompose and strip combining marks for other accented chars
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

        // Replace non-alphanumeric with hyphens, collapse, and trim
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

        // Trim trailing hyphen
        return sb.Length > 0 && sb[^1] == '-'
            ? sb.ToString(0, sb.Length - 1)
            : sb.ToString();
    }

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

    private sealed record KeycloakRole(
        string Id,
        string Name);

    private sealed record KeycloakOrganisationResponse(
        string Id,
        string Name);
}
