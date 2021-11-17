using System.Threading.Tasks;
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
                    collection.AddHostedService<AndroidApplicationTestingBackgroundService>();
                    collection.AddHttpClient();
                    collection.AddSingleton<AndroidApplicationTester>();
                })
                .RunConsoleAsync();
        }
    }
}