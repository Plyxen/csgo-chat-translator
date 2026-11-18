using System.Text;
using System.Text.Json;

namespace CsgoChatTranslator;

public sealed class TranslationFailedException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Uses free web translation endpoints that need no key or account. They rate-limit without warning,
/// so there are three routes; a route that fails is skipped and the one that last worked goes first.
/// </summary>
public sealed class Translator : IDisposable
{
    private const int MaxCacheEntries = 500;

    private sealed record Route(string Name, Func<string, string, string, string> Url, Func<string, string> Parse);

    private static readonly Route[] Routes =
    [
        new("google", (text, from, to) =>
            $"https://translate.googleapis.com/translate_a/single?client=gtx&dt=t&sl={from}&tl={to}&q={text}", ParseResponse),
        new("google-2", (text, from, to) =>
            $"https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl={from}&tl={to}&q={text}", ParseSimpleResponse),
        new("mymemory", (text, from, to) =>
            $"https://api.mymemory.translated.net/get?q={text}&langpair={from}|{to}", ParseMyMemoryResponse),
    ];

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly Dictionary<string, string> _cache = [];
    private volatile int _preferred;

    public Translator() => _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

    /// <summary>The route that answered last, for the status window.</summary>
    public string LastRoute { get; private set; } = Routes[0].Name;

    public async Task<string> TranslateAsync(string text, string from, string to, CancellationToken ct)
    {
        var key = $"{from}>{to}:{text}";
        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;
        }

        var (q, sl, tl) = (Uri.EscapeDataString(text), Uri.EscapeDataString(from), Uri.EscapeDataString(to));
        var first = _preferred;
        Exception? last = null;

        for (var attempt = 0; attempt < Routes.Length; attempt++)
        {
            var index = (first + attempt) % Routes.Length;
            var route = Routes[index];
            try
            {
                using var response = await _http.GetAsync(route.Url(q, sl, tl), ct);
                response.EnsureSuccessStatusCode();
                var result = route.Parse(await response.Content.ReadAsStringAsync(ct));

                _preferred = index;
                LastRoute = route.Name;
                lock (_cache)
                {
                    if (_cache.Count >= MaxCacheEntries) _cache.Clear();
                    _cache[key] = result;
                }
                return result;
            }
            catch (Exception e) when (!ct.IsCancellationRequested &&
                e is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or InvalidOperationException)
            {
                last = e;
            }
        }
        throw new TranslationFailedException("No translation service answered.", last);
    }

    /// <summary>[[["Hello","привет",...],...],...]</summary>
    public static string ParseResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0 ||
            root[0].ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Unexpected translation response.");
        }

        var sb = new StringBuilder();
        foreach (var segment in root[0].EnumerateArray())
        {
            if (segment.ValueKind == JsonValueKind.Array && segment.GetArrayLength() > 0 &&
                segment[0].ValueKind == JsonValueKind.String)
            {
                sb.Append(segment[0].GetString());
            }
        }
        return NotEmpty(sb.ToString());
    }

    /// <summary>["Hello"] or [["Hello","ru"]]</summary>
    public static string ParseSimpleResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            throw new InvalidDataException("Unexpected translation response.");
        }

        var first = root[0];
        if (first.ValueKind == JsonValueKind.Array && first.GetArrayLength() > 0) first = first[0];
        if (first.ValueKind != JsonValueKind.String) throw new InvalidDataException("Unexpected translation response.");
        return NotEmpty(first.GetString()!);
    }

    /// <summary>{"responseData":{"translatedText":"Hello"},"responseStatus":200}</summary>
    public static string ParseMyMemoryResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("responseData", out var data) || data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("translatedText", out var text) || text.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Unexpected translation response.");
        }

        // Quota and error messages arrive in the same field, in capitals.
        var result = System.Net.WebUtility.HtmlDecode(text.GetString()!);
        if (result.Contains("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase) ||
            (root.TryGetProperty("responseStatus", out var status) && status.ToString() is not ("200" or "")))
        {
            throw new InvalidDataException("MyMemory refused the request.");
        }
        return NotEmpty(result);
    }

    private static string NotEmpty(string value)
    {
        var result = value.Trim();
        return result.Length > 0 ? result : throw new InvalidDataException("Empty translation.");
    }

    public void Dispose() => _http.Dispose();
}
