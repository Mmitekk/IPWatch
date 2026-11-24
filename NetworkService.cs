using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace IpWatch;

internal static class NetworkService
{
    private const uint ForwardTypeIndirect = 4;

    public static NetworkSnapshot GetDefaultRoute()
    {
        try
        {
            var rows = ReadForwardTable();
            var best = rows
                .Where(r => r.Destination == 0 && r.Mask == 0 && r.NextHop != 0 && r.Type == ForwardTypeIndirect)
                .OrderBy(r => r.Metric)
                .Cast<ForwardRow?>()
                .FirstOrDefault();

            if (best is { } row)
            {
                var adapter = FindAdapter(row.InterfaceIndex);
                var gateway = new IPAddress(BitConverter.GetBytes(row.NextHop));
                return new NetworkSnapshot(
                    gateway.ToString(),
                    adapter?.LocalIp,
                    adapter?.Name ?? "—");
            }
        }
        catch (Exception)
        {
            // Ниже — запасной путь через NetworkInterface.
        }

        return FallbackRoute();
    }

    private static NetworkSnapshot FallbackRoute()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .Select(n => new
                {
                    Adapter = n,
                    Props = n.GetIPProperties(),
                    Index = TryGetIndex(n)
                })
                .Where(x => x.Props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)))
                .OrderBy(x => x.Index)
                .ToList();

            var picked = candidates.FirstOrDefault();
            if (picked == null) return NetworkSnapshot.Empty;

            var gateway = picked.Props.GatewayAddresses
                .First(g => g.Address.AddressFamily == AddressFamily.InterNetwork).Address.ToString();
            var localIp = picked.Props.UnicastAddresses
                .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(u => u.Address.ToString())
                .FirstOrDefault();

            return new NetworkSnapshot(gateway, localIp, picked.Adapter.Name);
        }
        catch (Exception)
        {
            return NetworkSnapshot.Empty;
        }
    }

    private static int TryGetIndex(NetworkInterface adapter)
    {
        try { return adapter.GetIPProperties().GetIPv4Properties()?.Index ?? int.MaxValue; }
        catch (Exception) { return int.MaxValue; }
    }

    private static (string? LocalIp, string Name)? FindAdapter(uint index)
    {
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (TryGetIndex(adapter) != index) continue;

            var localIp = adapter.GetIPProperties().UnicastAddresses
                .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(u => u.Address.ToString())
                .FirstOrDefault();

            return (localIp, adapter.Name);
        }

        return null;
    }

    private static List<ForwardRow> ReadForwardTable()
    {
        int size = 0;
        _ = GetIpForwardTable(IntPtr.Zero, ref size, false);

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetIpForwardTable(buffer, ref size, false) != 0)
                throw new InvalidOperationException("GetIpForwardTable failed");

            int count = Marshal.ReadInt32(buffer);
            var rows = new List<ForwardRow>(count);
            int rowSize = Marshal.SizeOf<ForwardRow>();
            IntPtr cursor = IntPtr.Add(buffer, 4);

            for (int i = 0; i < count; i++)
            {
                rows.Add(Marshal.PtrToStructure<ForwardRow>(cursor));
                cursor = IntPtr.Add(cursor, rowSize);
            }

            return rows;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ForwardRow
    {
        public uint Destination;
        public uint Mask;
        public uint Policy;
        public uint NextHop;
        public uint InterfaceIndex;
        public uint Type;
        public uint Protocol;
        public uint Age;
        public uint NextHopAs;
        public uint Metric;
        public uint Metric2;
        public uint Metric3;
        public uint Metric4;
        public uint Metric5;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetIpForwardTable(IntPtr table, ref int size, bool order);
}

internal static class PingService
{
    private static readonly Ping Pinger = new();

    public static async Task<int?> IcmpAsync(string host, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attemptCts.CancelAfter(TimeSpan.FromMilliseconds(1500));

                var reply = await Pinger.SendPingAsync(host, TimeSpan.FromMilliseconds(1500), cancellationToken: attemptCts.Token)
                    .ConfigureAwait(false);
                if (reply.Status == IPStatus.Success)
                    return (int)reply.RoundtripTime;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // пробуем следующую попытку
            }
        }

        return null;
    }

    public static async Task<int?> TcpAsync(string host, int port, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attemptCts.CancelAfter(TimeSpan.FromMilliseconds(2000));

                var sw = System.Diagnostics.Stopwatch.StartNew();
                await client.ConnectAsync(host, port, attemptCts.Token).ConfigureAwait(false);
                sw.Stop();
                var ms = (int)sw.ElapsedMilliseconds;
                if (client.Connected) return ms;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // пробуем следующий хост
            }
        }

        return null;
    }

    /// <summary>Пинг до интернета: сначала ICMP до доступных хостов, при блокировке — TCP handshake.</summary>
    public static async Task<int?> InternetAsync(CancellationToken ct)
    {
        // В разных странах публичные DNS (например 1.1.1.1 в Индонезии) могут быть заблокированы провайдерами
        foreach (var host in new[] { "77.88.55.88", "8.47.69.0" /* ipwho.is */, "8.8.8.8", "1.1.1.1" })
        {
            if (ct.IsCancellationRequested) break;
            var icmp = await IcmpAsync(host, ct).ConfigureAwait(false);
            if (icmp != null) return icmp;
        }

        foreach (var host in new[] { "yandex.ru", "google.com" })
        {
            if (ct.IsCancellationRequested) break;
            var tcp = await TcpAsync(host, 443, ct).ConfigureAwait(false);
            if (tcp != null) return tcp;
        }

        return null;
    }
}
