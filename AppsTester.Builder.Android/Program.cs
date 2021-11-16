using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpAdbClient;
using SharpAdbClient.DeviceCommands;

namespace AppsTester.Builder.Android
{
    class Program
    {
        static async Task Main()
        {
            await Host
                .CreateDefaultBuilder()
                .ConfigureServices((context, collection) =>
                {
                    collection.AddHostedService<AndroidApplicationTestingBackgroundService>();
                    collection.AddHttpClient();
                    collection.AddSingleton<AndroidApplicationTester>();
                })
                .RunConsoleAsync();
        }

        static void Complete()
        {

        }
    }
}