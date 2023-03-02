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
using System.Text.Json;
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
        private readonly ConcurrentDictionary<string, Measurement<int>> connectedDevices = new();
        public MetricsService()
        {
            meter = new Meter(IMetricsService.MeterName);
            meter.CreateObservableUpDownCounter("checker_devices_total", () => connectedDevices.Select(kvp => kvp.Value));
        }

        public void CaptureDeviceConnected(string serial)
        {
            UpdateMeasurment(serial, 1);
        }

        public void CaptureDeviceDisconnected(string serial)
        {
            UpdateMeasurment(serial, 0);
        }

        private void UpdateMeasurment(string serial, int count)
        {
            var measurment = new Measurement<int>(count, new KeyValuePair<string, object>("serial", serial));
            connectedDevices.AddOrUpdate(serial, measurment, (_, _) => measurment);
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
