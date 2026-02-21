using System.Text.Json;

namespace MyApp.Presentation.Observability.Redaction;

internal static class JsonBodyRedactor
{
    public static string RedactIfJson(string raw, HashSet<string> denyKeys)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            using var ms = new MemoryStream();
            using var w = new Utf8JsonWriter(ms);

            WriteValue(doc.RootElement, w, denyKeys);

            w.Flush();
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            return raw; // if parse fails, keep truncated raw
        }
    }

    private static void WriteValue(JsonElement el, Utf8JsonWriter w, HashSet<string> denyKeys)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteStartObject();
                foreach (var p in el.EnumerateObject())
                {
                    w.WritePropertyName(p.Name);

                    if (denyKeys.Contains(p.Name))
                    {
                        w.WriteStringValue("<redacted>");
                        continue;
                    }

                    WriteValue(p.Value, w, denyKeys);
                }
                w.WriteEndObject();
                break;

            case JsonValueKind.Array:
                w.WriteStartArray();
                foreach (var item in el.EnumerateArray())
                    WriteValue(item, w, denyKeys);
                w.WriteEndArray();
                break;

            default:
                el.WriteTo(w);
                break;
        }
    }
}
