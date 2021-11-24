using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharpAdbClient;

namespace AppsTester.Checker.Android.Adb
{
    internal interface IAdbClientProvider
    {
        IAdbClient GetAdbClient();
    }

    internal class AdbClientProvider : IAdbClientProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdbClientProvider> _logger;

        private IAdbClient _adbClient;

        public AdbClientProvider(IConfiguration configuration, ILogger<AdbClientProvider> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public IAdbClient GetAdbClient()
        {
            if (_adbClient != null)
                return _adbClient;

            var dnsEndPoint = new DnsEndPoint(_configuration["Adb:Host"], port: 5037);
            var adbClient = new AdbClient(dnsEndPoint, adbSocketFactory: Factories.AdbSocketFactory);

            _logger.LogInformation($"Connecting to ADB server at {_configuration["Adb:Host"]}:5037.");

            var version = adbClient.GetAdbVersion();
            
            _logger.LogInformation(
                $"Successfully connected to ADB server at {_configuration["Adb:Host"]}:5037. Version is {version}.");

            _adbClient = adbClient;

            return _adbClient;
        }
    }
}