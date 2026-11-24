using System.Drawing;
using System.Net.Http;

namespace IpWatch;

internal static class FlagCache
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly Dictionary<string, Image?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<Image?> GetAsync(string countryCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2) return null;
        var code = countryCode.ToLowerInvariant();

        lock (Cache)
        {
            if (Cache.TryGetValue(code, out var cached)) return cached;
        }

        Image? image = null;
        try
        {
            var bytes = await Http.GetByteArrayAsync($"https://flagcdn.com/w80/{code}.png", ct).ConfigureAwait(false);
            using var ms = new MemoryStream(bytes);
            using var loaded = Image.FromStream(ms);
            image = new Bitmap(loaded);
        }
        catch (Exception)
        {
            image = null;
        }

        lock (Cache)
        {
            if (image != null) Cache[code] = image;
        }

        return image;
    }
}
