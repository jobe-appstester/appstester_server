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
using AppsTester.Shared.Files;
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
        private readonly ITemporaryFolderProvider _temporaryFolderProvider;

        public AndroidApplicationTester(
            IConfiguration configuration,
            ILogger<AndroidApplicationTester> logger,
            IHttpClientFactory httpClientFactory,
            IAdbClientProvider adbClientProvider,
            IRabbitBusProvider rabbitBusProvider,
            IGradleRunner gradleRunner,
            IInstrumentationsOutputParser instrumentationsOutputParser,
            ITemporaryFolderProvider temporaryFolderProvider)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _adbClientProvider = adbClientProvider;
            _rabbitBusProvider = rabbitBusProvider;
            _gradleRunner = gradleRunner;
            _instrumentationsOutputParser = instrumentationsOutputParser;
            _temporaryFolderProvider = temporaryFolderProvider;
        }

        public async Task<SubmissionCheckResult> CheckSubmissionAsync(
            SubmissionCheckRequest submissionCheckRequest,
            DeviceData deviceData,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(submissionCheckRequest.Parameters["android_package_name"] as string))
                return ValidationResult(submissionCheckRequest,
                    "Invalid Android Package Name. Please, check parameter's value in question settings.");

            await SetStatusAsync(submissionCheckRequest, "checking_started");

            using var temporaryFolder = _temporaryFolderProvider.Get();

            _logger.LogInformation($"Generated temporary directory: {temporaryFolder.AbsolutePath}");

            await SetStatusAsync(submissionCheckRequest, "unzip_files");

            try
            {
                await ExtractSubmitFilesAsync(submissionCheckRequest, temporaryFolder);
            }
            catch (ZipException e)
            {
                Console.WriteLine(e);
                return ValidationResult(submissionCheckRequest, "Cannot extract submitted file.");
            }

            await ExtractTemplateFilesAsync(submissionCheckRequest, temporaryFolder);

            await SetStatusAsync(submissionCheckRequest, "gradle_build");

            if (!_gradleRunner.IsGradlewInstalledInDirectory(temporaryFolder.AbsolutePath))
                return ValidationResult(submissionCheckRequest,
                    "Can't find Gradlew launcher. Please, check template and submission files.");

            var assembleDebugTaskResult = await _gradleRunner.ExecuteTaskAsync(temporaryFolder.AbsolutePath, "assembleDebug");
            if (!assembleDebugTaskResult.IsSuccessful)
                return CompilationErrorResult(submissionCheckRequest, assembleDebugTaskResult);

            var assembleDebugAndroidTestResult =
                await _gradleRunner.ExecuteTaskAsync(temporaryFolder.AbsolutePath, "assembleDebugAndroidTest");
            if (!assembleDebugAndroidTestResult.IsSuccessful)
                return CompilationErrorResult(submissionCheckRequest, assembleDebugTaskResult);

            await SetStatusAsync(submissionCheckRequest, "install_application");

            var adbClient = _adbClientProvider.GetAdbClient();
            var packageManager = new PackageManager(adbClient, deviceData);

            foreach (var package in packageManager.Packages.Where(p => p.Key.Contains("profexam")))
                packageManager.UninstallPackage(package.Key);

            var apkFilePath = Path.Join(temporaryFolder.AbsolutePath, "app", "build", "outputs", "apk", "debug", "app-debug.apk");
            packageManager.InstallPackage(apkFilePath, true);
            _logger.LogInformation($"Reinstalled debug application in directory: {temporaryFolder.AbsolutePath}");

            var apkFilePath2 = Path.Join(temporaryFolder.AbsolutePath, "app", "build", "outputs", "apk", "androidTest", "debug",
                "app-debug-androidTest.apk");
            packageManager.InstallPackage(apkFilePath2, true);
            _logger.LogInformation($"Reinstalled androidTest application in directory: {temporaryFolder.AbsolutePath}");

            await SetStatusAsync(submissionCheckRequest, "test");

            var consoleOutputReceiver = new ConsoleOutputReceiver();
            _logger.LogInformation($"Started testing of Android application for event {submissionCheckRequest.Id}");
            await adbClient.ExecuteRemoteCommandAsync(
                $"am instrument -r -w {submissionCheckRequest.Parameters["android_package_name"]}", deviceData,
                consoleOutputReceiver, Encoding.UTF8, cancellationToken);
            _logger.LogInformation($"Completed testing of Android application for event {submissionCheckRequest.Id}");
            var consoleOutput = consoleOutputReceiver.ToString();

            return _instrumentationsOutputParser.Parse(submissionCheckRequest, consoleOutput);
        }

        private async Task SetStatusAsync(SubmissionCheckRequest submissionCheckRequest, string status)
        {
            var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            var submissionCheckStatusEvent = new SubmissionCheckStatusEvent
            {
                SubmissionId = submissionCheckRequest.Id,
                OccurenceDateTime = DateTime.UtcNow
            };
            submissionCheckStatusEvent.SetStatus(new AndroidCheckStatus { Status = status });

            await rabbitConnection.PubSub.PublishAsync(submissionCheckStatusEvent);
        }

        private SubmissionCheckResult ValidationResult(
            SubmissionCheckRequest submissionCheckRequest, string validationMessage)
        {
            return new SubmissionCheckResult
            {
                Id = submissionCheckRequest.Id,
                Grade = 0,
                GradleError = validationMessage,
                ResultCode = SubmissionCheckResultCode.CompilationError,
                TestResults = new List<SubmissionCheckTestResult>(),
                TotalGrade = 0
            };
        }

        private SubmissionCheckResult CompilationErrorResult(
            SubmissionCheckRequest submissionCheckRequest, GradleTaskExecutionResult taskExecutionResult)
        {
            var totalErrorStringBuilder = new StringBuilder();
            totalErrorStringBuilder.AppendLine(taskExecutionResult.StandardOutput);
            totalErrorStringBuilder.AppendLine();
            totalErrorStringBuilder.AppendLine(taskExecutionResult.StandardError);

            return new SubmissionCheckResult
            {
                Id = submissionCheckRequest.Id,
                Grade = 0,
                TotalGrade = 0,
                TestResults = new List<SubmissionCheckTestResult>(),
                GradleError = totalErrorStringBuilder.ToString().Trim(),
                ResultCode = SubmissionCheckResultCode.CompilationError
            };
        }

        private async Task ExtractTemplateFilesAsync(SubmissionCheckRequest submissionCheckRequest,
            ITemporaryFolder temporaryFolder)
        {
            var fileStream = await DownloadFileAsync(submissionCheckRequest.Files["template"]);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            await Task.Run(() => archive.ExtractToDirectory(temporaryFolder.AbsolutePath, true));
            _logger.LogInformation($"Extracted template files into the directory: {temporaryFolder}");
        }

        private async Task ExtractSubmitFilesAsync(SubmissionCheckRequest submissionCheckRequest, ITemporaryFolder temporaryFolder)
        {
            await using var downloadFileStream = await DownloadFileAsync(submissionCheckRequest.Files["submission"]);

            await using var downloadedFile = new MemoryStream();
            await downloadFileStream.CopyToAsync(downloadedFile);

            using var zipArchive = new ZipArchive(downloadedFile);

            var levelsToReduce = zipArchive.Entries.Min(e => e.FullName.Split('/').Length);
            if (levelsToReduce > 0)
            {
                var entriesToMove = zipArchive.Entries.ToList();
                foreach (var entryToMove in entriesToMove)
                {
                    var movedEntry =
                        zipArchive.CreateEntry(string.Join('/', entryToMove.FullName.Split('/').Skip(levelsToReduce)));

                    await using var entryToMoveStream = entryToMove.Open();
                    await using var movedEntryStream = movedEntry.Open();
                    await entryToMoveStream.CopyToAsync(movedEntryStream);

                    entryToMove.Delete();
                }
            }

            zipArchive.ExtractToDirectory(temporaryFolder.AbsolutePath, overwriteFiles: true);

            _logger.LogInformation($"Extracted submit files into the directory: {temporaryFolder}");
        }

        private async Task<Stream> DownloadFileAsync(string fileHash)
        {
            using var httpClient = _httpClientFactory.CreateClient();
            var fileStream = await httpClient.GetStreamAsync(
                $"{_configuration["Controller:Url"]}/api/v1/files/{fileHash}");
            return fileStream;
        }
    }
}