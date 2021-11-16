using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace AppsTester.Builder.Android
{
    internal class AndroidApplicationTestingBackgroundService : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AndroidApplicationTester _androidApplicationTester;

        public AndroidApplicationTestingBackgroundService(IHttpClientFactory httpClientFactory, AndroidApplicationTester androidApplicationTester)
        {
            _httpClientFactory = httpClientFactory;
            _androidApplicationTester = androidApplicationTester;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _androidApplicationTester.TestAsync(new TestApplicationRequest{ RequestId = Guid.NewGuid(), SubmitZipArchiveFileStream = new FileStream("../../../submit.zip", FileMode.Open), TemplateZipArchiveFileStream = new FileStream("../../../template.zip", FileMode.Open) });
            while (true)
            {
                _httpClientFactory.CreateClient();
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}