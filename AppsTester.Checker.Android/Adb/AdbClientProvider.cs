using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
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

        public AdbClientProvider(IConfiguration configuration, IOptions<AdbOptions> adbOptions, ILogger<AdbClientProvider> logger)
        {
            var dnsEndPoint = new DnsEndPoint(adbOptions.Value.Host, port: 5037);
            _logger = logger;

            SetupAdbServer(Path.Combine(configuration.GetValue<string>("ANDROID_SDK_ROOT"), "platform-tools", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "adb.exe" : "adb"));
            SetupAdbClient(adbOptions.Value, logger, dnsEndPoint);
            SetupDeviceMonitor(logger, dnsEndPoint);
        }
        private void SetupAdbServer(string adbPath)
        {
            if (!AdbServer.Instance.GetStatus().IsRunning)
            {
                _logger.LogInformation("ADB server {adbPath} is not running", adbPath);
                var startResult = AdbServer.Instance.StartServer(adbPath, false);
                _logger.LogInformation("ADB server start result {startResult}", startResult);
            }

        }
        private void SetupAdbClient(AdbOptions configuration, ILogger logger, EndPoint dnsEndPoint)
        {
            _adbClient = new AdbClient(dnsEndPoint, adbSocketFactory: Factories.AdbSocketFactory);

            logger.LogInformation("Connecting to ADB server at {adbHost}:5037.", configuration.Host);

            var version = _adbClient.GetAdbVersion();

            logger.LogInformation(
                "Successfully connected to ADB server at {adbHost}:5037. Version is {version}.",
                configuration.Host,
                version);
        }

        private void SetupDeviceMonitor(ILogger logger, EndPoint dnsEndPoint)
        {
            _deviceMonitor = new DeviceMonitor(new AdbSocket(dnsEndPoint));

            _deviceMonitor.DeviceConnected += (_, args) =>
                logger.LogInformation("Connected device with serial {Serial}", args.Device.Serial);

            _deviceMonitor.DeviceDisconnected += (_, args) =>
                logger.LogInformation("Disconnected device with serial {Serial}", args.Device.Serial);

            _deviceMonitor.DeviceChanged += (_, args) =>
                logger.LogInformation("Changed device with serial {Serial}", args.Device.Serial);

            _deviceMonitor.Start();
        }

        public IAdbClient GetAdbClient() => _adbClient;

        public void Dispose()
        {
            _deviceMonitor.Dispose();
        }
    }
}