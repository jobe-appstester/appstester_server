using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace AppsTester.Controller.Metrics
{
    public interface IMetricsService
    {
        public const string MeterName = "AppsTester.Controller";
        void CaptureSubmissionCheckResult(int questionNumber, string authorId, int handleTimeMs, string resultStatus, string testingDevice, string testingServer);
    }
    public class MetricsService : IMetricsService
    {
        private readonly Meter meter;
        private readonly Counter<int> submissionResultCounter;
        public MetricsService()
        {
            meter = new Meter(IMetricsService.MeterName);
            submissionResultCounter = meter.CreateCounter<int>("submission-result");
        }
        public void CaptureSubmissionCheckResult(int questionNumber, string authorId, int handleTimeMs, string resultStatus, string testingDevice, string testingServer)
        {
            submissionResultCounter.Add(1,
                new KeyValuePair<string, object>(nameof(questionNumber), questionNumber),
                new KeyValuePair<string, object>(nameof(authorId), authorId),
                new KeyValuePair<string, object>(nameof(handleTimeMs), handleTimeMs),
                new KeyValuePair<string, object>(nameof(resultStatus), resultStatus),
                new KeyValuePair<string, object>(nameof(testingDevice), testingDevice),
                new KeyValuePair<string, object>(nameof(testingServer), testingServer)
            );
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
                var resouceBuilder = ResourceBuilder.CreateEmpty().AddService("AppsTester.Controller");
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
