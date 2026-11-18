using System.Globalization;
using System.Net;
using System.Text.Json;

namespace IpWatch;

internal static class IpLookupService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(6)
        })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IPWatch/1.0 (+desktop tray app)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    public static async Task<IpInfo> GetAsync(CancellationToken ct)
    {
        var errors = new List<string>();

        foreach (var provider in new Func<CancellationToken, Task<IpInfo?>>[] { FromIpWhoIs, FromIpApi, FromIpApiCo })
        {
            try
            {
                var info = await provider(ct).ConfigureAwait(false);
                if (info != null && !string.IsNullOrWhiteSpace(info.Ip)) return info;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        throw new InvalidOperationException("Не удалось получить данные об IP-адресе" +
                                            (errors.Count > 0 ? " (" + string.Join("; ", errors.Distinct()) + ")" : ""));
    }

    // https://ipwho.is/ — HTTPS, без ключа, отдаёт страну, город, провайдера и часовой пояс.
    private static async Task<IpInfo?> FromIpWhoIs(CancellationToken ct)
    {
        using var doc = await GetJsonAsync("https://ipwho.is/", ct).ConfigureAwait(false);
        var root = doc.RootElement;

        if (root.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.False) return null;

        var ip = Str(root, "ip");
        if (string.IsNullOrWhiteSpace(ip)) return null;

        var offset = 0;
        var tzId = "";
        if (root.TryGetProperty("timezone", out var tz) && tz.ValueKind == JsonValueKind.Object)
        {
            tzId = Str(tz, "id");
            if (tz.TryGetProperty("offset", out var off) && off.TryGetInt32(out var o)) offset = o;
        }

        return new IpInfo(
            Ip: ip!,
            CountryName: Str(root, "country"),
            CountryCode: Str(root, "country_code"),
            Region: Str(root, "region"),
            City: Str(root, "city"),
            Isp: Conn(root, "isp") is { Length: > 0 } isp ? isp : Conn(root, "org"),
            TimeZoneId: tzId,
            UtcOffsetSeconds: offset);
    }

    // http://ip-api.com/json/ — бесплатный резерв.
    private static async Task<IpInfo?> FromIpApi(CancellationToken ct)
    {
        using var doc = await GetJsonAsync(
            "http://ip-api.com/json/?fields=status,message,country,countryCode,regionName,city,timezone,offset,isp,org,query",
            ct).ConfigureAwait(false);
        var root = doc.RootElement;

        if (!root.TryGetProperty("status", out var status) || status.GetString() != "success") return null;

        var ip = Str(root, "query");
        if (string.IsNullOrWhiteSpace(ip)) return null;

        var offset = 0;
        if (root.TryGetProperty("offset", out var off) && off.TryGetInt32(out var o)) offset = o;

        var isp = Str(root, "isp");
        if (string.IsNullOrWhiteSpace(isp)) isp = Str(root, "org");

        return new IpInfo(
            Ip: ip!,
            CountryName: Str(root, "country"),
            CountryCode: Str(root, "countryCode"),
            Region: Str(root, "regionName"),
            City: Str(root, "city"),
            Isp: isp,
            TimeZoneId: Str(root, "timezone"),
            UtcOffsetSeconds: offset);
    }

    // https://ipapi.co/json/ — ещё один резерв.
    private static async Task<IpInfo?> FromIpApiCo(CancellationToken ct)
    {
        using var doc = await GetJsonAsync("https://ipapi.co/json/", ct).ConfigureAwait(false);
        var root = doc.RootElement;

        var ip = Str(root, "ip");
        if (string.IsNullOrWhiteSpace(ip)) return null;

        var offset = 0;
        var rawOffset = Str(root, "utc_offset");
        if (rawOffset.Length == 5 &&
            int.TryParse(rawOffset.AsSpan(1, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) &&
            int.TryParse(rawOffset.AsSpan(3, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var m))
        {
            offset = (h * 3600 + m * 60) * (rawOffset[0] == '-' ? -1 : 1);
        }

        return new IpInfo(
            Ip: ip!,
            CountryName: Str(root, "country_name"),
            CountryCode: Str(root, "country_code"),
            Region: Str(root, "region"),
            City: Str(root, "city"),
            Isp: Str(root, "org"),
            TimeZoneId: Str(root, "timezone"),
            UtcOffsetSeconds: offset);
    }

    private static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        await using var stream = await Http.GetStreamAsync(url, ct).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
    }

    private static string Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string Conn(JsonElement element, string name) =>
        element.TryGetProperty("connection", out var conn) && conn.ValueKind == JsonValueKind.Object
            ? Str(conn, name)
            : "";
}
