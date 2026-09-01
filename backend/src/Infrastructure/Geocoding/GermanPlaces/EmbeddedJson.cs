using System.Reflection;
using System.Text.Json;

namespace Infrastructure.Geocoding.GermanPlaces;

internal static class EmbeddedJson
{
	public static IReadOnlyList<T> Load<T>(string resourceName)
	{
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
			?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");

		return JsonSerializer.Deserialize<IReadOnlyList<T>>(stream)
			?? throw new InvalidOperationException($"Embedded resource '{resourceName}' is empty or invalid.");
	}
}
