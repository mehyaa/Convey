using Convey.Types;
using System;
using System.Net;
using System.Net.Sockets;

namespace Convey;

internal class ServiceId : IServiceId
{
    private readonly AppOptions _appOptions;

    public ServiceId(AppOptions appOptions)
    {
        _appOptions = appOptions;
    }

    private string _id;

    public string Id
    {
        get
        {
            if (!string.IsNullOrEmpty(_id))
            {
                return _id;
            }

            if (IsInDocker())
            {
                // Environment.MachineName -> Container ID
                _id = $"{_appOptions.Service}:{Environment.MachineName}";
            }
            else
            {
                _id = $"{_appOptions.Service}:{GetHostIpAddress()}-{Environment.ProcessId}";
            }

            return _id;
        }
    }

    private static string GetHostIpAddress()
    {
        var ip =
            Array.Find(
                Dns.GetHostEntry(Dns.GetHostName()).AddressList,
                i => i.AddressFamily == AddressFamily.InterNetwork);

        return ip?.ToString() ?? throw new ArgumentException("IP address not found");
    }

    private static bool IsInDocker()
    {
        return Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
    }
}