using System.Net;
using System.Text;
using Application.Common.Geocoding;
using AwesomeAssertions;
using Infrastructure.Geocoding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IntegrationTests;

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
		const string response = """
			[
				{"lat":"44.5834","lon":"-68.7873","addresstype":"postcode","address":{"city":"Bucksport"}},
				{"lat":"37.5384","lon":"127.0016","addresstype":"postcode","address":{"city":"Seoul"}},
				{"lat":"51.2833","lon":"12.3500","addresstype":"postcode","address":{"town":"Markkleeberg"}}
			]
			""";
		var sut = CreateService(new StubHandler(_ => JsonResponse(response)));

		var results = await sut.SearchCitiesAsync("04416", "de");

		results.Should().ContainSingle(r => r.Label == "Markkleeberg");
	}

	[Test]
	public async Task SearchCitiesAsync_StreetNamedAfterTheQuery_IsNotMislabeledWithItsContainingCity()
	{
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
