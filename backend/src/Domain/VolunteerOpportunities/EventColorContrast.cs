using System.Globalization;

namespace Domain.VolunteerOpportunities;

/// <summary>
/// Organizers pick a raw calendar event color via an unconstrained OS color
/// picker (see CalendarWidget.tsx) which then renders as a chip - the app
/// flips the chip's text between white and near-black based on the color's
/// luminance, but the color itself still needs to clear WCAG 1.4.11's 3:1
/// non-text-contrast floor against the white calendar background, or the
/// chip becomes invisible as a UI component regardless of its text.
/// Mirrors the frontend's lib/colorContrast.ts formula (WCAG relative
/// luminance) so client and server agree on what "readable" means.
/// </summary>
public static class EventColorContrast
{
	public const double MinimumContrastRatio = 3.0;

	public static bool IsValidHex(string color)
	{
		if (color.Length is not (4 or 7) || color[0] != '#')
			return false;

		return color[1..].All(Uri.IsHexDigit);
	}

	public static double ContrastAgainstWhite(string hexColor)
	{
		var luminance = RelativeLuminance(hexColor);
		return (1.0 + 0.05) / (luminance + 0.05);
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
