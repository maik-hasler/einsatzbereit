using System.Globalization;
using System.Text;

namespace Infrastructure.Geocoding.GermanCities;

internal static class GermanCityNameNormalizer
{
	// Folds German umlauts/eszett the way people type them on a non-German
	// keyboard (Muenchen, Koeln, Weissenfels) before stripping any remaining
	// diacritics, so "Muenchen" and "München" normalize to the same key.
	public static string Normalize(string value)
	{
		var folded = value
			.ToLowerInvariant()
			.Replace("ß", "ss")
			.Replace("ä", "ae")
			.Replace("ö", "oe")
			.Replace("ü", "ue");

		var decomposed = folded.Normalize(NormalizationForm.FormD);
		var builder = new StringBuilder(decomposed.Length);

		foreach (var character in decomposed)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
				builder.Append(character);
		}

		return builder.ToString();
	}
}
