using System.Net;
using System.Text;
using Application.Common.Geocoding;
using AwesomeAssertions;
using Infrastructure.Geocoding;
using Infrastructure.Geocoding.GermanPlaces;
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

		// An exonym, so the local index steps aside and the request is actually sent.
		await sut.SearchCitiesAsync("Munich", "en");

		handler.LastRequest.Should().NotBeNull();
		handler.LastRequest!.RequestUri!.Query.Should().Contain("countrycodes=de");
	}

	[Test]
	public async Task SearchCitiesAsync_PostalCodeMatch_KeepsTheGermanCityAndDropsInternationalNoise()
	{
		// 70140 is a company's "Grossempfaenger" code, which the local index drops -
		// so this reaches Nominatim, which is the half under test here.
		const string response = """
			[
				{"lat":"44.5834","lon":"-68.7873","addresstype":"postcode","address":{"city":"Bucksport"}},
				{"lat":"37.5384","lon":"127.0016","addresstype":"postcode","address":{"city":"Seoul"}},
				{"lat":"51.2833","lon":"12.3500","addresstype":"postcode","address":{"town":"Markkleeberg"}}
			]
			""";
		var sut = CreateService(new StubHandler(_ => JsonResponse(response)));

		var results = await sut.SearchCitiesAsync("70140", "de");

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

		// "9" guarantees no real German city name can prefix-match, so this
		// isolates the assertion to the addresstype filter above - the local
		// fallback (tested separately below) never gets a chance to mask a
		// filtering bug by finding an unrelated real city.
		var results = await sut.SearchCitiesAsync("9nonexistent", "de");

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

		// A query no German city name can match, so the local index steps aside and
		// leaves the remote response as the only source of the result.
		var results = await sut.SearchCitiesAsync("Leipzig-sur-Mer", "de");

		results.Should().ContainSingle(r =>
			r.Label == "Leipzig" && r.Latitude == 51.3397 && r.Longitude == 12.3731);
	}

	[Test]
	public async Task SearchCitiesAsync_LocalMatch_IsAnsweredWithoutAskingNominatim()
	{
		// Every remote call queues behind the shared throttle, which a
		// search-as-you-type field cannot afford - and Nominatim answers a prefix
		// like "Leip" with roads and lakes anyway (#2227).
		var handler = new StubHandler(_ => JsonResponse("""
			[{"lat":"1.0","lon":"2.0","addresstype":"city","address":{"city":"Leipzig-on-Sea"}}]
			"""));
		var sut = CreateService(handler);

		var results = await sut.SearchCitiesAsync("Leip", "de");

		results.Should().Contain(r => r.Label == "Leipzig");
		handler.LastRequest.Should().BeNull();
	}

	[Test]
	public async Task SearchCitiesAsync_UmlautFoldedPrefix_StillMatchesTheLocalDirectory()
	{
		var sut = CreateService(new StubHandler(_ => JsonResponse("[]")));

		var results = await sut.SearchCitiesAsync("Muenchen", "de");

		results.Should().Contain(r => r.Label == "München");
	}

	[Test]
	public async Task SearchCitiesAsync_ExonymTheLocalDirectoryDoesNotCarry_FallsBackToNominatim()
	{
		// The embedded gazetteer holds German endonyms only, so an English speaker
		// typing "Munich" is exactly the case Nominatim is kept around for.
		const string response = """
			[
				{"lat":"48.1374","lon":"11.5755","addresstype":"city","address":{"city":"Munich"}}
			]
			""";
		var sut = CreateService(new StubHandler(_ => JsonResponse(response)));

		var results = await sut.SearchCitiesAsync("Munich", "en");

		results.Should().ContainSingle(r => r.Label == "Munich");
	}

	[Test]
	public async Task SearchCitiesAsync_RemoteRequestFails_LeavesTheFieldWithoutSuggestions()
	{
		var sut = CreateService(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

		var results = await sut.SearchCitiesAsync("Munich", "en");

		results.Should().BeEmpty();
	}

	[Test]
	public async Task GeocodeAsync_StreetNominatimCannotMatch_FallsBackToTheCity()
	{
		// A structured query only answers when Nominatim can match the street and house
		// number too. Reporting NotFound for a real address on a road OSM does not carry
		// marked the opportunity permanently un-geocoded, so every radius search skipped
		// it while its card still showed a city map pin (#2319).
		var handler = new SequencedStubHandler(
			JsonResponse("[]"),
			JsonResponse("""[{"lat":"51.3396","lon":"12.3713"}]"""));
		var sut = CreateService(handler);

		var result = await sut.GeocodeAsync("Tierparkweg", "5", "04177", "Leipzig");

		result.Outcome.Should().Be(GeocodingOutcome.Found);
		result.Coordinates!.Latitude.Should().BeApproximately(51.3396, 0.0001);

		handler.Requests.Should().HaveCount(2);
		handler.Requests[1].RequestUri!.Query.Should()
			.Contain("city=Leipzig").And.NotContain("street=");
	}

	[Test]
	public async Task GeocodeAsync_ExactAddressMatch_DoesNotAskForTheCityAsWell()
	{
		var handler = new SequencedStubHandler(
			JsonResponse("""[{"lat":"51.3396","lon":"12.3713"}]"""));
		var sut = CreateService(handler);

		var result = await sut.GeocodeAsync("Tierparkweg", "5", "04177", "Leipzig");

		result.Outcome.Should().Be(GeocodingOutcome.Found);
		handler.Requests.Should().ContainSingle("the exact address resolved, so there is nothing to fall back to");
	}

	[Test]
	public async Task GeocodeAsync_NeitherAddressNorCityResolves_StaysNotFound()
	{
		var sut = CreateService(new StubHandler(_ => JsonResponse("[]")));

		var result = await sut.GeocodeAsync("Nowhere Lane", "1", "00000", "Wolkenkuckucksheim");

		result.Outcome.Should().Be(GeocodingOutcome.NotFound);
	}

	[Test]
	public async Task GeocodeAsync_TransientFailureOnTheAddress_IsNotRetriedAsACityLookup()
	{
		// A transient failure is the retry job's business - swapping it for a coarser
		// city-level hit would quietly downgrade an address that is merely unreachable.
		var handler = new SequencedStubHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
		var sut = CreateService(handler);

		var result = await sut.GeocodeAsync("Tierparkweg", "5", "04177", "Leipzig");

		result.Outcome.Should().Be(GeocodingOutcome.TransientFailure);
		handler.Requests.Should().ContainSingle();
	}

	private static NominatimGeocodingService CreateService(HttpMessageHandler handler) =>
		new(
			new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.example/") },
			Options.Create(new GeocodingOptions { MinRequestIntervalMilliseconds = 0 }),
			NullLogger<NominatimGeocodingService>.Instance,
			new GermanPlaceDirectory());

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

	private sealed class SequencedStubHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
	{
		private int _index;

		public List<HttpRequestMessage> Requests { get; } = [];

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Requests.Add(request);
			var response = _index < responses.Length ? responses[_index] : responses[^1];
			_index++;
			return Task.FromResult(response);
		}
	}
}
