using System.Globalization;
using System.Text;

namespace Domain.Common;

public static class SlugGenerator
{
	public static string Generate(string name)
	{
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

		var slug = sb.ToString().ToLowerInvariant();

		sb.Clear();
		var prevHyphen = true;
		foreach (var c in slug)
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
}
