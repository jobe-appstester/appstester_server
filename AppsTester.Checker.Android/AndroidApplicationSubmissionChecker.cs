using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Checker.Android.Adb;
using AppsTester.Checker.Android.Apk;
using AppsTester.Checker.Android.Devices;
using AppsTester.Checker.Android.Gradle;
using AppsTester.Checker.Android.Instrumentations;
using AppsTester.Checker.Android.Results;
using AppsTester.Checker.Android.Statuses;
using AppsTester.Shared.Files;
using AppsTester.Shared.SubmissionChecker;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using SharpAdbClient;
using SharpAdbClient.DeviceCommands;

namespace AppsTester.Checker.Android
{
    internal class AndroidApplicationSubmissionChecker : SubmissionChecker
    {
        private readonly IAdbClientProvider _adbClientProvider;
        private readonly IGradleRunner _gradleRunner;
        private readonly IInstrumentationsOutputParser _instrumentationsOutputParser;
        private readonly ISubmissionProcessingLogger _logger;
        private readonly ITemporaryFolderProvider _temporaryFolderProvider;
        private readonly IReservedDevicesProvider _reservedDevicesProvider;
        private readonly IApkReader _apkReader;

        private readonly ISubmissionFilesProvider _filesProvider;
        private readonly ISubmissionResultSetter _submissionResultSetter;
        private readonly ISubmissionStatusSetter _statusSetter;

        public AndroidApplicationSubmissionChecker(IAdbClientProvider adbClientProvider,
            IGradleRunner gradleRunner,
            IInstrumentationsOutputParser instrumentationsOutputParser,
            ITemporaryFolderProvider temporaryFolderProvider,
            ISubmissionStatusSetter statusSetter,
            ISubmissionResultSetter submissionResultSetter,
            ISubmissionFilesProvider filesProvider,
            IReservedDevicesProvider reservedDevicesProvider,
            ISubmissionProcessingLogger logger,
            IApkReader apkReader)
            : base(submissionResultSetter)
        {
            _adbClientProvider = adbClientProvider;
            _gradleRunner = gradleRunner;
            _instrumentationsOutputParser = instrumentationsOutputParser;
            _temporaryFolderProvider = temporaryFolderProvider;
            _statusSetter = statusSetter;
            _submissionResultSetter = submissionResultSetter;
            _filesProvider = filesProvider;
            _reservedDevicesProvider = reservedDevicesProvider;
            _logger = logger;
            _apkReader = apkReader;
        }

        private async Task ExtractTemplateFilesAsync(ITemporaryFolder temporaryFolder)
        {
            var fileStream = await _filesProvider.GetFileAsync("template");
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            await Task.Run(() => archive.ExtractToDirectory(temporaryFolder.AbsolutePath, true));
            _logger.LogInformation("Extracted template files into the directory: {temporaryFolder}", temporaryFolder);
        }

        private async Task ExtractSubmitFilesAsync(ITemporaryFolder temporaryFolder)
        {
            await using var downloadFileStream = await _filesProvider.GetFileAsync("submission");

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

            _logger.LogInformation("Extracted submit files into the directory: {temporaryFolder}", temporaryFolder);
        }

        protected override async Task<object> CheckSubmissionCoreAsync(CancellationToken cancellationToken)
        {
            await _statusSetter.SetStatusAsync(new ProcessingStatus("checking_started"), cancellationToken);

            using var temporaryFolder = _temporaryFolderProvider.Get();

            _logger.LogInformation("Generated temporary directory: {temporaryFolder}", temporaryFolder);

            await _statusSetter.SetStatusAsync(new ProcessingStatus("unzip_files"), cancellationToken);

            try
            {
                await ExtractSubmitFilesAsync(temporaryFolder);
            }
            catch (ZipException e)
            {
                Console.WriteLine(e);

                return new ValidationErrorResult(GradleError: "Cannot extract submitted file.");
            }

            await ExtractTemplateFilesAsync(temporaryFolder);

            await _statusSetter.SetStatusAsync(new ProcessingStatus("gradle_build"), cancellationToken);

            if (!_gradleRunner.IsGradlewInstalledInDirectory(temporaryFolder.AbsolutePath))
                return new ValidationErrorResult(
                    GradleError: "Can't find Gradlew launcher. Please, check template and submission files.");

            var assembleDebugTaskResult = await _gradleRunner.ExecuteTaskAsync(
                tempDirectory: temporaryFolder.AbsolutePath, taskName: "assembleDebug", cancellationToken);
            if (!assembleDebugTaskResult.IsSuccessful)
                return CompilationResult(assembleDebugTaskResult);

            var assembleDebugAndroidTestResult =
                await _gradleRunner.ExecuteTaskAsync(tempDirectory: temporaryFolder.AbsolutePath,
                    taskName: "assembleDebugAndroidTest", cancellationToken);
            if (!assembleDebugAndroidTestResult.IsSuccessful)
                return CompilationResult(assembleDebugTaskResult);

            await _statusSetter.SetStatusAsync(new ProcessingStatus("install_application"), cancellationToken);

            var adbClient = _adbClientProvider.GetAdbClient();

            using var device = await _reservedDevicesProvider.ReserveDeviceAsync(cancellationToken);
            var deviceData = device.DeviceData;

            var packageManager = new PackageManager(adbClient, deviceData);

            var baseApksPath = Path.Join(temporaryFolder.AbsolutePath, "app", "build", "outputs", "apk");

            var applicationApkFile = Path.Join(baseApksPath, "debug", "app-debug.apk");
            packageManager.InstallPackage(applicationApkFile, reinstall: false);
            _logger.LogInformation("Reinstalled debug application in directory: {temporaryFolder}", temporaryFolder);

            var testingApkFile = Path.Join(baseApksPath, "androidTest", "debug", "app-debug-androidTest.apk");
            packageManager.InstallPackage(testingApkFile, reinstall: false);
            _logger.LogInformation("Reinstalled androidTest application in directory: {temporaryFolder}",
                temporaryFolder);

            await _statusSetter.SetStatusAsync(new ProcessingStatus("test"), cancellationToken);

            var consoleOutputReceiver = new ConsoleOutputReceiver();

            _logger.LogInformation("Started testing of Android application");

            await adbClient.ExecuteRemoteCommandAsync(
                $"am instrument -r -w {await _apkReader.ReadPackageNameAsync(testingApkFile)}",
                deviceData,
                consoleOutputReceiver, Encoding.UTF8, cancellationToken);

            _logger.LogInformation("Completed testing of Android application");

            var consoleOutput = consoleOutputReceiver.ToString();

            packageManager.UninstallPackage(await _apkReader.ReadPackageNameAsync(applicationApkFile));
            packageManager.UninstallPackage(await _apkReader.ReadPackageNameAsync(testingApkFile));

            var result = _instrumentationsOutputParser.Parse(consoleOutput);
            return result.GetResult<CheckResult>();
        }

        private static CompilationErrorResult CompilationResult(GradleTaskExecutionResult taskExecutionResult)
        {
            var totalErrorStringBuilder = new StringBuilder();
            totalErrorStringBuilder.AppendLine(taskExecutionResult.StandardOutput);
            totalErrorStringBuilder.AppendLine();
            totalErrorStringBuilder.AppendLine(taskExecutionResult.StandardError);

            return new CompilationErrorResult(GradleError: totalErrorStringBuilder.ToString().Trim());
        }
    }
}