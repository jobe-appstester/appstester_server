using System;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpAdbClient;

namespace AppsTester.Checker.Android.Adb
{
    internal interface IAdbClientProvider : IDisposable
    {
        IAdbClient GetAdbClient();
    }

    internal class AdbClientProvider : IAdbClientProvider
    {
        private IAdbClient _adbClient;
        private IDeviceMonitor _deviceMonitor;
        private readonly ILogger<AdbClientProvider> _logger;

        public AdbClientProvider(IOptions<AdbOptions> adbOptions, ILogger<AdbClientProvider> logger)
        {
            var dnsEndPoint = new DnsEndPoint(adbOptions.Value.Host, port: 5037);
            _logger = logger;

            SetupAdbClient(adbOptions.Value, dnsEndPoint);
            SetupDeviceMonitor(dnsEndPoint);
        }
        private void SetupAdbClient(AdbOptions configuration, EndPoint dnsEndPoint)
        {
            _adbClient = new AdbClient(dnsEndPoint, adbSocketFactory: Factories.AdbSocketFactory);

            _logger.LogInformation("Connecting to ADB server at {adbHost}:5037.", configuration.Host);

            var version = _adbClient.GetAdbVersion();

            _logger.LogInformation(
                "Successfully connected to ADB server at {adbHost}:5037. Version is {version}.",
                configuration.Host,
                version);
        }

        private void SetupDeviceMonitor(EndPoint dnsEndPoint)
        {
            _deviceMonitor = new DeviceMonitor(new AdbSocket(dnsEndPoint));

            _deviceMonitor.DeviceConnected += (_, args) => _logger.LogInformation("Connected device with serial {Serial}", args.Device.Serial);

            _deviceMonitor.DeviceDisconnected += (_, args) => _logger.LogInformation("Disconnected device with serial {Serial}", args.Device.Serial);

            _deviceMonitor.DeviceChanged += (_, args) => _logger.LogInformation("Changed device with serial {Serial}", args.Device.Serial);

            _deviceMonitor.Start();
        }

        public IAdbClient GetAdbClient() => _adbClient;

        public void Dispose()
        {
            _deviceMonitor.Dispose();
        }
    }
}