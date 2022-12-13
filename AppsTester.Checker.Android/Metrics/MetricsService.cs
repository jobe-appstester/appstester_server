using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppsTester.Checker.Android.Metrics
{
    public interface IMetricsService
    {
        public const string MeterName = "AppsTester.Checker.Android";
        void CaptureDeviceConnected(string serialNumber);
        void CaptureDeviceDisconnected(string serialNumber);
    }
    public class MetricsService : IMetricsService
    {
        private readonly Meter meter;

        /// <summary>
        /// Stores map of serial to label to prevent negative counter number on diconnect events by adb
        /// </summary>
        private readonly ConcurrentDictionary<string, KeyValuePair<string, object>> connectedDevices = new();
        public MetricsService()
        {
            meter = new Meter(IMetricsService.MeterName);
            meter.CreateObservableUpDownCounter("checker_devices_total", () => connectedDevices.Select(kvp => new Measurement<int>(1, kvp.Value)));
        }
        public void CaptureDeviceConnected(string serial)
        {
            connectedDevices.TryAdd(serial, new KeyValuePair<string, object>("serial", serial));
        }

        public void CaptureDeviceDisconnected(string serial)
        {
            connectedDevices.TryRemove(serial, out _);
        }
    }

    public static class MetricsExtensions
    {
        public static void AddMetrics(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IMetricsService, MetricsService>();
            var otlpExporterEndpoint = configuration.GetValue<string>("OtlpExporterEndpoint");
            if (!string.IsNullOrEmpty(otlpExporterEndpoint))
            {
                var resouceBuilder = ResourceBuilder.CreateEmpty().AddService("AppsTester.Checker.Android");
                services.AddOpenTelemetryMetrics(b =>
                {
                    b.SetResourceBuilder(resouceBuilder);
                    b.AddMeter(IMetricsService.MeterName);
                    b.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpExporterEndpoint);
                    });
                });
            }
        }
    }
}
