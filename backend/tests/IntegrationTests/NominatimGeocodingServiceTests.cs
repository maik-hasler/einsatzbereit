using System.Net;
using System.Text;
using Application.Common.Geocoding;
using AwesomeAssertions;
using Infrastructure.Geocoding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IntegrationTests;

// Exercises Infrastructure.Geocoding.NominatimGeocodingService directly
// (InternalsVisibleTo, see Infrastructure.csproj) against a stubbed HttpClient
// instead of the real Nominatim API - like SmtpEmailServiceTests, it needs no
// Aspire/Testcontainers fixture.
//
// Regression coverage for #1900: searching "04416" (Markkleeberg's real
// postal code) used to rank Bucksport (Maine, USA) and Seoul above the
// correct German city, and "Leip" surfaced a Hamburg street literally named
// "Hans-Leip-Straße" mislabeled as a "Hamburg" suggestion via its address
// breakdown. Both fixtures below are trimmed from real Nominatim responses
// captured live while investigating the bug. The two root causes fixed here:
// `countrycodes=de` now scopes the request to Germany, and results are kept
// only when Nominatim's own `addresstype` marks them as an actual place/
// postcode - `featuretype=city` looked like it did this but is a no-op for
// free-text `q=` searches, verified against the live API.
public class NominatimGeocodingServiceTests
{
	[Test]
	public async Task SearchCitiesAsync_AnyQuery_RestrictsTheRequestToGermany()
	{
		var handler = new StubHandler(_ => JsonResponse("[]"));
		var sut = CreateService(handler);

		await sut.SearchCitiesAsync("Leipzig", "de");

		handler.LastRequest.Should().NotBeNull();
		handler.LastRequest!.RequestUri!.Query.Should().Contain("countrycodes=de");
	}

	[Test]
	public async Task SearchCitiesAsync_PostalCodeMatch_KeepsTheGermanCityAndDropsInternationalNoise()
	{
		// Trimmed from the live, unfiltered response for q=04416 - #1900.
		const string response = """
			[
				{"lat":"44.5834","lon":"-68.7873","addresstype":"postcode","address":{"city":"Bucksport"}},
				{"lat":"37.5384","lon":"127.0016","addresstype":"postcode","address":{"city":"Seoul"}},
				{"lat":"51.2833","lon":"12.3500","addresstype":"postcode","address":{"town":"Markkleeberg"}}
			]
			""";
		var sut = CreateService(new StubHandler(_ => JsonResponse(response)));

		var results = await sut.SearchCitiesAsync("04416", "de");

		// A live call would never even receive the Bucksport/Seoul entries once
		// countrycodes=de is applied - this fixture instead locks down that a
		// postcode-type match (as opposed to a city-type match) still resolves
		// to the correct label via its address breakdown.
		results.Should().ContainSingle(r => r.Label == "Markkleeberg");
	}

	[Test]
	public async Task SearchCitiesAsync_StreetNamedAfterTheQuery_IsNotMislabeledWithItsContainingCity()
	{
		// Trimmed from the live response for q=Leip, countrycodes=de - #1900.
		// Hans-Leip-Straße is a real Hamburg street, not a match for a city
		// named "Leip"; its address breakdown still carries "Hamburg" as the
		// containing city, which the old (silently ignored) featuretype=city
		// param never filtered out.
		const string response = """
			[
				{"lat":"52.4333","lon":"14.0333","addresstype":"water","address":{"city":"Beeskow"}},
				{"lat":"53.55","lon":"9.9500","addresstype":"road","address":{"city":"Hamburg"}}
			]
			""";
		var sut = CreateService(new StubHandler(_ => JsonResponse(response)));

		var results = await sut.SearchCitiesAsync("Leip", "de");

		results.Should().BeEmpty();
	}

	[Test]
	public async Task SearchCitiesAsync_GenuineCityMatch_IsReturned()
	{
		const string response = """
			[
				{"lat":"51.3397","lon":"12.3731","addresstype":"city","address":{"city":"Leipzig"}}
			]
			""";
		var sut = CreateService(new StubHandler(_ => JsonResponse(response)));

		var results = await sut.SearchCitiesAsync("Leipzig", "de");

		results.Should().ContainSingle(r =>
			r.Label == "Leipzig" && r.Latitude == 51.3397 && r.Longitude == 12.3731);
	}

	private static NominatimGeocodingService CreateService(HttpMessageHandler handler) =>
		new(
			new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.example/") },
			Options.Create(new GeocodingOptions { MinRequestIntervalMilliseconds = 0 }),
			NullLogger<NominatimGeocodingService>.Instance);

	private static HttpResponseMessage JsonResponse(string json) =>
		new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

	private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		public HttpRequestMessage? LastRequest { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, CancellationToken cancellationToken)
		{
			LastRequest = request;
			return Task.FromResult(respond(request));
		}
	}
}
