using System.Threading.Tasks;
using AppsTester.Checker.Android.Adb;
using AppsTester.Shared.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AppsTester.Checker.Android
{
    internal static class Program
    {
        private static async Task Main()
        {
            await Host
                .CreateDefaultBuilder()
                .ConfigureServices((_, collection) =>
                {
                    collection.AddSingleton<IAdbClientProvider, AdbClientProvider>();
                    collection.AddTransient<IAdbDevicesProvider, AdbDevicesProvider>();

                    collection.AddRabbitMq();

                    collection.AddHostedService<AndroidApplicationTestingBackgroundService>();
                    collection.AddHttpClient();
                    collection.AddSingleton<AndroidApplicationTester>();
                })
                .RunConsoleAsync();
        }
    }
}