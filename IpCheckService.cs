using System.Net;

namespace IpWatch;

/// <summary>Быстрая проверка только внешнего IP — без геоданных, дешёвый запрос.</summary>
internal static class IpCheckService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(15)
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IPWatch/1.0 (+desktop tray app)");
        return client;
    }

    public static async Task<string?> GetIpAsync(CancellationToken ct)
    {
        foreach (var url in new[] { "https://api.ipify.org", "https://icanhazip.com", "https://checkip.amazonaws.com" })
        {
            try
            {
                var text = (await Http.GetStringAsync(url, ct).ConfigureAwait(false)).Trim();
                if (IPAddress.TryParse(text, out var address)) return address.ToString();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // пробуем следующий сервис
            }
        }

        return null;
    }
}
