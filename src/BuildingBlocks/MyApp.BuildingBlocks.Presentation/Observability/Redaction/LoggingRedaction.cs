using Microsoft.AspNetCore.Http;
using System.Text;

namespace MyApp.BuildingBlocks.Presentation.Observability.Redaction;

internal static class LoggingRedaction
{
	private const string Redacted = "<redacted>";

	public static string SanitizeQueryString(IQueryCollection query, IReadOnlySet<string> denyKeys, int maxLen)
	{
		if (query.Count == 0) return string.Empty;

		var sb = new StringBuilder();
		sb.Append('?');

		var first = true;
		foreach (var (key, values) in query)
		{
			if (!first) sb.Append('&');
			first = false;

			sb.Append(key);
			sb.Append('=');

			if (denyKeys.Contains(key))
			{
				sb.Append(Redacted);
				continue;
			}

			var raw = values.Count switch
			{
				0 => string.Empty,
				1 => values[0],
				_ => string.Join(",", values.ToArray())
			};

			sb.Append(Truncate(raw, maxLen));
		}

		return sb.ToString();
	}

	public static Dictionary<string, string> SanitizeHeaders(
		IHeaderDictionary headers,
		IReadOnlySet<string> allow,
		IReadOnlySet<string> deny,
		int maxLen)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (var (key, values) in headers)
		{
			if (deny.Contains(key))
			{
				result[key] = Redacted;
				continue;
			}

			if (allow.Count > 0 && !allow.Contains(key))
				continue;

			var raw = values.Count switch
			{
				0 => string.Empty,
				1 => values[0],
				_ => string.Join(",", values.ToArray())
			};

			result[key] = Truncate(raw, maxLen);
		}

		return result;
	}

	private static string Truncate(string? value, int maxLen)
	{
		if (string.IsNullOrEmpty(value)) return string.Empty;
		if (value.Length <= maxLen) return value;
		return value[..maxLen] + "…";
	}
}