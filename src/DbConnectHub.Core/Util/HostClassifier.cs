using System.Net;

namespace DbConnectHub.Core.Util;

public enum HostKind
{
    /// <summary>localhost / 127.0.0.1 / ::1</summary>
    Local,

    /// <summary>A Docker Compose service name such as "db" — reachable from other containers, not from your PC directly.</summary>
    DockerService,

    /// <summary>10.x / 172.16-31.x / 192.168.x — your LAN or a VPN.</summary>
    Private,

    /// <summary>Anything else: a cloud / production server. Shown with a red badge.</summary>
    Remote
}

public static class HostClassifier
{
    public static HostKind Classify(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return HostKind.Local;
        var h = host.Trim().ToLowerInvariant();

        if (h is "localhost" or "127.0.0.1" or "::1" or "0.0.0.0" or "host.docker.internal" or ".")
            return HostKind.Local;
        if (h.EndsWith(".localhost")) return HostKind.Local;

        if (IPAddress.TryParse(h, out var ip))
        {
            if (IPAddress.IsLoopback(ip)) return HostKind.Local;
            var b = ip.GetAddressBytes();
            if (b.Length == 4)
            {
                if (b[0] == 10) return HostKind.Private;
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return HostKind.Private;
                if (b[0] == 192 && b[1] == 168) return HostKind.Private;
                if (b[0] == 169 && b[1] == 254) return HostKind.Private;
            }
            return HostKind.Remote;
        }

        // No dot -> almost always a docker-compose service name ("db", "postgres", "database").
        if (!h.Contains('.')) return HostKind.DockerService;

        if (h.EndsWith(".local") || h.EndsWith(".internal") || h.EndsWith(".lan") || h.EndsWith(".test"))
            return HostKind.Private;

        return HostKind.Remote;
    }
}
