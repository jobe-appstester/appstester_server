using System.Threading.Tasks;
using AppsTester.Checker.Android.Adb;
using AppsTester.Checker.Android.Gradle;
using AppsTester.Checker.Android.Instrumentations;
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
                    collection.AddSingleton<IGradleRunner, GradleRunner>();
                    collection.AddSingleton<IInstrumentationsOutputParser,InstrumentationsOutputParser>();

                    collection.AddRabbitMq();

                    collection.AddHostedService<AndroidApplicationTestingBackgroundService>();
                    collection.AddHttpClient();
                    collection.AddSingleton<AndroidApplicationTester>();
                })
                .RunConsoleAsync();
        }
    }
}