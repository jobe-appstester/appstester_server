using System;
using System.IO;
using System.Threading.Tasks;
using AppsTester.Checker.Android.Adb;
using AppsTester.Checker.Android.Apk;
using AppsTester.Checker.Android.Devices;
using AppsTester.Checker.Android.Gradle;
using AppsTester.Checker.Android.Instrumentations;
using AppsTester.Checker.Android.Metrics;
using AppsTester.Shared.Files;
using AppsTester.Shared.RabbitMq;
using AppsTester.Shared.SubmissionChecker;
using Medallion.Threading;
using Medallion.Threading.FileSystem;
using Medallion.Threading.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AppsTester.Checker.Android
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var app = Host
                .CreateDefaultBuilder(args)
                .ConfigureServices((builder, services) =>
                {
                    services.AddOptions<AdbOptions>()
                        .Bind(builder.Configuration.GetSection("Adb"))
                        .ValidateDataAnnotations();
                    services.AddSingleton<IAdbClientProvider, AdbClientProvider>();
                    services.AddTransient<IApkReader, ApkReader>();

                    services.AddSingleton<IAdbDevicesProvider, AdbDevicesProvider>();
                    services.AddScoped<IGradleRunner, GradleRunner>();
                    services.AddScoped<IInstrumentationsOutputParser, InstrumentationsOutputParser>();

                    services.AddOptions<ControllerOptions>()
                        .Bind(builder.Configuration.GetSection("Controller"))
                        .ValidateDataAnnotations();
                    services.AddSubmissionChecker<AndroidApplicationSubmissionChecker>(
                        checkerSystemName: "android",
                        parallelExecutions: 6);

                    services.AddSingleton<IReservedDevicesProvider, ReservedDevicesProvider>(provider =>
                    {
                        var redisConnectionString = provider.GetService<IConfiguration>()
                            .GetConnectionString(name: "DevicesSynchronizationRedis");

                        IDistributedLockProvider distributedLockProvider;
                        if (string.IsNullOrWhiteSpace(redisConnectionString))
                        {
                            distributedLockProvider =
                                new FileDistributedSynchronizationProvider(
                                    new DirectoryInfo(Environment.CurrentDirectory));
                        }
                        else
                        {
                            distributedLockProvider = new RedisDistributedSynchronizationProvider(
                                database: ConnectionMultiplexer.Connect(redisConnectionString).GetDatabase());
                        }

                        return new ReservedDevicesProvider(
                            provider.GetService<IAdbDevicesProvider>(), distributedLockProvider);
                    });

                    services.AddMetrics(builder.Configuration);

                    services.AddTemporaryFolders();
                    services.AddRabbitMq();

                    services.AddHttpClient();
                })
                .ConfigureLogging((_, loggingBuilder) =>
                {
                    loggingBuilder.AddConsole();
                    loggingBuilder.AddSentry();
                })
                .UseConsoleLifetime()
                .Build();

            // initialize avd provider for devices listening
            app.Services.GetRequiredService<IAdbClientProvider>();

            await app.RunAsync();
        }
    }
}