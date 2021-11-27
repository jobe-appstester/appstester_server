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
using AppsTester.Shared.Events;
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

        public async Task<SubmissionCheckResultEvent> CheckSubmissionAsync(
            SubmissionCheckRequestEvent submissionCheckRequestEvent,
            DeviceData deviceData,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(submissionCheckRequestEvent.PlainParameters["android_package_name"] as string))
                return ValidationResult(submissionCheckRequestEvent,
                    "Invalid Android Package Name. Please, check parameter's value in question settings.");

            await SetStatusAsync(submissionCheckRequestEvent, "checking_started");

            using var temporaryFolder = _temporaryFolderProvider.Get();

            _logger.LogInformation($"Generated temporary directory: {temporaryFolder.AbsolutePath}");

            await SetStatusAsync(submissionCheckRequestEvent, "unzip_files");

            try
            {
                await ExtractSubmitFilesAsync(submissionCheckRequestEvent, temporaryFolder);
            }
            catch (ZipException e)
            {
                Console.WriteLine(e);
                return ValidationResult(submissionCheckRequestEvent, "Cannot extract submitted file.");
            }

            await ExtractTemplateFilesAsync(submissionCheckRequestEvent, temporaryFolder);

            await SetStatusAsync(submissionCheckRequestEvent, "gradle_build");

            if (!_gradleRunner.IsGradlewInstalledInDirectory(temporaryFolder.AbsolutePath))
                return ValidationResult(submissionCheckRequestEvent,
                    "Can't find Gradlew launcher. Please, check template and submission files.");

            var assembleDebugTaskResult = await _gradleRunner.ExecuteTaskAsync(tempDirectory: temporaryFolder.AbsolutePath, taskName: "assembleDebug", cancellationToken);
            if (!assembleDebugTaskResult.IsSuccessful)
                return CompilationErrorResult(submissionCheckRequestEvent, assembleDebugTaskResult);

            var assembleDebugAndroidTestResult =
                await _gradleRunner.ExecuteTaskAsync(tempDirectory: temporaryFolder.AbsolutePath, taskName: "assembleDebugAndroidTest", cancellationToken);
            if (!assembleDebugAndroidTestResult.IsSuccessful)
                return CompilationErrorResult(submissionCheckRequestEvent, assembleDebugTaskResult);

            await SetStatusAsync(submissionCheckRequestEvent, "install_application");

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

            await SetStatusAsync(submissionCheckRequestEvent, "test");

            var consoleOutputReceiver = new ConsoleOutputReceiver();
            _logger.LogInformation($"Started testing of Android application for event {submissionCheckRequestEvent.SubmissionId}");
            await adbClient.ExecuteRemoteCommandAsync(
                $"am instrument -r -w {submissionCheckRequestEvent.PlainParameters["android_package_name"]}", deviceData,
                consoleOutputReceiver, Encoding.UTF8, cancellationToken);
            _logger.LogInformation($"Completed testing of Android application for event {submissionCheckRequestEvent.SubmissionId}");
            var consoleOutput = consoleOutputReceiver.ToString();

            return _instrumentationsOutputParser.Parse(submissionCheckRequestEvent, consoleOutput);
        }

        private async Task SetStatusAsync(SubmissionCheckRequestEvent submissionCheckRequestEvent, string status)
        {
            var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            var submissionCheckStatusEvent = new SubmissionCheckStatusEvent
                {
                    SubmissionId = submissionCheckRequestEvent.SubmissionId
                }
                .WithStatus(new AndroidCheckStatus { Status = status });

            await rabbitConnection.PubSub.PublishAsync(submissionCheckStatusEvent);
        }

        private SubmissionCheckResultEvent ValidationResult(
            SubmissionCheckRequestEvent submissionCheckRequestEvent, string validationMessage)
        {
            return new SubmissionCheckResultEvent { SubmissionId = submissionCheckRequestEvent.SubmissionId }
                .WithResult(
                    new AndroidCheckResult
                    {
                        Grade = 0,
                        TotalGrade = 0,
                        TestResults = new List<SubmissionCheckTestResult>(),
                        GradleError = validationMessage,
                        ResultCode = SubmissionCheckResultCode.CompilationError,
                    });
        }

        private SubmissionCheckResultEvent CompilationErrorResult(
            SubmissionCheckRequestEvent submissionCheckRequestEvent, GradleTaskExecutionResult taskExecutionResult)
        {
            var totalErrorStringBuilder = new StringBuilder();
            totalErrorStringBuilder.AppendLine(taskExecutionResult.StandardOutput);
            totalErrorStringBuilder.AppendLine();
            totalErrorStringBuilder.AppendLine(taskExecutionResult.StandardError);

            return new SubmissionCheckResultEvent { SubmissionId = submissionCheckRequestEvent.SubmissionId }
                .WithResult(
                    new AndroidCheckResult
                    {
                        Grade = 0,
                        TotalGrade = 0,
                        TestResults = new List<SubmissionCheckTestResult>(),
                        GradleError = totalErrorStringBuilder.ToString().Trim(),
                        ResultCode = SubmissionCheckResultCode.CompilationError,
                    });
        }

        private async Task ExtractTemplateFilesAsync(SubmissionCheckRequestEvent submissionCheckRequestEvent,
            ITemporaryFolder temporaryFolder)
        {
            var fileStream = await DownloadFileAsync(submissionCheckRequestEvent.Files["template"]);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            await Task.Run(() => archive.ExtractToDirectory(temporaryFolder.AbsolutePath, true));
            _logger.LogInformation($"Extracted template files into the directory: {temporaryFolder}");
        }

        private async Task ExtractSubmitFilesAsync(SubmissionCheckRequestEvent submissionCheckRequestEvent, ITemporaryFolder temporaryFolder)
        {
            await using var downloadFileStream = await DownloadFileAsync(submissionCheckRequestEvent.Files["submission"]);

            await using var downloadedFile = new MemoryStream();
            await downloadFileStream.CopyToAsync(downloadedFile);

            using (var mutableZipArchive = new ZipArchive(downloadedFile, ZipArchiveMode.Update, leaveOpen: true))
            {
                var levelsToReduce = mutableZipArchive
                    .Entries
                    .Where(e => e.Length != 0)
                    .Min(e => e.FullName.Count(c => c == '/'));

                if (levelsToReduce > 0)
                {
                    var entriesToMove = mutableZipArchive.Entries.ToList();
                    foreach (var entryToMove in entriesToMove)
                    {
                        var newEntryPath = string.Join('/', entryToMove.FullName.Split('/').Skip(levelsToReduce));
                        if (newEntryPath == string.Empty)
                            continue;

                        var movedEntry = mutableZipArchive.CreateEntry(newEntryPath);

                        await using (var entryToMoveStream = entryToMove.Open())
                        await using (var movedEntryStream = movedEntry.Open())
                            await entryToMoveStream.CopyToAsync(movedEntryStream);

                        entryToMove.Delete();
                    }
                }
            }

            downloadedFile.Seek(offset: 0, SeekOrigin.Begin);

            using var zipArchive = new ZipArchive(downloadedFile);

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