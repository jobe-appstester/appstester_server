using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Checker.Android.Adb;
using AppsTester.Checker.Android.Gradle;
using AppsTester.Checker.Android.Instrumentations;
using AppsTester.Shared;
using AppsTester.Shared.RabbitMq;
using EasyNetQ;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharpAdbClient;
using SharpAdbClient.DeviceCommands;

namespace AppsTester.Checker.Android
{
    internal class AndroidApplicationTester
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAdbClientProvider _adbClientProvider;
        private readonly IRabbitBusProvider _rabbitBusProvider;
        private readonly IGradleRunner _gradleRunner;
        private readonly IInstrumentationsOutputParser _instrumentationsOutputParser;
        private readonly ILogger<AndroidApplicationTester> _logger;

        public AndroidApplicationTester(
            IConfiguration configuration,
            ILogger<AndroidApplicationTester> logger,
            IHttpClientFactory httpClientFactory,
            IAdbClientProvider adbClientProvider,
            IRabbitBusProvider rabbitBusProvider,
            IGradleRunner gradleRunner,
            IInstrumentationsOutputParser instrumentationsOutputParser)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _adbClientProvider = adbClientProvider;
            _rabbitBusProvider = rabbitBusProvider;
            _gradleRunner = gradleRunner;
            _instrumentationsOutputParser = instrumentationsOutputParser;
        }

        public async Task<SubmissionCheckResult> CheckSubmissionAsync(
            SubmissionCheckRequest submissionCheckRequest,
            DeviceData deviceData,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(submissionCheckRequest.Parameters["android_package_name"] as string))
            {
                return new SubmissionCheckResult
                {
                    Id = submissionCheckRequest.Id,
                    Grade = 0,
                    GradleError = "Invalid Android Package Name. Please, check parameter's value in question settings.",
                    ResultCode = SubmissionCheckResultCode.CompilationError,
                    TestResults = new List<SubmissionCheckTestResult>(),
                    TotalGrade = 0
                };
            }

            var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            var submissionCheckStatusEvent = new SubmissionCheckStatusEvent
            {
                SubmissionId = submissionCheckRequest.Id,
                OccurenceDateTime = DateTime.UtcNow
            };
            submissionCheckStatusEvent.SetStatus(new AndroidCheckStatus { Status = "checking_started" });
            await rabbitConnection.PubSub.PublishAsync(submissionCheckStatusEvent);

            var tempDirectory = CreateBuildDirectory(submissionCheckRequest);
            _logger.LogInformation($"Generated temporary directory: {tempDirectory}");

            submissionCheckStatusEvent = new SubmissionCheckStatusEvent
            {
                SubmissionId = submissionCheckRequest.Id,
                OccurenceDateTime = DateTime.UtcNow
            };
            submissionCheckStatusEvent.SetStatus(new AndroidCheckStatus { Status = "unzip_files" });
            await rabbitConnection.PubSub.PublishAsync(submissionCheckStatusEvent);

            try
            {
                await ExtractSubmitFilesAsync(submissionCheckRequest, tempDirectory);
            }
            catch (ZipException e)
            {
                Console.WriteLine(e);
                return new SubmissionCheckResult
                {
                    Id = submissionCheckRequest.Id,
                    Grade = 0,
                    GradleError = "Cannot extract the ZIP file",
                    ResultCode = SubmissionCheckResultCode.CompilationError,
                    TestResults = new List<SubmissionCheckTestResult>(),
                    TotalGrade = 0
                };
            }

            await ExtractTemplateFilesAsync(submissionCheckRequest, tempDirectory);

            submissionCheckStatusEvent = new SubmissionCheckStatusEvent
            {
                SubmissionId = submissionCheckRequest.Id,
                OccurenceDateTime = DateTime.UtcNow
            };
            submissionCheckStatusEvent.SetStatus(new AndroidCheckStatus { Status = "gradle_build" });
            await rabbitConnection.PubSub.PublishAsync(submissionCheckStatusEvent);

            var assembleDebugTaskResult = await _gradleRunner.ExecuteTaskAsync(tempDirectory, "assembleDebug");
            if (assembleDebugTaskResult.ExitCode != 0)
            {
                return new SubmissionCheckResult
                {
                    Id = submissionCheckRequest.Id,
                    Grade = 0,
                    TotalGrade = 0,
                    TestResults = new List<SubmissionCheckTestResult>(),
                    GradleError = (assembleDebugTaskResult.StandardOutput + Environment.NewLine + Environment.NewLine +
                                   assembleDebugTaskResult.StandardError).Trim(),
                    ResultCode = SubmissionCheckResultCode.CompilationError
                };
            }

            var assembleDebugAndroidTaskResult =
                await _gradleRunner.ExecuteTaskAsync(tempDirectory, "assembleDebugAndroidTest");
            if (assembleDebugAndroidTaskResult.ExitCode != 0)
            {
                return new SubmissionCheckResult
                {
                    Id = submissionCheckRequest.Id,
                    Grade = 0,
                    TotalGrade = 0,
                    TestResults = new List<SubmissionCheckTestResult>(),
                    GradleError = (assembleDebugAndroidTaskResult.StandardOutput + Environment.NewLine +
                                   Environment.NewLine + assembleDebugAndroidTaskResult.StandardError).Trim(),
                    ResultCode = SubmissionCheckResultCode.CompilationError
                };
            }

            var adbClient = _adbClientProvider.GetAdbClient();

            submissionCheckStatusEvent = new SubmissionCheckStatusEvent
            {
                SubmissionId = submissionCheckRequest.Id,
                OccurenceDateTime = DateTime.UtcNow
            };
            submissionCheckStatusEvent.SetStatus(new AndroidCheckStatus { Status = "install_application" });
            await rabbitConnection.PubSub.PublishAsync(submissionCheckStatusEvent);

            var packageManager = new PackageManager(adbClient, deviceData);

            foreach (var package in packageManager.Packages.Where(p => p.Key.Contains("profexam")))
                packageManager.UninstallPackage(package.Key);

            var apkFilePath = Path.Join(tempDirectory, "app", "build", "outputs", "apk", "debug", "app-debug.apk");
            packageManager.InstallPackage(apkFilePath, true);
            _logger.LogInformation($"Reinstalled debug application in directory: {tempDirectory}");

            var apkFilePath2 = Path.Join(tempDirectory, "app", "build", "outputs", "apk", "androidTest", "debug",
                "app-debug-androidTest.apk");
            packageManager.InstallPackage(apkFilePath2, true);
            _logger.LogInformation($"Reinstalled androidTest application in directory: {tempDirectory}");

            submissionCheckStatusEvent = new SubmissionCheckStatusEvent
            {
                SubmissionId = submissionCheckRequest.Id,
                OccurenceDateTime = DateTime.UtcNow
            };
            submissionCheckStatusEvent.SetStatus(new AndroidCheckStatus { Status = "test" });
            await rabbitConnection.PubSub.PublishAsync(submissionCheckStatusEvent);

            var consoleOutputReceiver = new ConsoleOutputReceiver();
            _logger.LogInformation($"Started testing of Android application for event {submissionCheckRequest.Id}");
            await adbClient.ExecuteRemoteCommandAsync(
                $"am instrument -r -w {submissionCheckRequest.Parameters["android_package_name"]}", deviceData,
                consoleOutputReceiver, Encoding.UTF8, cancellationToken);
            _logger.LogInformation($"Completed testing of Android application for event {submissionCheckRequest.Id}");
            var consoleOutput = consoleOutputReceiver.ToString();

            Directory.Delete(tempDirectory, true);

            return _instrumentationsOutputParser.Parse(submissionCheckRequest, consoleOutput);
        }

        private async Task ExtractTemplateFilesAsync(SubmissionCheckRequest submissionCheckRequest,
            string tempDirectory)
        {
            var fileStream = await DownloadFileAsync(submissionCheckRequest.Files["template"]);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            await Task.Run(() => archive.ExtractToDirectory(tempDirectory, true));
            _logger.LogInformation($"Extracted template files into the directory: {tempDirectory}");
        }

        private async Task ExtractSubmitFilesAsync(SubmissionCheckRequest submissionCheckRequest, string tempDirectory)
        {
            var downloadFileStream = await DownloadFileAsync(submissionCheckRequest.Files["submission"]);

            var downloadedFile = new MemoryStream();
            await downloadFileStream.CopyToAsync(downloadedFile);

            await Task.Run(() =>
            {
                var fastZip = new FastZip();
                fastZip.ExtractZip(downloadedFile, tempDirectory, FastZip.Overwrite.Always, null, null, null, false, true);
            });
            _logger.LogInformation($"Extracted submit files into the directory: {tempDirectory}");
        }

        private async Task<Stream> DownloadFileAsync(string fileHash)
        {
            using var httpClient = _httpClientFactory.CreateClient();
            var fileStream = await httpClient.GetStreamAsync(
                $"{_configuration["Controller:Url"]}/api/v1/files/{fileHash}");
            return fileStream;
        }

        private static string CreateBuildDirectory(SubmissionCheckRequest submissionCheckRequest)
        {
            var tempDirectory = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }
    }
}