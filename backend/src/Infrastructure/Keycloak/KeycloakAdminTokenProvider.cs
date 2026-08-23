using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Keycloak;

internal sealed class KeycloakAdminTokenProvider(
	IHttpClientFactory httpClientFactory,
	IOptions<KeycloakOptions> options,
	ILogger<KeycloakAdminTokenProvider> logger)
{
	public const string HttpClientName = "keycloak-admin-token";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private readonly KeycloakOptions _options = options.Value;
	private readonly SemaphoreSlim _lock = new(1, 1);

	private string? _token;
	private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

	public async Task<string> GetTokenAsync(
		bool forceRefresh,
		CancellationToken cancellationToken = default)
	{
		if (!forceRefresh && _token is not null && DateTimeOffset.UtcNow < _expiresAt)
		{
			return _token;
		}

		await _lock.WaitAsync(cancellationToken);
		try
		{
			if (!forceRefresh && _token is not null && DateTimeOffset.UtcNow < _expiresAt)
			{
				return _token;
			}

			var token = await RequestTokenAsync(cancellationToken);

			_token = token.AccessToken;

			var safetySeconds = Math.Min(30, token.ExpiresIn / 2);
			_expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn - safetySeconds);

			return _token;
		}
		finally
		{
			_lock.Release();
		}
	}

	private async Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken)
	{
		var client = httpClientFactory.CreateClient(HttpClientName);

		using var tokenRequest = new FormUrlEncodedContent([
			new KeyValuePair<string, string>("grant_type", "client_credentials"),
			new KeyValuePair<string, string>("client_id", _options.ClientId),
			new KeyValuePair<string, string>("client_secret", _options.ClientSecret)
		]);

		using var response = await client.PostAsync(
			$"/realms/{_options.Realm}/protocol/openid-connect/token",
			tokenRequest,
			cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			logger.LogWarning(
				"Keycloak token request failed with {StatusCode}",
				(int)response.StatusCode);

			throw new HttpRequestException(
				$"Keycloak token request failed with {(int)response.StatusCode} {response.StatusCode}",
				inner: null,
				response.StatusCode);
		}

		return await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken)
			?? throw new InvalidOperationException("Keycloak returned an empty token response.");
	}

	private sealed record TokenResponse(
		[property: JsonPropertyName("access_token")] string AccessToken,
		[property: JsonPropertyName("expires_in")] int ExpiresIn);
}
