using System;
using System.IO;
using System.Threading.Tasks;
using AppsTester.Checker.Android.Adb;
using AppsTester.Checker.Android.Devices;
using AppsTester.Checker.Android.Gradle;
using AppsTester.Checker.Android.Instrumentations;
using AppsTester.Shared.Files;
using AppsTester.Shared.RabbitMq;
using AppsTester.Shared.SubmissionChecker;
using Medallion.Threading;
using Medallion.Threading.FileSystem;
using Medallion.Threading.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace AppsTester.Checker.Android
{
    internal static class Program
    {
        private static async Task Main()
        {
            await Host
                .CreateDefaultBuilder()
                .ConfigureServices((builder, collection) =>
                {
                    collection.AddSingleton<IAdbClientProvider, AdbClientProvider>();
                    
                    collection.AddScoped<IAdbDevicesProvider, AdbDevicesProvider>();
                    collection.AddScoped<IGradleRunner, GradleRunner>();
                    collection.AddScoped<IInstrumentationsOutputParser, InstrumentationsOutputParser>();

                    collection.Configure<ControllerOptions>(builder.Configuration.GetSection("Controller"));
                    collection.AddSubmissionChecker<AndroidApplicationTester>(checkerSystemName: "android");

                    collection.AddSingleton<IReservedDevicesProvider, ReservedDevicesProvider>(provider =>
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

                    collection.AddTemporaryFolders();
                    collection.AddRabbitMq();

                    //collection.AddHostedService<AndroidApplicationTestingBackgroundService>();
                    collection.AddHttpClient();
                    //collection.AddSingleton<AndroidApplicationTester>();
                })
                .RunConsoleAsync();
        }
    }
}