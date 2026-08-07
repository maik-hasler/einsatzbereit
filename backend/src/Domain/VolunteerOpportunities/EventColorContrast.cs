using System.Globalization;

namespace Domain.VolunteerOpportunities;

/// <summary>
/// Organizers pick a raw calendar event color via an unconstrained OS color
/// picker (see CalendarWidget.tsx) which then renders as a chip - the app
/// flips the chip's text between white and near-black based on the color's
/// luminance. Two independent WCAG floors apply to that color:
/// <list type="bullet">
/// <item><see cref="MinimumContrastRatio"/> (1.4.11, non-text): the chip
/// itself must clear 3:1 against the white calendar background, or it
/// becomes invisible as a UI component regardless of its text.</item>
/// <item><see cref="MinimumTextContrastRatio"/> (1.4.3, text): the better of
/// white/near-black text on top of the chip must independently clear 4.5:1.
/// The two candidates cross over around 4.16:1 (einsatzbereit#1726) - a
/// color can comfortably clear the 3:1 chip floor while both text options
/// still fall short of 4.5:1, so this can't be derived from the first
/// check.</item>
/// </list>
/// Mirrors the frontend's lib/colorContrast.ts formula (WCAG relative
/// luminance) so client and server agree on what "readable" means.
/// </summary>
public static class EventColorContrast
{
	public const double MinimumContrastRatio = 3.0;
	public const double MinimumTextContrastRatio = 4.5;

	private const string DarkTextColor = "#111827";

	public static bool IsValidHex(string color)
	{
		if (color.Length is not (4 or 7) || color[0] != '#')
			return false;

		return color[1..].All(Uri.IsHexDigit);
	}

	public static double ContrastAgainstWhite(string hexColor) =>
		ContrastRatio(1.0, RelativeLuminance(hexColor));

	/// <summary>The higher of white-on-color and near-black-on-color contrast - whichever text color the chip would actually use.</summary>
	public static double BestTextContrastRatio(string hexColor)
	{
		var backgroundLuminance = RelativeLuminance(hexColor);
		var whiteContrast = ContrastRatio(1.0, backgroundLuminance);
		var darkContrast = ContrastRatio(RelativeLuminance(DarkTextColor), backgroundLuminance);
		return Math.Max(whiteContrast, darkContrast);
	}

	private static double ContrastRatio(double luminanceA, double luminanceB)
	{
		var lighter = Math.Max(luminanceA, luminanceB);
		var darker = Math.Min(luminanceA, luminanceB);
		return (lighter + 0.05) / (darker + 0.05);
	}

	private static double RelativeLuminance(string hexColor)
	{
		var (r, g, b) = ToRgb(hexColor);
		return 0.2126 * ToLinear(r) + 0.7152 * ToLinear(g) + 0.0722 * ToLinear(b);
	}

	private static double ToLinear(int channel)
	{
		var c = channel / 255.0;
		return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
	}

	private static (int R, int G, int B) ToRgb(string hexColor)
	{
		var hex = hexColor[1..];
		if (hex.Length == 3)
			hex = string.Concat(hex.Select(c => new string(c, 2)));

		var r = int.Parse(hex[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		var g = int.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		var b = int.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		return (r, g, b);
	}
}
